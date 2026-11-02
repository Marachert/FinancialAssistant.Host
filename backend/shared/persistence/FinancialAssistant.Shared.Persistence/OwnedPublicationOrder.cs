using System.Globalization;
using System.Text.Json.Serialization;

namespace FinancialAssistant.Shared.Persistence;

public static class OwnedPublicationOrder
{
    // Call only inside the same owner-locked transaction that creates the immutable outbox intent.
    public static async Task<long> AllocateAsync(OwnedRecordTransaction records, string stream, string owner, string publicationId)
    {
        ArgumentNullException.ThrowIfNull(records);
        Validate(stream, owner, publicationId);
        var id = stream + ":" + publicationId;
        if (await records.GetAsync("publication_order", owner, id) is not null)
        { return await ReadRequiredAsync(records, stream, owner, publicationId); }
        var head = await records.GetAsync("publication_order_head", owner, stream);
        var previous = head is null ? 0 : Number(head, immutable: false);
        if (previous >= long.MaxValue - 1) { throw new StorageUnavailableException(); }
        var next = previous + 1;
        var ordinal = stream + ":" + next.ToString(CultureInfo.InvariantCulture);
        if (await records.GetAsync("publication_order_index", owner, ordinal) is not null) { throw new StorageConflictException(); }
        if (!await records.InsertAsync("publication_order", owner, id, OwnedRecordCodec.Encode(1, new Sequence(next))) ||
            !await records.InsertAsync("publication_order_index", owner, ordinal, OwnedRecordCodec.Encode(1, new Publication(publicationId))))
        { throw new StorageConflictException(); }
        var encoded = OwnedRecordCodec.Encode(1, new Sequence(next));
        var saved = head is null ? await records.InsertAsync("publication_order_head", owner, stream, encoded) :
            await records.ReplaceAsync("publication_order_head", owner, stream, head.Revision, encoded);
        if (!saved) { throw new StorageConflictException(); }
        return next;
    }

    public static async Task<long> ReadRequiredAsync(OwnedRecordTransaction records, string stream, string owner, string publicationId)
    {
        ArgumentNullException.ThrowIfNull(records);
        Validate(stream, owner, publicationId);
        var durable = await records.GetAsync("publication_order", owner, stream + ":" + publicationId) ?? throw new StorageUnavailableException();
        var number = Number(durable, immutable: true);
        var index = await records.GetAsync("publication_order_index", owner, stream + ":" + number.ToString(CultureInfo.InvariantCulture))
            ?? throw new StorageUnavailableException();
        if (index.Revision != 0 || OwnedRecordCodec.Decode<Publication>(1, index).Id != publicationId) { throw new StorageUnavailableException(); }
        return number;
    }

    private static long Number(DurableRecord record, bool immutable)
    {
        var value = OwnedRecordCodec.Decode<Sequence>(1, record).Value;
        if (value is <= 0 or long.MaxValue || record.Revision != (immutable ? 0 : value - 1)) { throw new StorageUnavailableException(); }
        return value;
    }

    private static void Validate(string stream, string owner, string id)
    {
        if (stream is not { Length: >= 1 and <= 40 } || stream.Any(c => c is not (>= 'a' and <= 'z' or '-')) ||
            owner is not { Length: 64 } || owner.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
            id is not { Length: >= 1 and <= 200 } || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid owned publication ordering identity."); }
    }

    private sealed record Sequence([property: JsonRequired] long Value);
    private sealed record Publication([property: JsonRequired] string Id);
}
