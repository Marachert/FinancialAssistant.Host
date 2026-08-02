using System.Collections.Concurrent;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class InMemoryFinancialScoreEventPublisher : IFinancialScoreEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEventEnvelope<ScoreCalculatedV1>> published = new();

    public IReadOnlyCollection<IntegrationEventEnvelope<ScoreCalculatedV1>> Published =>
        published.ToArray();

    public Task PublishAsync(
        IntegrationEventEnvelope<ScoreCalculatedV1> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        published.Enqueue(envelope);
        return Task.CompletedTask;
    }
}
