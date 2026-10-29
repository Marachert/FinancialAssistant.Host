using FinancialAssistant.Audit.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class AuditRetentionWorkerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StorageDeadlineAndStorageFailuresRetryWithoutFaultingTheWorker(int failure)
    {
        var clock = new TickClock(); var calls = 0;
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = new AuditRetentionWorker(token =>
        {
            Assert.False(token.IsCancellationRequested);
            if (Interlocked.Increment(ref calls) == 1)
            {
                return Task.FromException<AuditCleanupResult>(failure switch
                {
                    0 => new OperationCanceledException("synthetic storage deadline", CancellationToken.None),
                    1 => new StorageUnavailableException(),
                    _ => new StorageConflictException()
                });
            }
            retried.TrySetResult(); return Task.FromResult(new AuditCleanupResult(0, 0, null));
        }, clock, NullLogger<AuditRetentionWorker>.Instance);
        await worker.StartAsync(default);
        Assert.Equal(1, calls); Assert.False(worker.ExecuteTask!.IsCompleted);
        clock.Tick(); await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls); Assert.False(worker.ExecuteTask.IsCompleted);
        await worker.StopAsync(default);
        Assert.False(worker.ExecuteTask.IsFaulted);
    }

    [Fact]
    public async Task ShutdownCancellationExitsCleanupWithoutRetryOrFailure()
    {
        var calls = 0;
        using var worker = new AuditRetentionWorker(async token =>
        {
            Interlocked.Increment(ref calls); await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new AuditCleanupResult(0, 0, null);
        }, new TickClock(), NullLogger<AuditRetentionWorker>.Instance);
        await worker.StartAsync(default); Assert.Equal(1, calls);
        await worker.StopAsync(default);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully); Assert.Equal(1, calls);
    }

    private sealed class TickClock : TimeProvider
    {
        private TickTimer? timer;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => timer = new TickTimer(callback, state);
        public void Tick() => timer!.Tick();
    }
    private sealed class TickTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool disposed;
        public void Tick() { if (!disposed) { callback(state); } }
        public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
        public void Dispose() => disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
