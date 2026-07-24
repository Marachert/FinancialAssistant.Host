using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionIntakeEndpointTests : IClassFixture<TransactionIntakeWebApplicationFactory>
{
    private readonly HttpClient client;

    public TransactionIntakeEndpointTests(TransactionIntakeWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Intake_ParsesNaturalLanguageIntoStructuredReviewableDraft()
    {
        using var request = CreateRequest(
            "synthetic-intake-structured-0001",
            "Spent $12.50 at Coffee Shop today");

        var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        Assert.StartsWith("draft_", draft.Id, StringComparison.Ordinal);
        Assert.Equal("draft", draft.Status);
        Assert.Equal("expense", draft.Type);
        Assert.Equal(12.50m, draft.Amount);
        Assert.Equal("USD", draft.Currency);
        Assert.Equal("expense.food", draft.CategoryId);
        Assert.Equal("Coffee Shop", draft.Merchant);
        Assert.NotNull(draft.Date);
        Assert.InRange(draft.Confidence, 0.75m, 1m);
        Assert.Empty(draft.Ambiguities);
        Assert.False(draft.RequiresReview);
        Assert.Equal("ai_natural_language", draft.Suggestion.Source);
        Assert.Null(draft.Suggestion.SourceReferenceId);
        Assert.Equal("suggestion", draft.Suggestion.OutputAuthority);
        Assert.Equal(draft.Confidence, draft.Suggestion.Confidence);
        Assert.Empty(draft.Suggestion.Ambiguities);
        Assert.Empty(draft.Suggestion.MissingFields);
        Assert.Equal(
            "Review the suggested transaction before confirming.",
            draft.Suggestion.ReviewMessage);
    }

    [Fact]
    public async Task GatewayForwardedIntakePath_UsesTheSameDraftContract()
    {
        using var request = CreateRequest(
            "synthetic-intake-gateway-path-0001",
            "Received 50 USD salary today",
            route: TransactionIntakeApiRoutes.GatewayIntake);

        var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal("income", draft.Type);
        Assert.Equal("income.salary", draft.CategoryId);
    }

    [Fact]
    public async Task Intake_ReplaysStoredDraftForSameUserKeyAndNormalizedInput()
    {
        const string key = "synthetic-intake-replay-0001";
        using var firstRequest = CreateRequest(key, "Paid 20 USD for taxi today");
        using var secondRequest = CreateRequest(key, "  Paid   20 USD for taxi today  ");

        var firstResponse = await client.SendAsync(firstRequest);
        var first = await firstResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        var secondResponse = await client.SendAsync(secondRequest);
        var second = await secondResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.CreatedAtUtc, second.CreatedAtUtc);
    }

    [Fact]
    public async Task Intake_RejectsIdempotencyKeyReuseForDifferentInput()
    {
        const string key = "synthetic-intake-conflict-0001";
        using var firstRequest = CreateRequest(key, "Spent $10 on food today");
        using var secondRequest = CreateRequest(key, "Received $10 salary today");

        var firstResponse = await client.SendAsync(firstRequest);
        var secondResponse = await client.SendAsync(secondRequest);
        var problem = await secondResponse.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("idempotency_key_conflict", problem.Code);
    }

    [Fact]
    public async Task Intake_RequiresOpaqueIdempotencyKey()
    {
        using var request = CreateRequest(null, "Spent $10 on food today");

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("idempotency_key_required", problem.Code);
    }

    [Fact]
    public async Task Intake_WithForgedUserHeaderButNoGatewayAuthentication_IsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TransactionIntakeApiRoutes.Intake)
        {
            Content = JsonContent.Create(new TransactionIntakeRequest("Spent $10 on food today"))
        };
        request.Headers.TryAddWithoutValidation(TransactionIntakeHeaders.GatewayUserId, "synthetic-forged-user");
        request.Headers.TryAddWithoutValidation(TransactionIntakeHeaders.IdempotencyKey, "synthetic-forged-key");

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("trusted_gateway_authentication_required", problem.Code);
    }

    [Fact]
    public async Task Intake_UnknownInputReturnsLowConfidenceDraftWithAmbiguities()
    {
        using var request = CreateRequest(
            "synthetic-intake-unknown-0001",
            "Maybe something happened");

        var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal("unknown", draft.Type);
        Assert.Null(draft.Amount);
        Assert.Null(draft.Currency);
        Assert.True(draft.RequiresReview);
        Assert.Contains("type", draft.Ambiguities);
        Assert.Contains("low_confidence", draft.Ambiguities);
        Assert.Equal("ai_natural_language", draft.Suggestion.Source);
        Assert.Equal(draft.Ambiguities, draft.Suggestion.Ambiguities);
        Assert.Contains("type", draft.Suggestion.MissingFields);
        Assert.Contains("amount", draft.Suggestion.MissingFields);
        Assert.Contains("currency", draft.Suggestion.MissingFields);
        Assert.Contains("category", draft.Suggestion.MissingFields);
        Assert.Contains("date", draft.Suggestion.MissingFields);
        Assert.StartsWith("Confidence is low.", draft.Suggestion.ReviewMessage);
    }

    [Theory]
    [InlineData("Received $2500 salary today", "income", "income.salary")]
    [InlineData("Transferred 100 USD today", "transfer", null)]
    public async Task Intake_DetectsSupportedTransactionTypes(
        string input,
        string expectedType,
        string? expectedCategoryId)
    {
        using var request = CreateRequest($"synthetic-intake-type-{expectedType}", input);

        var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal(expectedType, draft.Type);
        Assert.Equal(expectedCategoryId, draft.CategoryId);
    }

    [Fact]
    public async Task Intake_IdempotencyKeyIsScopedToGatewayUser()
    {
        const string key = "synthetic-intake-user-scope-0001";
        using var firstRequest = CreateRequest(key, "Spent $10 on food today", "synthetic-user-one");
        using var secondRequest = CreateRequest(key, "Spent $10 on food today", "synthetic-user-two");

        var firstResponse = await client.SendAsync(firstRequest);
        var first = await firstResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        var secondResponse = await client.SendAsync(secondRequest);
        var second = await secondResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Intake_ConcurrentRetriesReturnOneStoredDraft()
    {
        const string key = "synthetic-intake-concurrent-0001";
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var request = CreateRequest(key, "Paid 15 USD for coffee today");
            using var response = await client.SendAsync(request);
            var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();
            Assert.NotNull(draft);
            return (response.StatusCode, draft.Id);
        });

        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.StatusCode == HttpStatusCode.Created);
        Assert.All(
            results,
            result => Assert.True(
                result.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"Unexpected status code: {result.StatusCode}."));
        Assert.Single(results.Select(result => result.Id).Distinct(StringComparer.Ordinal));
    }

    private static HttpRequestMessage CreateRequest(
        string? idempotencyKey,
        string input,
        string userId = "synthetic-intake-user",
        string route = TransactionIntakeApiRoutes.Intake)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new TransactionIntakeRequest(input))
        };
        request.Headers.TryAddWithoutValidation(
            TransactionIntakeHeaders.GatewayAuthentication,
            TransactionIntakeWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(
            TransactionIntakeHeaders.GatewayUserId,
            userId);
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation(
                TransactionIntakeHeaders.IdempotencyKey,
                idempotencyKey);
        }

        return request;
    }
}
