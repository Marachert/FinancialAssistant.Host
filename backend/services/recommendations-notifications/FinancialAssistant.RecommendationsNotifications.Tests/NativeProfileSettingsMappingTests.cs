using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class NativeProfileSettingsMappingTests
{
    [Theory]
    [InlineData("USD", 75, 75)]
    [InlineData("USD", 0, null)]
    [InlineData("EUR", 75, null)]
    public void Mapping_preserves_known_monthly_currency_budget_without_inference(string currency, int budget, int? expected)
    {
        var source = Snapshot(budget);
        var value = NativeInsightProfileSettingsLoader.CreateSnapshot(source, currency);
        Assert.Equal(expected is null ? null : (decimal?)expected.Value, value.MonthlyBudget);
        Assert.False(value.Complete);
        Assert.Equal(source.OwnerHash, value.Owner); Assert.Equal(source.SourceVersion, value.SourceVersion);
        Assert.Equal(source.SourceRevision, value.SourceRevision); Assert.Equal(source.ObservedAtUtc, value.ObservedAtUtc);
    }

    [Fact]
    public void Mapping_requires_both_onboarding_flags_and_preserves_decimal_precision()
    {
        var source = Snapshot(123456789.12345678m) with { PreferencesOnboardingCompleted = true };
        var value = NativeInsightProfileSettingsLoader.CreateSnapshot(source, "USD");
        Assert.True(value.Complete); Assert.Equal(123456789.12345678m, value.MonthlyBudget);
    }

    private static ProfileFinancialSettingsSnapshot Snapshot(decimal budget) => new(new string('a', 64), "USD", budget,
        "strict", false, false, false, true, false, "profile-7", 7, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
}
