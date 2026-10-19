using System.Globalization;
using System.Text.Json.Serialization;

namespace FinancialAssistant.Shared.Persistence;

public static class OwnedUsageReservations
{
    public static void Validate(string owner, string provider, DateOnly day, int limit, string requestId)
    {
        foreach (var value in new[] { owner, provider, requestId })
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(c =>
                !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
            { throw new ArgumentException("Invalid durable usage identity."); }
        }
        if (day == default || limit is < 1 or > 10_000) { throw new ArgumentException("Invalid durable usage day or limit."); }
    }

    // The caller holds the service-owned subject lock; a crash cannot refund a committed reservation.
    public static async Task<bool> TryAcquireAsync(OwnedRecordTransaction records, string owner, string provider,
        DateOnly day, int limit, string requestId)
    {
        Validate(owner, provider, day, limit, requestId);
        var key = provider + ":" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var prior = await records.GetAsync("usage_requests", owner, requestId);
        if (prior is not null)
        {
            var reservation = OwnedRecordCodec.Decode<Reservation>(1, prior);
            if (reservation.Provider != provider || reservation.Day != day) { throw new StorageConflictException(); }
            return reservation.Accepted;
        }
        var counterRecord = await records.GetAsync("usage_days", owner, key);
        var counter = counterRecord is null ? new Counter(0) : OwnedRecordCodec.Decode<Counter>(1, counterRecord);
        if (counter.Count is < 0 or > 10_000) { throw new StorageUnavailableException(); }
        var accepted = counter.Count < limit;
        if (accepted)
        {
            var encoded = OwnedRecordCodec.Encode(1, new Counter(counter.Count + 1));
            if (counterRecord is null) { await records.InsertAsync("usage_days", owner, key, encoded); }
            else { await records.ReplaceAsync("usage_days", owner, key, counterRecord.Revision, encoded); }
        }
        await records.InsertAsync("usage_requests", owner, requestId, OwnedRecordCodec.Encode(1, new Reservation(provider, day, accepted)));
        return accepted;
    }

    private sealed record Reservation([property: JsonRequired] string Provider, [property: JsonRequired] DateOnly Day,
        [property: JsonRequired] bool Accepted);
    private sealed record Counter([property: JsonRequired] int Count);
}
