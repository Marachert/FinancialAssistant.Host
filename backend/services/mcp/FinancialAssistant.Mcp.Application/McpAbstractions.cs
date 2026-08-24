using FinancialAssistant.Mcp.Contracts;

namespace FinancialAssistant.Mcp.Application;

public interface IMcpOperationalDataProvider
{
    Task<McpSystemHealthResponse> GetSystemHealthAsync(CancellationToken cancellationToken);

    Task<McpAiCostSummaryResponse> GetAiCostSummaryAsync(CancellationToken cancellationToken);

    Task<McpParsingQualityResponse> GetParsingQualityAsync(CancellationToken cancellationToken);
}

public interface IMcpReferenceDataProvider
{
    Task<McpPromptEvalSummaryResponse> GetPromptEvalSummaryAsync(
        CancellationToken cancellationToken);

    McpArchitectureReferenceResponse GetArchitectureReference(string key);
}

public interface IMcpAuditSink
{
    Task RecordAsync(McpAuditEntry entry, CancellationToken cancellationToken);
}

public sealed record McpCallContext(
    string CorrelationId,
    IReadOnlyCollection<string> Roles);
