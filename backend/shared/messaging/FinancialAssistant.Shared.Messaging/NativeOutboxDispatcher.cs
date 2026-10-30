namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeOutboxWork
{
    public NativeOutboxWork(IReadOnlyList<NativePublication> publications, Func<CancellationToken, Task> acknowledge)
    {
        ArgumentNullException.ThrowIfNull(publications); ArgumentNullException.ThrowIfNull(acknowledge);
        if (publications.Count is < 1 or > 4 || publications.Any(p => p is null))
        { throw new ArgumentException("A native outbox item requires a bounded immutable publication set."); }
        Publications = Array.AsReadOnly(publications.ToArray()); Acknowledge = acknowledge;
    }

    public IReadOnlyList<NativePublication> Publications { get; }
    internal Func<CancellationToken, Task> Acknowledge { get; }
    public override string ToString() => "Native outbox work (payload omitted).";
}

public sealed class NativeOutboxDispatcher(INativePublisher publisher,
    Func<CancellationToken, Task<IReadOnlyList<NativeOutboxWork>>> readPending)
{
    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        var batch = await readPending(cancellationToken);
        if (batch is null || batch.Count > 25 || batch.Any(work => work is null))
        { throw new InvalidOperationException("Invalid native outbox batch."); }
        var completed = 0;
        foreach (var work in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var publication in work.Publications) { await publisher.PublishAsync(publication, cancellationToken); }
            await work.Acknowledge(cancellationToken); completed++;
        }
        return completed;
    }
}
