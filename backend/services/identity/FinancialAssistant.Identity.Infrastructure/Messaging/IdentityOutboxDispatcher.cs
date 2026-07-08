using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class IdentityOutboxDispatcher : BackgroundService
{
    private readonly IIdentityEventOutbox outbox;
    private readonly IIdentityEventTransport transport;
    private readonly ISystemClock clock;
    private readonly IdentityEventPublishingOptions options;
    private readonly ILogger<IdentityOutboxDispatcher> logger;

    public IdentityOutboxDispatcher(
        IIdentityEventOutbox outbox,
        IIdentityEventTransport transport,
        ISystemClock clock,
        IOptions<IdentityServiceOptions> options,
        ILogger<IdentityOutboxDispatcher> logger)
    {
        this.outbox = outbox;
        this.transport = transport;
        this.clock = clock;
        this.options = options.Value.Events;
        this.logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var pending = await outbox.ReadPendingAsync(options.BatchSize, now, cancellationToken);
        var publishedCount = 0;

        foreach (var message in pending)
        {
            try
            {
                var publishedAt = clock.UtcNow;
                await transport.PublishAsync(
                    message.Envelope with { PublishedAt = publishedAt },
                    cancellationToken);
                await outbox.MarkPublishedAsync(
                    message.Envelope.EventId,
                    publishedAt,
                    cancellationToken);
                publishedCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var retrySeconds = Math.Min(
                    Math.Max(1, options.MaximumRetryDelaySeconds),
                    Math.Pow(2, Math.Min(10, message.AttemptCount + 1)));
                await outbox.MarkFailedAsync(
                    message.Envelope.EventId,
                    exception.GetType().Name,
                    clock.UtcNow.AddSeconds(retrySeconds),
                    cancellationToken);
                IdentityOperationalLog.OutboxPublishFailed(
                    logger,
                    message.Envelope.EventId,
                    message.Envelope.EventType,
                    message.Envelope.CorrelationId,
                    message.Envelope.CausationId,
                    exception.GetType().Name,
                    message.AttemptCount,
                    retrySeconds);
            }
        }

        return publishedCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(
            Math.Max(100, options.DispatchIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchPendingAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
