using System.Text.Json;
using FinancialAssistant.FinancialSummary.Application;
using FinancialAssistant.FinancialSummary.Contracts;
using FinancialAssistant.FinancialSummary.Domain;
using Xunit;

namespace FinancialAssistant.FinancialSummary.Tests;

public sealed class FinancialSummaryContractTests
{
    [Fact]
    public void ResponseMapper_ExposesStableDashboardShapeWithoutProjectionInternals()
    {
        var referenceDate = new DateOnly(2026, 8, 20);
        var readModel = new FinancialSummaryReadModel(
            new string('a', 64),
            "USD",
            referenceDate,
            new FinancialPeriodTotals(referenceDate, referenceDate, 100m, 40m),
            new FinancialPeriodTotals(referenceDate.AddDays(-3), referenceDate.AddDays(3), 200m, 75m),
            new FinancialPeriodTotals(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 500m, 225m),
            275m,
            new[]
            {
                new FinancialCategoryTotals("expense.groceries", 0m, 80m)
            },
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero),
            IsStale: false);

        var response = FinancialSummaryResponseMapper.Map(readModel, "Europe/Kyiv");
        var json = JsonSerializer.Serialize(response);

        Assert.Equal("USD", response.Currency);
        Assert.Equal("Europe/Kyiv", response.TimeZoneId);
        Assert.Equal("daily", response.Daily.Period);
        Assert.Equal(60m, response.Daily.BalanceDelta);
        Assert.Equal(275m, response.Monthly.BalanceDelta);
        Assert.Equal(275m, response.BalanceDelta);
        Assert.Single(response.CategoryBreakdown);
        Assert.False(response.Freshness.IsStale);
        Assert.DoesNotContain("UserIdHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EventId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Origin", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyReadModel_MapsToZeroSafeResponse()
    {
        var referenceDate = new DateOnly(2026, 8, 20);
        var zero = new FinancialPeriodTotals(referenceDate, referenceDate, 0m, 0m);
        var readModel = new FinancialSummaryReadModel(
            new string('b', 64),
            "EUR",
            referenceDate,
            zero,
            zero,
            zero,
            0m,
            Array.Empty<FinancialCategoryTotals>(),
            LastEventAtUtc: null,
            IsStale: true);

        var response = FinancialSummaryResponseMapper.Map(readModel, "UTC");

        Assert.Equal(0m, response.Daily.IncomeTotal);
        Assert.Equal(0m, response.Weekly.ExpenseTotal);
        Assert.Equal(0m, response.Monthly.BalanceDelta);
        Assert.Empty(response.CategoryBreakdown);
        Assert.True(response.Freshness.IsStale);
        Assert.Null(response.Freshness.LastEventAtUtc);
    }

    [Fact]
    public void QueryAndRoutes_DefineOneExplicitPeriodConvention()
    {
        var query = new FinancialSummaryQuery(
            "USD",
            "Europe/Kyiv",
            new DateOnly(2026, 8, 20));

        Assert.Equal("/api/v1/financial-summary", FinancialSummaryApiRoutes.Summary);
        Assert.Equal("/financial-summary", FinancialSummaryApiRoutes.GatewaySummary);
        Assert.Equal("USD", query.Currency);
        Assert.Equal("Europe/Kyiv", query.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 8, 20), query.ReferenceDate);
    }
}
