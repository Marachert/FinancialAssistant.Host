using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class NoOpIdentityEventPublisher : IIdentityEventPublisher
{
    private readonly ILogger<NoOpIdentityEventPublisher> logger;

    public NoOpIdentityEventPublisher(ILogger<NoOpIdentityEventPublisher> logger)
    {
        this.logger = logger;
    }

    public Task PublishAsync(
        IdentityEventPublication publication,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Identity event publishing placeholder skipped event {EventName} version {EventVersion}.",
            publication.EventName,
            publication.EventVersion);

        return Task.CompletedTask;
    }
}
