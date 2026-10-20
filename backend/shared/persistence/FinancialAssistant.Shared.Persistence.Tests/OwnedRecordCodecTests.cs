using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using Xunit;

namespace FinancialAssistant.Shared.Persistence.Tests;

public sealed class OwnedRecordCodecTests
{
    [Fact]
    public void CanonicalDecimalRemovesRepresentationScaleWithoutFloatingPointOrRounding()
    {
        foreach (var value in new[] { decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m,
            12345678901234567890.12345678m, 12.340100m, -0.0001m, 0m })
        { Assert.Equal(value, OwnedRecordCodec.CanonicalDecimal(value)); }
        Assert.Equal(decimal.GetBits(1m), decimal.GetBits(OwnedRecordCodec.CanonicalDecimal(1.00m)));
        Assert.Equal(decimal.GetBits(12.3401m), decimal.GetBits(OwnedRecordCodec.CanonicalDecimal(12.340100m)));
    }

    [Fact]
    public void TypedVersionedRoundTripPreservesDecimalExactly()
    {
        var value = new SyntheticState(12345678901234567890.12345678m, true);
        var encoded = OwnedRecordCodec.Encode(1, value);
        Assert.Equal(value, OwnedRecordCodec.Decode<SyntheticState>(1, new DurableRecord(0, encoded)));
        Assert.Throws<StorageUnavailableException>(() => OwnedRecordCodec.Decode<SyntheticState>(2, new DurableRecord(0, encoded)));
    }

    [Theory]
    [InlineData("{\"version\":1}")]
    [InlineData("{\"version\":1,\"value\":{\"flag\":true}}")]
    [InlineData("{\"version\":1,\"value\":{\"amount\":1,\"flag\":true,\"unknown\":0}}")]
    public void MissingOrUnknownStateFieldsFailClosed(string json)
    {
        var exception = Assert.Throws<StorageUnavailableException>(() => OwnedRecordCodec.Decode<SyntheticState>(1,
            new DurableRecord(0, Encoding.UTF8.GetBytes(json))));
        Assert.Null(exception.InnerException);
    }

    private sealed record SyntheticState([property: JsonRequired] decimal Amount, [property: JsonRequired] bool Flag);
}
