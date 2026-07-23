using System.Collections.Concurrent;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Storage;

public sealed class InMemoryAiCallMetadataStore : IAiCallMetadataStore
{
    private readonly ConcurrentDictionary<string, AiCallMetadata> metadataByCallId =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<AiCallMetadata> Records => metadataByCallId.Values.ToArray();

    public Task AddAsync(AiCallMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!metadataByCallId.TryAdd(metadata.CallId, metadata))
        {
            throw new InvalidOperationException($"AI call metadata '{metadata.CallId}' already exists.");
        }

        return Task.CompletedTask;
    }
}
