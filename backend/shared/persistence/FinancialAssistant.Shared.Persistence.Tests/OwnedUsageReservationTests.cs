using Xunit;

namespace FinancialAssistant.Shared.Persistence.Tests;

public sealed class OwnedUsageReservationTests
{
    [Theory]
    [InlineData("owner\nforged", "provider", "request")]
    [InlineData("owner", "https://provider?credential", "request")]
    [InlineData("owner", "provider", "../request")]
    [InlineData("", "provider", "request")]
    public void UnsafeReservationIdentityIsRejectedBeforeSql(string owner, string provider, string request) =>
        Assert.Throws<ArgumentException>(() => OwnedUsageReservations.Validate(owner, provider, new DateOnly(2026, 10, 19), 1, request));

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void QuotaCannotBecomeUnbounded(int limit) => Assert.Throws<ArgumentException>(() =>
        OwnedUsageReservations.Validate("owner", "provider", new DateOnly(2026, 10, 19), limit, "request"));

    [Fact]
    public void UtcDayAndRequestIdentityAreMandatory()
    {
        Assert.Throws<ArgumentException>(() => OwnedUsageReservations.Validate("owner", "provider", default, 1, "request"));
        Assert.Throws<ArgumentException>(() => OwnedUsageReservations.Validate("owner", "provider", new DateOnly(2026, 10, 19), 1, ""));
        OwnedUsageReservations.Validate("owner", "provider", new DateOnly(2026, 10, 19), 10000, "request_v1");
    }
}
