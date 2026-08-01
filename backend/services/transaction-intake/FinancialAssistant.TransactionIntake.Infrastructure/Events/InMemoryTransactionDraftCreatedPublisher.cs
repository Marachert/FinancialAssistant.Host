using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Events;

public sealed class InMemoryTransactionDraftCreatedPublisher : ITransactionDraftCreatedPublisher
{
    private readonly ITransactionDraftCreatedConsumer[] consumers;
    private readonly ConcurrentQueue<TransactionDraftCreatedIntegrationEvent> publishedEvents = new();

    public InMemoryTransactionDraftCreatedPublisher(
        IEnumerable<ITransactionDraftCreatedConsumer> consumers)
    {
        this.consumers = consumers
            .OrderBy(consumer => consumer.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyCollection<TransactionDraftCreatedIntegrationEvent> PublishedEvents =>
        publishedEvents.ToArray();

    public async Task PublishAsync(
        TransactionDraftCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        foreach (var consumer in consumers)
        {
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
        }

        publishedEvents.Enqueue(integrationEvent);
    }
}
