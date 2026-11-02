using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeBrokerWorkerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StorageDeadlinesAndBrokerFailuresRetryWithoutStoppingTheHost(int failure)
    {
        var clock = new TickClock(); var calls = 0;
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = new NativeBrokerWorker(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                return Task.FromException(failure switch
                { 0 => new OperationCanceledException(), 1 => new NativeBrokerUnavailableException(), _ => new IOException() });
            }
            retried.TrySetResult(); return Task.CompletedTask;
        }, clock, NullLogger<NativeBrokerWorker>.Instance);
        await worker.StartAsync(default);
        Assert.Equal(1, calls); Assert.False(worker.ExecuteTask!.IsCompleted);
        Assert.Equal(TimeSpan.FromSeconds(2), clock.LastDueTime);
        clock.Tick(); await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, calls);
        await worker.StopAsync(default);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ShutdownDoesNotRetryAnUnsettledOperation()
    {
        var calls = 0;
        using var worker = new NativeBrokerWorker(async token =>
        { Interlocked.Increment(ref calls); await Task.Delay(Timeout.InfiniteTimeSpan, token); }, new TickClock(), NullLogger<NativeBrokerWorker>.Instance);
        await worker.StartAsync(default); Assert.Equal(1, calls);
        await worker.StopAsync(default);
        Assert.Equal(1, calls); Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FailureLogsNeverIncludeExceptionMessagesOrPayloads()
    {
        var logger = new CaptureLogger();
        using var worker = new NativeBrokerWorker(_ => throw new InvalidOperationException("synthetic-private-payload"), new TickClock(), logger);
        await worker.StartAsync(default);
        await worker.StopAsync(default);
        Assert.Equal("Native lifecycle attempt failed; retrying with bounded backoff.", Assert.Single(logger.Messages));
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    private sealed class CaptureLogger : ILogger<NativeBrokerWorker>
    {
        internal List<string> Messages { get; } = new();
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.Null(exception); Messages.Add(formatter(state, exception)); }
    }
    private sealed class TickClock : TimeProvider
    {
        private TickTimer? timer;
        public TimeSpan LastDueTime { get; private set; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { LastDueTime = dueTime; return timer = new TickTimer(callback, state); }
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
