using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionConfirmationEndpointTests : IClassFixture<TransactionIntakeWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly InMemoryIncomeRecordStore incomeStore;
    private readonly InMemoryExpenseRecordStore expenseStore;
    private readonly InMemoryTransactionConfirmedPublisher publisher;

    public TransactionConfirmationEndpointTests(TransactionIntakeWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        incomeStore = factory.Services.GetRequiredService<InMemoryIncomeRecordStore>();
        expenseStore = factory.Services.GetRequiredService<InMemoryExpenseRecordStore>();
        publisher = factory.Services.GetRequiredService<InMemoryTransactionConfirmedPublisher>();
    }

    [Fact]
    public async Task Confirm_IncomeDraftPublishesEventAndPersistsAuthoritativeIncome()
    {
        const string userId = "synthetic-confirm-income-user";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-confirm-income-intake",
            "Received $2500 salary today");

        using var request = CreateConfirmationRequest(userId, draft.Id);
        var response = await client.SendAsync(request);
        var confirmed = await response.Content.ReadFromJsonAsync<ConfirmedTransactionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(confirmed);
        Assert.Equal("confirmed", confirmed.Status);
        Assert.Equal("income", confirmed.TransactionType);
        Assert.Equal(2500m, confirmed.Amount);
        Assert.Equal("USD", confirmed.Currency);
        Assert.Equal("income.salary", confirmed.CategoryId);
        var record = Assert.Single(incomeStore.Records, item => item.TransactionId == confirmed.TransactionId);
        Assert.Equal(userId, record.UserId);
        Assert.Equal(draft.Id, record.SourceDraftId);
        Assert.DoesNotContain(expenseStore.Records, item => item.TransactionId == confirmed.TransactionId);
        var published = Assert.Single(publisher.PublishedEvents, item => item.DraftId == draft.Id);
        Assert.Equal(TransactionConfirmedIntegrationEvent.Name, published.EventType);
    }

    [Fact]
    public async Task GatewayConfirmPath_ExpenseDraftPersistsAuthoritativeExpense()
    {
        const string userId = "synthetic-confirm-expense-user";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-confirm-expense-intake",
            "Spent $18 at Coffee Shop today");

        using var request = CreateConfirmationRequest(
            userId,
            draft.Id,
            TransactionIntakeApiRoutes.GatewayConfirmDraft);
        var response = await client.SendAsync(request);
        var confirmed = await response.Content.ReadFromJsonAsync<ConfirmedTransactionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(confirmed);
        Assert.Equal("expense", confirmed.TransactionType);
        Assert.Equal("expense.food", confirmed.CategoryId);
        var record = Assert.Single(expenseStore.Records, item => item.TransactionId == confirmed.TransactionId);
        Assert.Equal("Coffee Shop", record.Merchant);
        Assert.DoesNotContain(incomeStore.Records, item => item.TransactionId == confirmed.TransactionId);
    }

    [Fact]
    public async Task Confirm_ConcurrentDuplicatesReturnOneTransactionAndPublishOnce()
    {
        const string userId = "synthetic-confirm-duplicate-user";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-confirm-duplicate-intake",
            "Paid 25 USD for taxi today");
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var request = CreateConfirmationRequest(userId, draft.Id);
            using var response = await client.SendAsync(request);
            var transaction = await response.Content.ReadFromJsonAsync<ConfirmedTransactionResponse>();
            Assert.NotNull(transaction);
            return (response.StatusCode, transaction.TransactionId);
        });

        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.StatusCode == HttpStatusCode.Created);
        Assert.All(
            results,
            result => Assert.True(result.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Single(results.Select(result => result.TransactionId).Distinct(StringComparer.Ordinal));
        Assert.Single(publisher.PublishedEvents, item => item.DraftId == draft.Id);
    }

    [Fact]
    public async Task Confirm_AmbiguousDraftIsRejectedWithoutPublishing()
    {
        const string userId = "synthetic-confirm-ambiguous-user";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-confirm-ambiguous-intake",
            "Maybe something happened");

        using var request = CreateConfirmationRequest(userId, draft.Id);
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("transaction_draft_not_confirmable", problem.Code);
        Assert.DoesNotContain(publisher.PublishedEvents, item => item.DraftId == draft.Id);
    }

    [Fact]
    public async Task Confirm_DraftOwnedByAnotherUserReturnsNotFound()
    {
        var draft = await CreateDraftAsync(
            "synthetic-confirm-owner",
            "synthetic-confirm-owner-intake",
            "Received 100 USD salary today");

        using var request = CreateConfirmationRequest("synthetic-confirm-other-user", draft.Id);
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("transaction_draft_not_found", problem.Code);
    }

    private async Task<TransactionDraftResponse> CreateDraftAsync(
        string userId,
        string idempotencyKey,
        string input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TransactionIntakeApiRoutes.Intake)
        {
            Content = JsonContent.Create(new TransactionIntakeRequest(input))
        };
        AddTrustedHeaders(request, userId);
        request.Headers.TryAddWithoutValidation(TransactionIntakeHeaders.IdempotencyKey, idempotencyKey);
        var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        return draft;
    }

    private static HttpRequestMessage CreateConfirmationRequest(
        string userId,
        string draftId,
        string route = TransactionIntakeApiRoutes.ConfirmDraft)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            route.Replace("{draftId}", draftId, StringComparison.Ordinal));
        AddTrustedHeaders(request, userId);
        request.Headers.TryAddWithoutValidation("correlationId", $"synthetic-confirm-{draftId}");
        return request;
    }

    private static void AddTrustedHeaders(HttpRequestMessage request, string userId)
    {
        request.Headers.TryAddWithoutValidation(
            TransactionIntakeHeaders.GatewayAuthentication,
            TransactionIntakeWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(TransactionIntakeHeaders.GatewayUserId, userId);
    }
}
