namespace FinancialAssistant.Shared.Elasticsearch;

/// <summary>
/// Service-owned hook that verifies the current template, mapping, and aliases.
/// Implementations must be idempotent and fail when incompatible drift is detected.
/// </summary>
public interface IElasticsearchMappingBootstrap
{
    Task EnsureCurrentMappingAsync(
        CancellationToken cancellationToken = default);
}
