using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using FinancialAssistant.Shared.Observability;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FinancialAssistant.Shared.Observability.Tests;

public sealed class NativeDiagnosticsTests
{
    [Fact]
    public async Task NeverFormatsOrPersistsMessagesStateExceptionCategoryOrScopes()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using var provider = new NativeDiagnosticsProvider(root.Options(), "monitoring", clock);
        const string canary = "synthetic-private-canary-not-for-diagnostics";
        var logger = provider.CreateLogger(canary);
        using var scope = logger.BeginScope(new Dictionary<string, object> { [canary] = canary });
        using var activity = new Activity("synthetic-operation").SetIdFormat(ActivityIdFormat.W3C).Start();
        logger.Log(LogLevel.Warning, new EventId(27701, canary), new { Payload = canary }, new Exception(canary),
            (_, _) => throw new InvalidOperationException("Formatter must never be invoked."));
        await provider.FlushAsync();
        var record = Assert.Single(await provider.ReadRecentAsync());
        Assert.Equal(27701, record.EventId); Assert.Equal(activity.TraceId.ToHexString(), record.TraceId);
        foreach (var path in Directory.GetFiles(root.Path, "*.object"))
        { Assert.DoesNotContain(canary, File.ReadAllText(path)); }
        Assert.Equal(1, provider.Counters.WrittenRecords);
    }

    [Fact]
    public async Task BoundedQueueMakesVisibilityLossExplicitAndDrainsOnCompletion()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); using var provider = new NativeDiagnosticsProvider(root.Options(), "monitoring", new Clock());
        var logger = provider.CreateLogger("synthetic");
        for (var i = 0; i < 257; i++) { logger.LogInformation("Synthetic technical event."); }
        Assert.Equal(1, provider.Counters.DroppedRecords);
        provider.Complete(); await provider.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(256, provider.Counters.WrittenRecords);
        Assert.Equal(1, provider.Counters.DroppedRecords);
        Assert.True((await provider.ReadRecentAsync()).Count <= 100);
        Assert.True(Directory.GetFiles(root.Path, "*.object").Length <= 4);
    }

    [Fact]
    public async Task RestartAndRaisingRetentionDoNotExtendOriginalExpiry()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using (var provider = new NativeDiagnosticsProvider(root.Options(), "monitoring", clock))
        {
            provider.CreateLogger("synthetic").LogWarning("Synthetic event."); await provider.FlushAsync();
        }
        using (var restarted = new NativeDiagnosticsProvider(root.Options(), "monitoring", clock))
        { Assert.Single(await restarted.ReadRecentAsync()); }
        Assert.Throws<IOException>(() => new NativeDiagnosticsProvider(root.Options(), "expense", clock));
        clock.Now = clock.Now.AddHours(1);
        using var expired = new NativeDiagnosticsProvider(root.Options(hours: 24), "monitoring", clock);
        Assert.Empty(await expired.ReadRecentAsync()); Assert.Empty(Directory.GetFiles(root.Path, "*.object"));
    }

    [Fact]
    public async Task CorruptStorageDoesNotLoopForeverOrHideFailureAtShutdown()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); using var provider = new NativeDiagnosticsProvider(root.Options(), "monitoring", new Clock());
        var logger = provider.CreateLogger("synthetic"); logger.LogWarning("Synthetic event."); await provider.FlushAsync();
        File.WriteAllText(Assert.Single(Directory.GetFiles(root.Path, "*.object")), "invalid synthetic storage");
        logger.LogWarning("Synthetic pending event."); provider.Complete();
        await provider.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, provider.Counters.StorageFailures); Assert.Equal(1, provider.Counters.DroppedRecords);
    }

    [Fact]
    public async Task LoweringRetentionPersistsTheShorterCutoff()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using (var original = new NativeDiagnosticsProvider(root.Options(24), "monitoring", clock))
        { original.CreateLogger("synthetic").LogWarning("Synthetic event."); await original.FlushAsync(); }
        clock.Now = clock.Now.AddMinutes(30);
        using (var shorter = new NativeDiagnosticsProvider(root.Options(1), "monitoring", clock))
        { Assert.Single(await shorter.ReadRecentAsync()); }
        clock.Now = clock.Now.AddHours(1);
        using var raised = new NativeDiagnosticsProvider(root.Options(24), "monitoring", clock);
        Assert.Empty(await raised.ReadRecentAsync());
    }

    [Fact]
    public async Task ClockRollbackReportsVisibilityGapWithoutPersistingAnInvalidBatch()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using var provider = new NativeDiagnosticsProvider(root.Options(), "monitoring", clock);
        provider.CreateLogger("synthetic").LogWarning("Synthetic event.");
        clock.Now = clock.Now.AddMinutes(-1); provider.Complete();
        await provider.RunAsync(default).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, provider.Counters.StorageFailures); Assert.Equal(1, provider.Counters.DroppedRecords);
        Assert.Empty(Directory.GetFiles(root.Path, "*.object"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void RetentionIsExplicitAndBounded(int hours) => Assert.Throws<InvalidOperationException>(() =>
        new NativeDiagnosticsOptions { RootPath = "synthetic", ServiceSid = "synthetic", RetentionHours = hours }.Validate());

    private static bool NativeAvailable()
    {
        if (OperatingSystem.IsWindows()) { return true; }
        Assert.Throws<PlatformNotSupportedException>(() => new NativeDiagnosticsProvider(
            new NativeDiagnosticsOptions { RootPath = "synthetic", ServiceSid = "synthetic", RetentionHours = 1 }, "monitoring", new Clock()));
        return false;
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Root : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fa-diagnostics-test-" + Guid.NewGuid().ToString("N"));
        private readonly string sid;
        public Root()
        {
            if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
            using var identity = WindowsIdentity.GetCurrent(); sid = identity.User!.Value;
            var security = new DirectorySecurity(); security.SetOwner(identity.User); security.SetAccessRuleProtection(true, false);
            foreach (var value in new[] { sid, "S-1-5-18", "S-1-5-32-544" }.Distinct())
            {
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(value), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            }
            new DirectoryInfo(Path).Create(security);
        }
        public NativeDiagnosticsOptions Options(int hours = 1) => new() { Enabled = true, RootPath = Path, ServiceSid = sid, RetentionHours = hours, MaximumBatches = 4 };
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            if (!resolved.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(resolved).StartsWith("fa-diagnostics-test-", StringComparison.Ordinal)) { throw new IOException("Unsafe fixture cleanup."); }
            Directory.Delete(resolved, recursive: true);
        }
    }
}
