using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.NativeFiles;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Shared.Observability;

public sealed class NativeDiagnosticsProvider : ILoggerProvider
{
    private readonly ProtectedDirectory files;
    private readonly NativeDiagnosticsOptions options;
    private readonly string service;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Channel<NativeDiagnosticRecord> queue = Channel.CreateBounded<NativeDiagnosticRecord>(new BoundedChannelOptions(256)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private long dropped;
    private long failures;
    private long written;
    private bool disposed;
    private int completed;

    public NativeDiagnosticsProvider(NativeDiagnosticsOptions options, string serviceId, TimeProvider clock)
    {
        options.Validate();
        _ = NativeServiceCatalog.Get(serviceId);
        this.options = options; service = serviceId; this.clock = clock;
        files = new ProtectedDirectory(options.RootPath, options.ServiceSid, options.MaximumBatches + 32);
        try { PruneAsync(default).GetAwaiter().GetResult(); }
        catch { files.Dispose(); throw new IOException("Native diagnostics storage is invalid."); }
    }

    public ILogger CreateLogger(string categoryName) => new SafeLogger(this);
    public NativeDiagnosticCounters Counters => new(Interlocked.Read(ref written), Interlocked.Read(ref dropped), Interlocked.Read(ref failures));
    public void Complete() { Interlocked.Exchange(ref completed, 1); queue.Writer.TryComplete(); }

    private void Capture(LogLevel level, EventId eventId)
    {
        if (level is < LogLevel.Information or >= LogLevel.None) { return; }
        var activity = Activity.Current;
        var trace = activity?.IdFormat == ActivityIdFormat.W3C ? activity.TraceId.ToHexString() : null;
        var span = activity?.IdFormat == ActivityIdFormat.W3C ? activity.SpanId.ToHexString() : null;
        var value = new NativeDiagnosticRecord(clock.GetUtcNow().ToUniversalTime(), eventId.Id is >= 0 and <= 1_000_000 ? eventId.Id : 0, level, trace, span);
        if (!queue.Writer.TryWrite(value)) { Interlocked.Increment(ref dropped); }
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                try { await FlushAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch
                {
                    Interlocked.Increment(ref failures);
                    if (Volatile.Read(ref completed) != 0)
                    { while (queue.Reader.TryRead(out _)) { Interlocked.Increment(ref dropped); } break; }
                    await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            await PruneAsync(token);
            var records = new List<NativeDiagnosticRecord>();
            while (records.Count < 25 && queue.Reader.TryRead(out var value)) { records.Add(value); }
            if (records.Count == 0) { return; }
            var now = clock.GetUtcNow().ToUniversalTime();
            if (records.Any(record => record.TimestampUtc > now))
            { Interlocked.Add(ref dropped, records.Count); throw new IOException("Native diagnostic clock moved backwards."); }
            var batch = new Batch(1, service, now, now.AddHours(options.RetentionHours), records.ToArray());
            var bytes = JsonSerializer.SerializeToUtf8Bytes(batch);
            var name = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())) + ".object";
            try { await files.WriteAsync(name, bytes, replace: false, token); }
            catch { Interlocked.Add(ref dropped, records.Count); throw; }
            Interlocked.Add(ref written, records.Count);
            await PruneAsync(token);
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<NativeDiagnosticRecord>> ReadRecentAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var batches = await ReadBatchesAsync(token);
            var now = clock.GetUtcNow();
            return batches.Where(item => now < EffectiveExpiry(item.Batch)).SelectMany(item => item.Batch.Records)
                .OrderByDescending(record => record.TimestampUtc).Take(100).ToArray();
        }
        finally { gate.Release(); }
    }

