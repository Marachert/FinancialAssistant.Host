using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityRegistrationLoginTests : IClassFixture<IdentityContractWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IdentityContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public IdentityRegistrationLoginTests(IdentityContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterAndSignIn_ReturnSessionsAndPublishVersionedEvents()
    {
        var email = CreateEmail();
        const string password = "Synthetic-Only-Password-123!";
        var register = new RegisterAccountRequest(email, password, CreateClient("web"));

        var registrationResponse = await client.PostAsJsonAsync(IdentityApiRoutes.Register, register);
        var registrationSession = await registrationResponse.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.NotNull(registrationSession);
        Assert.Equal("Bearer", registrationSession.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(registrationSession.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(registrationSession.RefreshToken));
        Assert.DoesNotContain(email, await registrationResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "user.registered.v1"
                && publication.SchemaVersion == 1
                && publication.SubjectId == registrationSession.User.UserId
                && publication.Data.TryGetValue("userId", out var userId)
                && userId == registrationSession.User.UserId);

        var signInResponse = await client.PostAsJsonAsync(
            IdentityApiRoutes.SignIn,
            new SignInRequest(email, password, CreateClient("android")));
        var signInSession = await signInResponse.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.NotNull(signInSession);
        Assert.Equal(registrationSession.User.UserId, signInSession.User.UserId);
        Assert.NotEqual(registrationSession.AccessToken, signInSession.AccessToken);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "user.signed_in.v1"
                && publication.SchemaVersion == 1
                && publication.SubjectId == signInSession.User.UserId
                && publication.Data.TryGetValue("sessionId", out var sessionId)
                && sessionId == signInSession.User.SessionId);
    }

    [Fact]
    public async Task DuplicateRegistration_ReturnsSafeConflict()
    {
        var email = CreateEmail();
        const string password = "Synthetic-Only-Password-123!";
        var request = new RegisterAccountRequest(email, password, CreateClient("ios"));

        var first = await client.PostAsJsonAsync(IdentityApiRoutes.Register, request);
        var duplicate = await client.PostAsJsonAsync(IdentityApiRoutes.Register, request);
        var body = await duplicate.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<IdentityApiErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.IdentityConflict, problem.Code);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRegistration_ReturnsFieldErrors()
    {
        var request = new RegisterAccountRequest(
            "not-an-email",
            "weak",
            new IdentityClientContext("short", "desktop", null));

        var response = await client.PostAsJsonAsync(IdentityApiRoutes.Register, request);
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ValidationFailed, problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.Contains("email", problem.Errors.Keys);
        Assert.Contains("password", problem.Errors.Keys);
        Assert.Contains("client.clientInstanceId", problem.Errors.Keys);
        Assert.Contains("client.platform", problem.Errors.Keys);
    }

    [Fact]
    public async Task WrongPasswordAndUnknownEmail_ReturnSameFailureAndSafeMetricEvents()
    {
        var email = CreateEmail();
        const string password = "Synthetic-Only-Password-123!";
        await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(email, password, CreateClient("web")));

        var wrongPassword = await client.PostAsJsonAsync(
            IdentityApiRoutes.SignIn,
            new SignInRequest(email, "Different-Synthetic-Password-456!", CreateClient("web")));
        var unknownEmail = await client.PostAsJsonAsync(
            IdentityApiRoutes.SignIn,
            new SignInRequest(CreateEmail(), "Different-Synthetic-Password-456!", CreateClient("web")));
        var wrongProblem = await wrongPassword.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);
        var unknownProblem = await unknownEmail.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.NotNull(wrongProblem);
        Assert.NotNull(unknownProblem);
        Assert.Equal(IdentityErrorCodes.AuthenticationFailed, wrongProblem.Code);
        Assert.Equal(wrongProblem.Code, unknownProblem.Code);
        Assert.Equal(wrongProblem.Title, unknownProblem.Title);
        Assert.Equal(wrongProblem.Detail, unknownProblem.Detail);

        var failures = factory.EventPublisher.Publications
            .Where(publication => publication.EventName == "authentication.failed.v1")
            .ToArray();
        Assert.True(failures.Length >= 2);
        Assert.All(failures, publication =>
        {
            Assert.Null(publication.SubjectId);
            Assert.False(publication.Data.ContainsKey("email"));
            Assert.Equal("credentials_not_accepted", publication.Data["reasonCode"]);
        });
    }

    [Fact]
    public async Task StoredCredential_ContainsOnlyProtectedLookupAndSecretValues()
    {
        var email = CreateEmail();
        const string password = "Synthetic-Only-Password-123!";
        await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(email, password, CreateClient("android")));

        var lookupHasher = factory.Services.GetRequiredService<IEmailLookupHasher>();
        var store = factory.Services.GetRequiredService<IIdentityAccountStore>();
        var lookupHash = lookupHasher.Hash(EmailIdentityNormalizer.Normalize(email));
        var credential = await store.FindCredentialByLookupHashAsync(lookupHash);

        Assert.NotNull(credential);
        Assert.NotEqual(email, credential.LookupKeyHash);
        Assert.NotEqual(password, credential.SecretHash);
        Assert.DoesNotContain(password, credential.SecretHash, StringComparison.Ordinal);
        Assert.Equal("aspnetcore-identity-v3", credential.SecretHashAlgorithm);
    }

    private static IdentityClientContext CreateClient(string platform)
    {
        return new IdentityClientContext(
            $"synthetic-client-{Guid.NewGuid():N}",
            platform,
            "0.0-test");
    }

    private static string CreateEmail()
    {
        return $"synthetic-{Guid.NewGuid():N}@example.invalid";
    }
}
