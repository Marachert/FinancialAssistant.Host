using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class OutboxIdentityEventPublisher : IIdentityEventPublisher
{
    private readonly IIdentityEventOutbox outbox;
    private readonly IIdentityEventSubjectHasher subjectHasher;
    private readonly string producer;

    public OutboxIdentityEventPublisher(
        IIdentityEventOutbox outbox,
        IIdentityEventSubjectHasher subjectHasher,
        IOptions<IdentityServiceOptions> options)
    {
        this.outbox = outbox;
        this.subjectHasher = subjectHasher;
        producer = options.Value.ServiceName;
    }

    public Task PublishAsync(
        IdentityEventPublication publication,
        CancellationToken cancellationToken = default)
    {
        Validate(publication);
        var envelope = new IdentityIntegrationEventEnvelope(
            Guid.NewGuid().ToString("D"),
            publication.EventName,
            publication.OccurredAtUtc,
            null,
            producer,
            publication.SchemaVersion,
            publication.CorrelationId,
            publication.CausationId,
            publication.SubjectId is null ? null : subjectHasher.Hash(publication.SubjectId),
            publication.Data,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["contentType"] = "application/json"
            });

        return outbox.EnqueueAsync(envelope, cancellationToken);
    }

    private static void Validate(IdentityEventPublication publication)
    {
        if (string.IsNullOrWhiteSpace(publication.EventName)
            || !publication.EventName.EndsWith($".v{publication.SchemaVersion}", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(publication.CorrelationId)
            || string.IsNullOrWhiteSpace(publication.CausationId)
            || publication.SchemaVersion < 1)
        {
            throw new InvalidOperationException("The identity event publication does not satisfy the shared envelope contract.");
        }
    }
}
