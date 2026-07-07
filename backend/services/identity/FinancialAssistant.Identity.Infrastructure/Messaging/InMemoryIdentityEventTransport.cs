using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class InMemoryIdentityEventTransport : IIdentityEventTransport
{
    private readonly ConcurrentQueue<IdentityIntegrationEventEnvelope> published = new();

    public IReadOnlyCollection<IdentityIntegrationEventEnvelope> Published => published.ToArray();

    public Task PublishAsync(
        IdentityIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        published.Enqueue(envelope);
        return Task.CompletedTask;
    }
}
