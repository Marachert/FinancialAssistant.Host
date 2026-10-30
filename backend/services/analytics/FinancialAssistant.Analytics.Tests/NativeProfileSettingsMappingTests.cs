using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Profile.Contracts;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class NativeProfileSettingsMappingTests
{
    [Theory]
    [InlineData("USD", 75, 75)]
    [InlineData("USD", 0, null)]
    [InlineData("EUR", 75, null)]
    public void Mapping_preserves_only_the_authorized_monthly_currency_budget(string currency, int budget, int? expected)
    {
        var source = Snapshot(budget);
        var value = NativeAnalyticsProfileSettingsLoader.CreateSnapshot(source, currency);
        Assert.Null(value.Limits.Daily);
        Assert.Null(value.Limits.Weekly);
        Assert.Equal(expected is null ? null : (decimal?)expected.Value, value.Limits.Monthly);
        Assert.Equal(source.OwnerHash, value.OwnerHash);
        Assert.Equal(source.SourceVersion, value.SourceSnapshotVersion);
        Assert.Equal(source.SourceRevision, value.SourceRevision);
        Assert.Equal(source.ObservedAtUtc, value.ObservedAtUtc);
    }

    [Fact]
    public void Mapping_does_not_round_decimal_input()
    {
        var value = NativeAnalyticsProfileSettingsLoader.CreateSnapshot(Snapshot(123456789.12345678m), "USD");
        Assert.Equal(123456789.12345678m, value.Limits.Monthly);
    }

    private static ProfileFinancialSettingsSnapshot Snapshot(decimal budget) => new(new string('a', 64), "USD", budget,
        "strict", false, false, false, true, false, "profile-7", 7, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
}
