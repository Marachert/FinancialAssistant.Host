using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Events;

public sealed class InMemoryTransactionConfirmedPublisher : ITransactionConfirmedPublisher
{
    private readonly ITransactionConfirmedConsumer[] consumers;
    private readonly ConcurrentQueue<TransactionConfirmedIntegrationEvent> publishedEvents = new();

    public InMemoryTransactionConfirmedPublisher(IEnumerable<ITransactionConfirmedConsumer> consumers)
    {
        this.consumers = consumers
            .OrderBy(consumer => consumer.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyCollection<TransactionConfirmedIntegrationEvent> PublishedEvents =>
        publishedEvents.ToArray();

    public async Task PublishAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        foreach (var consumer in consumers)
        {
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
        }

        publishedEvents.Enqueue(integrationEvent);
    }
}
