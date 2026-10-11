using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using Xunit;

namespace FinancialAssistant.Shared.Persistence.Tests;

public sealed class OwnedRecordCodecTests
{
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