    private async Task PruneAsync(CancellationToken token)
    {
        var batches = await ReadBatchesAsync(token);
        var now = clock.GetUtcNow();
        foreach (var item in batches.Where(item => now >= EffectiveExpiry(item.Batch))) { files.DeleteFile(item.Name); }
        foreach (var item in batches.Where(item => now < EffectiveExpiry(item.Batch) && EffectiveExpiry(item.Batch) < item.Batch.ExpiresAtUtc))
        { await files.WriteAsync(item.Name, JsonSerializer.SerializeToUtf8Bytes(item.Batch with { ExpiresAtUtc = EffectiveExpiry(item.Batch) }), replace: true, token); }
        var retained = batches.Where(item => now < EffectiveExpiry(item.Batch)).OrderBy(item => item.Batch.WrittenAtUtc).ToArray();
        foreach (var item in retained.Take(Math.Max(0, retained.Length - options.MaximumBatches))) { files.DeleteFile(item.Name); }
        foreach (var name in files.ListFiles().Where(name => name.EndsWith(".tmp", StringComparison.Ordinal)).Take(25)) { files.DeleteTemporary(name); }
    }

    private async Task<List<(string Name, Batch Batch)>> ReadBatchesAsync(CancellationToken token)
    {
        var batches = new List<(string Name, Batch Batch)>();
        foreach (var name in files.ListFiles().Where(name => name.EndsWith(".object", StringComparison.Ordinal)))
        {
            var bytes = await files.ReadAsync(name, 64 * 1024, token) ?? throw new IOException("Native diagnostics record is unavailable.");
            var value = JsonSerializer.Deserialize<Batch>(bytes, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                ?? throw new IOException("Native diagnostics record is invalid.");
            if (value.Version != 1 || value.ServiceId != service || value.WrittenAtUtc == default || value.WrittenAtUtc.Offset != TimeSpan.Zero
                || value.ExpiresAtUtc.Offset != TimeSpan.Zero || (value.ExpiresAtUtc - value.WrittenAtUtc).TotalHours is < 1 or > 24
                || (value.ExpiresAtUtc - value.WrittenAtUtc).Ticks % TimeSpan.TicksPerHour != 0
                || value.Records is null || value.Records.Length is < 1 or > 25)
            { throw new IOException("Native diagnostics record is invalid."); }
            foreach (var record in value.Records)
            {
                if (record is null || record.TimestampUtc == default || record.TimestampUtc.Offset != TimeSpan.Zero
                    || record.TimestampUtc > value.WrittenAtUtc || record.EventId is < 0 or > 1_000_000
                    || record.Level is < LogLevel.Information or >= LogLevel.None || !Hex(record.TraceId, 32) || !Hex(record.SpanId, 16)
                    || (record.TraceId is null) != (record.SpanId is null))
                { throw new IOException("Native diagnostics record is invalid."); }
            }
            batches.Add((name, value));
        }
        return batches;
    }

    private DateTimeOffset EffectiveExpiry(Batch value) => value.ExpiresAtUtc - value.WrittenAtUtc > TimeSpan.FromHours(options.RetentionHours)
        ? value.WrittenAtUtc.AddHours(options.RetentionHours) : value.ExpiresAtUtc;
    private static bool Hex(string? value, int length) => value is null || value.Length == length && value.All(char.IsAsciiHexDigit);
    public void Dispose()
    {
        Complete(); gate.Wait();
        try
        {
            if (!disposed)
            {
                disposed = true; files.Dispose();
                while (queue.Reader.TryRead(out _)) { Interlocked.Increment(ref dropped); }
            }
        }
        finally { gate.Release(); }
    }
    private sealed class SafeLogger(NativeDiagnosticsProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level is >= LogLevel.Information and < LogLevel.None;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => owner.Capture(level, eventId);
    }
    private sealed record Batch([property: JsonRequired] int Version, [property: JsonRequired] string ServiceId,
        [property: JsonRequired] DateTimeOffset WrittenAtUtc, [property: JsonRequired] DateTimeOffset ExpiresAtUtc,
        [property: JsonRequired] NativeDiagnosticRecord[] Records);
}

public sealed record NativeDiagnosticRecord([property: JsonRequired] DateTimeOffset TimestampUtc,
    [property: JsonRequired] int EventId, [property: JsonRequired] LogLevel Level,
    [property: JsonRequired] string? TraceId, [property: JsonRequired] string? SpanId);
public sealed record NativeDiagnosticCounters(long WrittenRecords, long DroppedRecords, long StorageFailures);
