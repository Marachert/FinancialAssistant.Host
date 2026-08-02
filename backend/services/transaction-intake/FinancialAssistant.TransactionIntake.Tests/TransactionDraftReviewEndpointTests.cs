using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionDraftReviewEndpointTests : IClassFixture<TransactionIntakeWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly InMemoryIncomeRecordStore incomeStore;
    private readonly InMemoryExpenseRecordStore expenseStore;
    private readonly InMemoryTransactionConfirmedPublisher publisher;

    public TransactionDraftReviewEndpointTests(TransactionIntakeWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        incomeStore = factory.Services.GetRequiredService<InMemoryIncomeRecordStore>();
        expenseStore = factory.Services.GetRequiredService<InMemoryExpenseRecordStore>();
        publisher = factory.Services.GetRequiredService<InMemoryTransactionConfirmedPublisher>();
    }

    [Fact]
    public async Task Review_ReturnsOnlyTheOwningUsersDraft()
    {
        const string userId = "synthetic-review-owner";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-review-owner-intake",
            "Maybe something happened");

        using var ownerRequest = CreateRequest(HttpMethod.Get, TransactionIntakeApiRoutes.ReviewDraft, userId, draft.Id);
        using var ownerResponse = await client.SendAsync(ownerRequest);
        var reviewed = await ownerResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.NotNull(reviewed);
        Assert.Equal("draft", reviewed.Status);
        Assert.Equal(draft.Id, reviewed.Id);

        using var otherRequest = CreateRequest(
            HttpMethod.Get,
            TransactionIntakeApiRoutes.ReviewDraft,
            "synthetic-review-other-user",
            draft.Id);
        using var otherResponse = await client.SendAsync(otherRequest);
        var problem = await otherResponse.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("transaction_draft_not_found", problem.Code);
    }

    [Fact]
    public async Task Update_CompleteReviewedValuesCanBeConfirmedExactlyOnce()
    {
        const string userId = "synthetic-review-update-owner";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-review-update-intake",
            "Maybe something happened");
        var update = new TransactionDraftUpdateRequest(
            draft.Revision,
            "expense",
            42.15m,
            "usd",
            "expense.food",
            "Synthetic Cafe",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "  reviewed   by user  ");

        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            TransactionIntakeApiRoutes.ReviewDraft,
            userId,
            draft.Id,
            update);
        using var updateResponse = await client.SendAsync(updateRequest);
        var reviewed = await updateResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(reviewed);
        Assert.False(reviewed.RequiresReview);
        Assert.Equal(draft.Revision + 1, reviewed.Revision);
        Assert.Empty(reviewed.Ambiguities);
        Assert.Equal("USD", reviewed.Currency);
        Assert.Equal("reviewed by user", reviewed.Note);

        using var firstConfirm = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.ConfirmDraft,
            userId,
            draft.Id);
        using var firstResponse = await client.SendAsync(firstConfirm);
        var first = await firstResponse.Content.ReadFromJsonAsync<ConfirmedTransactionResponse>();
        using var replayConfirm = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.ConfirmDraft,
            userId,
            draft.Id);
        using var replayResponse = await client.SendAsync(replayConfirm);
        var replay = await replayResponse.Content.ReadFromJsonAsync<ConfirmedTransactionResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(first.TransactionId, replay.TransactionId);
        var expense = Assert.Single(expenseStore.Records, item => item.SourceDraftId == draft.Id);
        Assert.Equal(42.15m, expense.Amount);
        Assert.Single(publisher.PublishedEvents, item => item.DraftId == draft.Id);

        using var finalReview = CreateRequest(
            HttpMethod.Get,
            TransactionIntakeApiRoutes.ReviewDraft,
            userId,
            draft.Id);
        using var finalReviewResponse = await client.SendAsync(finalReview);
        var confirmedDraft = await finalReviewResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.NotNull(confirmedDraft);
        Assert.Equal("confirmed", confirmedDraft.Status);
    }

    [Fact]
    public async Task Update_InvalidReviewedValuesRemainUnconfirmable()
    {
        const string userId = "synthetic-review-invalid-owner";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-review-invalid-intake",
            "Received 100 USD salary today");
        var update = new TransactionDraftUpdateRequest(
            draft.Revision,
            "expense",
            null,
            "USD",
            "income.salary",
            null,
            DateOnly.FromDateTime(DateTime.UtcNow));

        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            TransactionIntakeApiRoutes.GatewayReviewDraft,
            userId,
            draft.Id,
            update);
        using var updateResponse = await client.SendAsync(updateRequest);
        var reviewed = await updateResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(reviewed);
        Assert.True(reviewed.RequiresReview);
        Assert.Contains("amount", reviewed.Ambiguities);
        Assert.Contains("category", reviewed.Ambiguities);

        using var confirmRequest = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.ConfirmDraft,
            userId,
            draft.Id);
        using var confirmResponse = await client.SendAsync(confirmRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);
        Assert.DoesNotContain(publisher.PublishedEvents, item => item.DraftId == draft.Id);
    }

    [Fact]
    public async Task Update_StaleExpectedRevisionReturnsConflictAndPreservesAcceptedValues()
    {
        const string userId = "synthetic-review-stale-owner";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-review-stale-intake",
            "Maybe something happened");
        var acceptedUpdate = new TransactionDraftUpdateRequest(
            draft.Revision,
            "expense",
            25.50m,
            "USD",
            "expense.groceries",
            "Synthetic Market",
            DateOnly.FromDateTime(DateTime.UtcNow));

        using var acceptedRequest = CreateRequest(
            HttpMethod.Put,
            TransactionIntakeApiRoutes.ReviewDraft,
            userId,
            draft.Id,
            acceptedUpdate);
        using var acceptedResponse = await client.SendAsync(acceptedRequest);
        var accepted = await acceptedResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        Assert.NotNull(accepted);

        var staleUpdate = acceptedUpdate with { Amount = 99.99m };
        using var staleRequest = CreateRequest(
            HttpMethod.Put,
            TransactionIntakeApiRoutes.ReviewDraft,
            userId,
            draft.Id,
            staleUpdate);
        using var staleResponse = await client.SendAsync(staleRequest);
        var conflict = await staleResponse.Content.ReadFromJsonAsync<TransactionIntakeErrorResponse>();
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.NotNull(conflict);
        Assert.Equal("transaction_draft_not_editable", conflict.Code);

        using var reviewRequest = CreateRequest(
            HttpMethod.Get,
            TransactionIntakeApiRoutes.ReviewDraft,
            userId,
            draft.Id);
        using var reviewResponse = await client.SendAsync(reviewRequest);
        var current = await reviewResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.NotNull(current);
        Assert.Equal(25.50m, current.Amount);
        Assert.Equal(accepted.Revision, current.Revision);
    }

    [Fact]
    public async Task Reject_IsIdempotentAndNeverChangesFinancialTotals()
    {
        const string userId = "synthetic-review-reject-owner";
        var draft = await CreateDraftAsync(
            userId,
            "synthetic-review-reject-intake",
            "Spent 18 USD at Coffee Shop today");

        using var rejectRequest = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.RejectDraft,
            userId,
            draft.Id);
        using var rejectResponse = await client.SendAsync(rejectRequest);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        Assert.NotNull(rejected);
        Assert.Equal("rejected", rejected.Status);

        using var replayRequest = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.GatewayRejectDraft,
            userId,
            draft.Id);
        using var replayResponse = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);

        using var confirmRequest = CreateRequest(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.ConfirmDraft,
            userId,
            draft.Id);
        using var confirmResponse = await client.SendAsync(confirmRequest);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirmResponse.StatusCode);
        Assert.DoesNotContain(incomeStore.Records, item => item.SourceDraftId == draft.Id);
        Assert.DoesNotContain(expenseStore.Records, item => item.SourceDraftId == draft.Id);
        Assert.DoesNotContain(publisher.PublishedEvents, item => item.DraftId == draft.Id);
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
        using var response = await client.SendAsync(request);
        var draft = await response.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(draft);
        return draft;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string route,
        string userId,
        string draftId,
        object? body = null)
    {
        var request = new HttpRequestMessage(
            method,
            route.Replace("{draftId}", draftId, StringComparison.Ordinal));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        AddTrustedHeaders(request, userId);
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
