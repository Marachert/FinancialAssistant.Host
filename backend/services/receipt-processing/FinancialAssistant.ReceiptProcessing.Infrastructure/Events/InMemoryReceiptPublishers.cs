using System.Collections.Concurrent;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Events;

public sealed class InMemoryReceiptUploadedPublisher : IReceiptUploadedPublisher
{
    private readonly IReceiptUploadedConsumer[] consumers;
    private readonly ConcurrentQueue<ReceiptUploadedIntegrationEvent> published = new();

    public InMemoryReceiptUploadedPublisher(IEnumerable<IReceiptUploadedConsumer> consumers)
    {
        this.consumers = consumers.ToArray();
    }

    public IReadOnlyCollection<ReceiptUploadedIntegrationEvent> PublishedEvents =>
        published.ToArray();

    public async Task PublishAsync(
        ReceiptUploadedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        published.Enqueue(integrationEvent);
        foreach (var consumer in consumers)
        {
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
        }
    }
}

public sealed class InMemoryOcrCompletedPublisher : IOcrCompletedPublisher
{
    private readonly IOcrCompletedConsumer[] consumers;
    private readonly ConcurrentQueue<OcrCompletedIntegrationEvent> published = new();

    public InMemoryOcrCompletedPublisher(IEnumerable<IOcrCompletedConsumer> consumers)
    {
        this.consumers = consumers.ToArray();
    }

    public IReadOnlyCollection<OcrCompletedIntegrationEvent> PublishedEvents =>
        published.ToArray();

    public async Task PublishAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        published.Enqueue(integrationEvent);
        foreach (var consumer in consumers)
        {
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
        }
    }
}
