using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialAssistant.Shared.Persistence;

public static class OwnedRecordCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static byte[] Encode<T>(int version, T value)
    {
        if (version < 1 || value is null) { throw new ArgumentException("Invalid owned record version or value."); }
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope<T>(version, value), Json);
    }

    public static T Decode<T>(int version, DurableRecord record)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(record.Payload, Json);
            if (version < 1 || envelope is null || envelope.Version != version || envelope.Value is null)
            { throw new StorageUnavailableException(); }
            return envelope.Value;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        { throw new StorageUnavailableException(); }
    }

    private sealed record Envelope<T>(int Version, [property: JsonRequired] T Value);
}
