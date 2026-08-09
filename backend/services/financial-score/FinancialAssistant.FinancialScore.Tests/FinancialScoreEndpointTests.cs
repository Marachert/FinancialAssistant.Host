using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Contracts;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreEndpointTests :
    IClassFixture<FinancialScoreWebApplicationFactory>
{
    private readonly FinancialScoreWebApplicationFactory factory;
    private readonly HttpClient client;

    public FinancialScoreEndpointTests(FinancialScoreWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CurrentAndHistory_ReturnOwnerScopedTransparentScores()
    {
        const string userId = "synthetic-score-owner";
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<FinancialScoreService>();
            await service.ApplyAsync(
                FinancialScoreServiceTests.CreateEvent(
                    "income-api",
                    0,
                    FinancialRecordEventTypes.IncomeCreated,
                    500m,
                    FinancialScoreOwnerHasher.Hash(userId)),
                null,
                CancellationToken.None);
            await service.ApplyAsync(
                FinancialScoreServiceTests.CreateEvent(
                    "expense-api",
                    0,
                    FinancialRecordEventTypes.ExpenseCreated,
                    100m,
                    FinancialScoreOwnerHasher.Hash(userId)),
                null,
                CancellationToken.None);
        }

        using var currentRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.GatewayCurrent}?currency=usd");
        AddTrustedHeaders(currentRequest, userId);
        using var currentResponse = await client.SendAsync(currentRequest);
        var current = await currentResponse.Content.ReadFromJsonAsync<FinancialScoreResponse>();

        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.NotNull(current);
        Assert.Equal("USD", current.Currency);
        Assert.Equal(FinancialScoreFormula.Version, current.FormulaVersion);
        Assert.Equal(5, current.Factors.Count);
        Assert.All(current.Factors, factor => Assert.NotNull(factor.Inputs));

        var periodStart = new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 8, 20, 13, 0, 0, TimeSpan.Zero);
        using var historyRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.GatewayHistory}?currency=USD&limit=1&fromUtc={Uri.EscapeDataString(periodStart.ToString("O"))}&toUtc={Uri.EscapeDataString(periodEnd.ToString("O"))}");
        AddTrustedHeaders(historyRequest, userId);
        using var historyResponse = await client.SendAsync(historyRequest);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<FinancialScoreHistoryResponse>();

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history);
        Assert.Single(history.Items);
        Assert.Equal(periodStart, history.FromUtc);
        Assert.Equal(periodEnd, history.ToUtc);
        Assert.True(history.HasMore);
        Assert.NotNull(history.NextBeforeUtc);
        Assert.False(string.IsNullOrWhiteSpace(history.NextBeforeCalculationId));

        using var nextHistoryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.GatewayHistory}?currency=USD&limit=1&fromUtc={Uri.EscapeDataString(periodStart.ToString("O"))}&toUtc={Uri.EscapeDataString(periodEnd.ToString("O"))}&beforeUtc={Uri.EscapeDataString(history.NextBeforeUtc.Value.ToString("O"))}&beforeCalculationId={Uri.EscapeDataString(history.NextBeforeCalculationId)}");
        AddTrustedHeaders(nextHistoryRequest, userId);
        using var nextHistoryResponse = await client.SendAsync(nextHistoryRequest);
        var nextHistory = await nextHistoryResponse.Content
            .ReadFromJsonAsync<FinancialScoreHistoryResponse>();
        Assert.Equal(HttpStatusCode.OK, nextHistoryResponse.StatusCode);
        Assert.NotNull(nextHistory);
        Assert.Single(nextHistory.Items);
        Assert.NotEqual(history.Items[0].CalculationId, nextHistory.Items[0].CalculationId);
    }

    [Fact]
    public async Task Endpoints_RequireTrustedGatewayAndValidateQueries()
    {
        using var unauthorized = await client.GetAsync(
            $"{FinancialScoreApiRoutes.Current}?currency=USD");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var missingCurrencyRequest = new HttpRequestMessage(
            HttpMethod.Get,
            FinancialScoreApiRoutes.Current);
        AddTrustedHeaders(missingCurrencyRequest, "synthetic-missing-currency-owner");
        using var missingCurrency = await client.SendAsync(missingCurrencyRequest);
        var missingProblem = await missingCurrency.Content
            .ReadFromJsonAsync<FinancialScoreApiErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, missingCurrency.StatusCode);
        Assert.Equal("invalid_financial_score_request", missingProblem?.Code);

        using var invalidHistoryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.History}?currency=USD&limit=101&beforeUtc=not-a-date");
        AddTrustedHeaders(invalidHistoryRequest, "synthetic-invalid-history-owner");
        using var invalidHistory = await client.SendAsync(invalidHistoryRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidHistory.StatusCode);

        using var incompletePeriodRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.History}?currency=USD&fromUtc=2026-08-01T00:00:00Z");
        AddTrustedHeaders(incompletePeriodRequest, "synthetic-invalid-period-owner");
        using var incompletePeriod = await client.SendAsync(incompletePeriodRequest);
        Assert.Equal(HttpStatusCode.BadRequest, incompletePeriod.StatusCode);
    }

    [Fact]
    public async Task Current_PersistsNeutralDefaultUntilConfirmedEventArrives()
    {
        const string userId = "synthetic-empty-owner";
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.Current}?currency=EUR");
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var current = await response.Content.ReadFromJsonAsync<FinancialScoreResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(current);
        Assert.Equal(FinancialScoreFormula.NewUserDefault, current.Score);
        Assert.Contains(
            current.Factors.Single(item => item.Code == "penalty_cap").Inputs,
            item => item.Code == "new_user_default" && item.Value == 1m);

        using var historyRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{FinancialScoreApiRoutes.History}?currency=EUR");
        AddTrustedHeaders(historyRequest, userId);
        using var historyResponse = await client.SendAsync(historyRequest);
        var history = await historyResponse.Content
            .ReadFromJsonAsync<FinancialScoreHistoryResponse>();

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.NotNull(history);
        Assert.Single(history.Items);
        Assert.Equal(current.CalculationId, history.Items[0].CalculationId);
    }

    private static void AddTrustedHeaders(HttpRequestMessage request, string userId)
    {
        request.Headers.TryAddWithoutValidation(
            FinancialScoreGatewayHeaders.Authentication,
            FinancialScoreWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(FinancialScoreGatewayHeaders.UserId, userId);
    }
}
