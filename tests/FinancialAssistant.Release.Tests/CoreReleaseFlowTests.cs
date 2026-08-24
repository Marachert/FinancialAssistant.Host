using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Analytics.Tests;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Contracts;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.FinancialScore.Tests;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Tests;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.Release.Tests;

public sealed class CoreReleaseFlowTests
{
    [Fact]
    public async Task SyntheticUser_CompletesRegisterLoginInputConfirmDashboardAndScore()
    {
        using var identityFactory = new IdentityContractWebApplicationFactory();
        using var intakeFactory = new TransactionIntakeWebApplicationFactory();
        using var analyticsFactory = new AnalyticsWebApplicationFactory();
        using var scoreFactory = new FinancialScoreWebApplicationFactory();
        using var identityClient = identityFactory.CreateClient();
        using var intakeClient = intakeFactory.CreateClient();
        using var analyticsClient = analyticsFactory.CreateClient();
        using var scoreClient = scoreFactory.CreateClient();

        var email = $"release-{Guid.NewGuid():N}@example.invalid";
        const string password = "Synthetic-Release-Password-123!";
        var clientContext = new IdentityClientContext(
            $"release-client-{Guid.NewGuid():N}",
            "web",
            "0.0-synthetic");
        using var registration = await identityClient.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(email, password, clientContext));
        var registered = await registration.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.NotNull(registered);

        using var signIn = await identityClient.PostAsJsonAsync(
            IdentityApiRoutes.SignIn,
            new SignInRequest(email, password, clientContext));
        var session = await signIn.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.NotNull(session);
        Assert.Equal(registered.User.UserId, session.User.UserId);

        var userId = session.User.UserId;
        using var intake = Request(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.Intake,
            userId,
            new TransactionIntakeRequest("Something happened"));
        intake.Headers.Add(
            TransactionIntakeHeaders.IdempotencyKey,
            $"release-intake-{Guid.NewGuid():N}");
        using var intakeResponse = await intakeClient.SendAsync(intake);
        var draft = await intakeResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.Created, intakeResponse.StatusCode);
        Assert.NotNull(draft);

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        using var update = Request(
            HttpMethod.Put,
            TransactionIntakeApiRoutes.ReviewDraft.Replace("{draftId}", draft.Id),
            userId,
            new TransactionDraftUpdateRequest(
                draft.Revision,
                "expense",
                42.15m,
                "USD",
                "expense.food",
                "Synthetic Cafe",
                date,
                "release test"));
        using var updateResponse = await intakeClient.SendAsync(update);
        var reviewed = await updateResponse.Content.ReadFromJsonAsync<TransactionDraftResponse>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(reviewed);
        Assert.False(reviewed.RequiresReview);

        using var confirm = Request(
            HttpMethod.Post,
            TransactionIntakeApiRoutes.ConfirmDraft.Replace("{draftId}", draft.Id),
            userId);
        using var confirmResponse = await intakeClient.SendAsync(confirm);
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<ConfirmedTransactionResponse>();
        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);
        Assert.NotNull(confirmed);

        var expenseStore =
            intakeFactory.Services.GetRequiredService<InMemoryExpenseRecordStore>();
        var record = Assert.Single(
            expenseStore.Records,
            item => item.TransactionId == confirmed.TransactionId);
        var changedAt = record.UpdatedAtUtc ?? record.ConfirmedAtUtc;
        var payload = new FinancialRecordChangedV1(
            record.TransactionId,
            record.Amount,
            record.Currency,
            record.CategoryId,
            record.Date,
            record.Status,
            record.Revision,
            record.Origin,
            changedAt);

        using (var scope = analyticsFactory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AnalyticsProjector>().ApplyAsync(
                Envelope(payload, AnalyticsOwnerHasher.Hash(userId), "analytics"),
                CancellationToken.None);
        }

        using (var scope = scoreFactory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FinancialScoreService>().ApplyAsync(
                Envelope(payload, FinancialScoreOwnerHasher.Hash(userId), "score"),
                semanticFactors: null,
                CancellationToken.None);
        }

        using var dashboardRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.GatewayDashboard}?currency=USD&timeZoneId=UTC&referenceDate={date:yyyy-MM-dd}&trendDays=7");
        dashboardRequest.Headers.Add(
            AnalyticsGatewayHeaders.Authentication,
            AnalyticsWebApplicationFactory.GatewaySecret);
        dashboardRequest.Headers.Add(AnalyticsGatewayHeaders.UserId, userId);
        using var dashboardResponse = await analyticsClient.SendAsync(dashboardRequest);
        var dashboard =
            await dashboardResponse.Content.ReadFromJsonAsync<AnalyticsDashboardResponse>();
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(42.15m, dashboard.DailySummary.ExpenseTotal);

        using var scoreRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.GatewayCurrent}?currency=USD");
        scoreRequest.Headers.Add(
            FinancialScoreGatewayHeaders.Authentication,
            FinancialScoreWebApplicationFactory.GatewaySecret);
        scoreRequest.Headers.Add(FinancialScoreGatewayHeaders.UserId, userId);
        using var scoreResponse = await scoreClient.SendAsync(scoreRequest);
        var score = await scoreResponse.Content.ReadFromJsonAsync<FinancialScoreResponse>();
        Assert.Equal(HttpStatusCode.OK, scoreResponse.StatusCode);
        Assert.NotNull(score);
        Assert.Equal("USD", score.Currency);
        Assert.Equal(5, score.Factors.Count);

        var combinedBodies = string.Concat(
            await registration.Content.ReadAsStringAsync(),
            await signIn.Content.ReadAsStringAsync(),
            await intakeResponse.Content.ReadAsStringAsync(),
            await confirmResponse.Content.ReadAsStringAsync(),
            await dashboardResponse.Content.ReadAsStringAsync(),
            await scoreResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(email, combinedBodies, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, combinedBodies, StringComparison.Ordinal);
    }

    private static HttpRequestMessage Request(
        HttpMethod method,
        string route,
        string userId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.Add(
            TransactionIntakeHeaders.GatewayAuthentication,
            TransactionIntakeWebApplicationFactory.GatewaySecret);
        request.Headers.Add(TransactionIntakeHeaders.GatewayUserId, userId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Envelope(
        FinancialRecordChangedV1 payload,
        string userIdHash,
        string consumer) =>
        new(
            $"release-{consumer}-event-{Guid.NewGuid():N}",
            $"release-{consumer}-occurrence-{Guid.NewGuid():N}",
            FinancialRecordEventTypes.ExpenseCreated,
            payload.ChangedAtUtc,
            "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "release-flow-correlation",
            "release-confirmation-causation",
            userIdHash,
            payload);
}
