using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Tests;

public sealed class PhoneVerificationTests : IClassFixture<IdentityContractWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static int phoneSequence = 1000000;
    private readonly IdentityContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public PhoneVerificationTests(IdentityContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task ApprovedVerification_CreatesPhoneAccountAndSession()
    {
        var phone = CreatePhone();
        var clientContext = CreateClient("android");
        var start = await StartAsync(phone, clientContext);
        var startBody = await start.Content.ReadAsStringAsync();
        var challenge = JsonSerializer.Deserialize<PhoneVerificationStartResponse>(startBody, JsonOptions);

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        Assert.NotNull(challenge);
        Assert.DoesNotContain(phone, startBody, StringComparison.Ordinal);
        Assert.DoesNotContain(StubPhoneVerificationProvider.AcceptedCode, startBody, StringComparison.Ordinal);
        Assert.EndsWith(phone[^4..], challenge.MaskedDestination, StringComparison.Ordinal);

        var confirm = await client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationConfirm,
            new PhoneVerificationConfirmRequest(
                challenge.VerificationId,
                StubPhoneVerificationProvider.AcceptedCode,
                clientContext));
        var sessionBody = await confirm.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize<AuthSessionResponse>(sessionBody, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("phone_otp", session.User.AuthenticationMethod);
        Assert.DoesNotContain(phone, sessionBody, StringComparison.Ordinal);
        Assert.DoesNotContain(StubPhoneVerificationProvider.AcceptedCode, sessionBody, StringComparison.Ordinal);

        var hasher = factory.Services.GetRequiredService<IIdentityProviderIdentifierHasher>();
        var store = factory.Services.GetRequiredService<IIdentityFederatedAccountStore>();
        var phoneHash = hasher.Hash("phone", "number", phone);
        var link = await store.FindProviderLinkAsync("phone", phoneHash);
        Assert.NotNull(link);
        Assert.Equal(session.User.UserId, link.AccountId);
        Assert.NotEqual(phone, link.ProviderSubjectHash);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "user.registered.v1"
                && publication.SubjectId == session.User.UserId
                && publication.Data["authenticationMethod"] == "phone_otp");
    }

    [Fact]
    public async Task RepeatedApprovedVerification_ReusesLinkedAccount()
    {
        var phone = CreatePhone();
        var clientContext = CreateClient("ios");
        var firstChallenge = await ReadChallengeAsync(await StartAsync(phone, clientContext));
        var firstSession = await ConfirmAsync(firstChallenge.VerificationId, clientContext);
        var secondChallenge = await ReadChallengeAsync(await StartAsync(phone, clientContext));
        var secondSession = await ConfirmAsync(secondChallenge.VerificationId, clientContext);

        Assert.Equal(firstSession.User.UserId, secondSession.User.UserId);
        Assert.NotEqual(firstSession.User.SessionId, secondSession.User.SessionId);
    }

    [Fact]
    public async Task ImmediateRepeatStart_ReturnsCooldownRateLimit()
    {
        var phone = CreatePhone();
        var clientContext = CreateClient("web");
        var first = await StartAsync(phone, clientContext);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await StartAsync(phone, clientContext);
        var problem = await second.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.RateLimited, problem.Code);
        Assert.True(problem.RetryAfterSeconds > 0);
        Assert.True(second.Headers.RetryAfter?.Delta is not null
            || second.Headers.TryGetValues("Retry-After", out _));
    }

    [Fact]
    public async Task MaximumRejectedAttempts_LocksChallengeWithoutLeakingCode()
    {
        var phone = CreatePhone();
        var clientContext = CreateClient("android");
        var challenge = await ReadChallengeAsync(await StartAsync(phone, clientContext));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                IdentityApiRoutes.PhoneVerificationConfirm,
                new PhoneVerificationConfirmRequest(challenge.VerificationId, "000000", clientContext));
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.DoesNotContain("000000", body, StringComparison.Ordinal);
            Assert.DoesNotContain(phone, body, StringComparison.Ordinal);
        }

        var afterLock = await client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationConfirm,
            new PhoneVerificationConfirmRequest(
                challenge.VerificationId,
                StubPhoneVerificationProvider.AcceptedCode,
                clientContext));
        Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);
    }

    [Fact]
    public async Task DifferentClientCannotConfirmChallenge()
    {
        var phone = CreatePhone();
        var challenge = await ReadChallengeAsync(await StartAsync(phone, CreateClient("android")));
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationConfirm,
            new PhoneVerificationConfirmRequest(
                challenge.VerificationId,
                StubPhoneVerificationProvider.AcceptedCode,
                CreateClient("android")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProviderUnavailable_ReturnsSafeServiceUnavailable()
    {
        var phone = CreatePhone();
        factory.PhoneVerificationProvider.SetBehavior(
            phone,
            PhoneVerificationDispatchStatus.Unavailable);

        var response = await StartAsync(phone, CreateClient("ios"));
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<IdentityApiErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ProviderUnavailable, problem.Code);
        Assert.DoesNotContain(phone, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidStartRequest_ReturnsValidationErrors()
    {
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationStart,
            new PhoneVerificationStartRequest(
                "555-not-e164",
                "recovery",
                new IdentityClientContext("short", "desktop", null)));
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ValidationFailed, problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.Contains("phoneNumber", problem.Errors.Keys);
        Assert.Contains("purpose", problem.Errors.Keys);
        Assert.Contains("client.clientInstanceId", problem.Errors.Keys);
        Assert.Contains("client.platform", problem.Errors.Keys);
    }

    private Task<HttpResponseMessage> StartAsync(
        string phone,
        IdentityClientContext clientContext) =>
        client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationStart,
            new PhoneVerificationStartRequest(
                phone,
                PhoneVerificationPurposes.SignIn,
                clientContext));

    private async Task<AuthSessionResponse> ConfirmAsync(
        string verificationId,
        IdentityClientContext clientContext)
    {
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.PhoneVerificationConfirm,
            new PhoneVerificationConfirmRequest(
                verificationId,
                StubPhoneVerificationProvider.AcceptedCode,
                clientContext));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions))!;
    }

    private static async Task<PhoneVerificationStartResponse> ReadChallengeAsync(
        HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PhoneVerificationStartResponse>(JsonOptions))!;
    }

    private static IdentityClientContext CreateClient(string platform) =>
        new($"synthetic-client-{Guid.NewGuid():N}", platform, "0.0-test");

    private static string CreatePhone() =>
        $"+1555{Interlocked.Increment(ref phoneSequence):D7}";
}
