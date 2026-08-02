using System.Text.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class AnalyticsFinancialEventMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AnalyticsProjector projector;

    public AnalyticsFinancialEventMessageHandler(AnalyticsProjector projector)
    {
        this.projector = projector;
    }

    public async Task HandleAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<
            IntegrationEventEnvelope<FinancialRecordChangedV1>>(message.Span, JsonOptions) ??
            throw new JsonException("Financial event envelope is required.");

        await projector.ApplyAsync(envelope, cancellationToken);
    }
}
