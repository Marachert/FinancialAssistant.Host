using System.Globalization;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

internal sealed record NativeInsightOrder(string Stream, long Sequence);

internal static class NativeInsightOrderAdmission
{
    internal static async Task<bool> AdmitAsync(OwnedRecordTransaction records, InsightSource source)
    {
        var order = source.Order ?? throw new StorageUnavailableException();
        var stream = source.Pipeline + ":" + order.Stream + ":" + source.Currency;
        var key = stream + ":" + order.Sequence.ToString(CultureInfo.InvariantCulture);
        var expected = new Input(source.Id, source.Fingerprint, order.Sequence);
        var claim = await records.GetAsync("insight_order_sources", source.Owner, key);
        if (claim is not null)
        {
            if (claim.Revision != 0 || Read(claim) != expected) { throw new StorageConflictException(); }
            // A matching source receipt would already have replayed before admission.
            throw new StorageUnavailableException();
        }
        if (!await records.InsertAsync("insight_order_sources", source.Owner, key, OwnedRecordCodec.Encode(1, expected)))
        { throw new StorageConflictException(); }
        var head = await records.GetAsync("insight_order_heads", source.Owner, stream);
        if (head is not null)
        {
            var current = Read(head);
            var previous = await records.GetAsync("insight_order_sources", source.Owner,
                stream + ":" + current.Sequence.ToString(CultureInfo.InvariantCulture)) ?? throw new StorageUnavailableException();
            if (previous.Revision != 0 || Read(previous) != current) { throw new StorageUnavailableException(); }
            if (order.Sequence < current.Sequence) { return false; }
            if (order.Sequence == current.Sequence) { throw new StorageConflictException(); }
        }
        var value = OwnedRecordCodec.Encode(1, expected);
        var saved = head is null ? await records.InsertAsync("insight_order_heads", source.Owner, stream, value) :
            await records.ReplaceAsync("insight_order_heads", source.Owner, stream, head.Revision, value);
        if (!saved) { throw new StorageConflictException(); }
        return true;
    }

    private static Input Read(DurableRecord record)
    {
        var value = OwnedRecordCodec.Decode<Input>(1, record);
        try { InsightPersistenceValidation.Id(value.Id); }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
        if (value.Sequence is <= 0 or long.MaxValue || value.Fingerprint is not { Length: 64 } ||
            value.Fingerprint.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
        { throw new StorageUnavailableException(); }
        return value;
    }

    private sealed record Input([property: JsonRequired] string Id, [property: JsonRequired] string Fingerprint,
        [property: JsonRequired] long Sequence);
}
