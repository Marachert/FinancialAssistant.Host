using System.Text.Json;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class FinancialScoreFinancialEventMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FinancialScoreService service;

    public FinancialScoreFinancialEventMessageHandler(FinancialScoreService service)
    {
        this.service = service;
    }

    public async Task HandleAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<
            IntegrationEventEnvelope<FinancialRecordChangedV1>>(message.Span, JsonOptions) ??
            throw new JsonException("Financial event envelope is required.");
        await service.ApplyAsync(envelope, semanticFactors: null, cancellationToken);
    }
}
