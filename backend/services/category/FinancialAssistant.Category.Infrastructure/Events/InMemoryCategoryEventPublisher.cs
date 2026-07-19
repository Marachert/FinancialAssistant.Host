using System.Collections.Concurrent;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Infrastructure.Events;

public sealed class InMemoryCategoryEventPublisher : ICategoryEventPublisher
{
    private readonly ConcurrentQueue<CategoryUpdatedIntegrationEvent> events = new();

    public IReadOnlyCollection<CategoryUpdatedIntegrationEvent> PublishedEvents => events.ToArray();

    public Task PublishAsync(
        CategoryUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        events.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
