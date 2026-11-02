using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.Profile.Contracts;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class NativeProfileSettingsMappingTests
{
    [Theory]
    [InlineData("USD", 75, 75)]
    [InlineData("USD", 0, 0)]
    [InlineData("EUR", 75, 0)]
    public void Mapping_never_converts_a_known_budget_to_another_currency(string currency, int budget, int expected)
    {
        var source = Snapshot(budget);
        var value = NativeScoreProfileSettingsLoader.CreateSnapshot(source, currency);
        Assert.Equal((decimal)expected, value.Settings.MonthlyBudgetAmount);
        Assert.True(value.Settings.ProfileOnboardingCompleted);
        Assert.False(value.Settings.PreferencesOnboardingCompleted);
        Assert.Equal(source.OwnerHash, value.Owner);
        Assert.Equal(source.SourceVersion, value.SourceVersion);
        Assert.Equal(source.SourceRevision, value.SourceRevision);
        Assert.Equal(source.ObservedAtUtc, value.ObservedAtUtc);
    }

    [Fact]
    public void Mapping_does_not_round_decimal_input()
    {
        var value = NativeScoreProfileSettingsLoader.CreateSnapshot(Snapshot(123456789.12345678m), "USD");
        Assert.Equal(123456789.12345678m, value.Settings.MonthlyBudgetAmount);
    }

    private static ProfileFinancialSettingsSnapshot Snapshot(decimal budget) => new(new string('a', 64), "USD", budget,
        "strict", false, false, false, true, false, "profile-7", 7, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
}
