using System.Text.Json;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Infrastructure;

public sealed class AuditEventMessageHandler(IAuditEventConsumer consumer)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> HandleAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope<AuditEventV1>>(
                message.Span,
                JsonOptions)
            ?? throw new JsonException("Audit event envelope is required.");
        return await consumer.ConsumeAsync(envelope, cancellationToken);
    }
}
