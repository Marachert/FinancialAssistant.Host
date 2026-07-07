using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Tests;

public sealed class CapturingIdentityEventPublisher : IIdentityEventPublisher
{
    private readonly ConcurrentQueue<IdentityEventPublication> publications = new();

    public IReadOnlyCollection<IdentityEventPublication> Publications => publications.ToArray();

    public Task PublishAsync(
        IdentityEventPublication publication,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        publications.Enqueue(publication);
        return Task.CompletedTask;
    }
}
