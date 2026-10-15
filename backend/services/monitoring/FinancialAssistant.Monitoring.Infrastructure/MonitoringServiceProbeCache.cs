using FinancialAssistant.Monitoring.Application;

namespace FinancialAssistant.Monitoring.Infrastructure;

public sealed class MonitoringServiceProbeCache
{
    private readonly object gate = new();
    private readonly TimeProvider clock;
    private readonly TimeSpan lifetime;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    public const int MaximumEntries = 32;

    public MonitoringServiceProbeCache(TimeProvider clock, int seconds)
    {
        if (seconds is < 1 or > 5) { throw new ArgumentOutOfRangeException(nameof(seconds)); }
        this.clock = clock;
        lifetime = TimeSpan.FromSeconds(seconds);
    }

    public async Task<MonitoringServiceProbe> GetAsync(MonitoringServiceTargetOptions target,
        Func<Task<MonitoringServiceProbe>> probe, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (target.Name.Length is < 1 or > 64 || target.BaseAddress.Length > 2048) { throw new ArgumentException("Invalid health cache target."); }
        var key = target.Name + "\n" + target.BaseAddress;
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                var age = clock.GetElapsedTime(entry.Timestamp);
                if (age >= TimeSpan.Zero && age < lifetime) { return entry.Value; }
                entries.Remove(key);
            }
        }
        var started = clock.GetTimestamp();
        var result = await probe();
        token.ThrowIfCancellationRequested();
        lock (gate)
        {
            var age = clock.GetElapsedTime(started);
            if (age < TimeSpan.Zero || age >= lifetime) { return result; }
            if (entries.TryGetValue(key, out var newer) && newer.Timestamp > started) { return result; }
            if (entries.Count >= MaximumEntries && !entries.ContainsKey(key))
            { entries.Remove(entries.MinBy(pair => pair.Value.Timestamp).Key); }
            entries[key] = new Entry(started, result);
        }
        return result;
    }

    private sealed record Entry(long Timestamp, MonitoringServiceProbe Value);
}
