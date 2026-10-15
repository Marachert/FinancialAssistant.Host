using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Infrastructure;
using Xunit;

namespace FinancialAssistant.Monitoring.Tests;

public sealed class MonitoringServiceProbeCacheTests
{
    [Fact]
    public async Task PreservesObservationTimeExpiresAndDoesNotShareAcrossProcesses()
    {
        var clock = new Clock(); var cache = new MonitoringServiceProbeCache(clock, 2); var calls = 0;
        var target = new MonitoringServiceTargetOptions { Name = "synthetic-service", BaseAddress = "http://127.0.0.1:5101" };
        Task<MonitoringServiceProbe> Probe() { calls++; return Task.FromResult(new MonitoringServiceProbe(target.Name, "healthy", 1, clock.GetUtcNow(), null)); }
        var first = await cache.GetAsync(target, Probe, default);
        clock.Ticks += TimeSpan.TicksPerSecond;
        Assert.Same(first, await cache.GetAsync(target, Probe, default)); Assert.Equal(1, calls);
        clock.Ticks += TimeSpan.TicksPerSecond;
        Assert.NotSame(first, await cache.GetAsync(target, Probe, default)); Assert.Equal(2, calls);
        await new MonitoringServiceProbeCache(clock, 2).GetAsync(target, Probe, default); Assert.Equal(3, calls);
    }

    [Fact]
    public async Task EvictionIsBoundedAndCancellationDoesNotInventObservation()
    {
        var clock = new Clock(); var cache = new MonitoringServiceProbeCache(clock, 1); var calls = 0;
        Task<MonitoringServiceProbe> Probe() { calls++; return Task.FromResult(new MonitoringServiceProbe("synthetic", "unavailable", 1, clock.GetUtcNow(), "transport")); }
        for (var i = 0; i <= MonitoringServiceProbeCache.MaximumEntries; i++)
        {
            await cache.GetAsync(new MonitoringServiceTargetOptions { Name = "service" + i }, Probe, default);
            clock.Ticks++;
        }
        await cache.GetAsync(new MonitoringServiceTargetOptions { Name = "service0" }, Probe, default);
        Assert.Equal(MonitoringServiceProbeCache.MaximumEntries + 2, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(new MonitoringServiceTargetOptions { Name = "cancelled" }, Probe, new CancellationToken(true)));
        Assert.Equal(MonitoringServiceProbeCache.MaximumEntries + 2, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void LifetimeCannotBeUnbounded(int seconds) => Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringServiceProbeCache(TimeProvider.System, seconds));

    [Fact]
    public async Task SameTimestampOlderProbeCannotOverwriteNewerFailureObservation()
    {
        var clock = new Clock(); var cache = new MonitoringServiceProbeCache(clock, 2);
        var target = new MonitoringServiceTargetOptions { Name = "synthetic-tied-timestamp" };
        var older = new TaskCompletionSource<MonitoringServiceProbe>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = cache.GetAsync(target, () => older.Task, default);
        var newer = new MonitoringServiceProbe(target.Name, "unavailable", 1, clock.GetUtcNow(), "transport");
        Assert.Same(newer, await cache.GetAsync(target, () => Task.FromResult(newer), default));
        older.SetResult(new MonitoringServiceProbe(target.Name, "healthy", 1, clock.GetUtcNow(), null));
        await first;
        Assert.Same(newer, await cache.GetAsync(target, () => throw new InvalidOperationException("Fresh cached observation must be retained."), default));
    }

    private sealed class Clock : TimeProvider
    {
        public long Ticks { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero).AddTicks(Ticks);
    }
}
