using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Mcp.Infrastructure;

public sealed class McpReferenceDataProvider(IOptions<McpOptions> options) : IMcpReferenceDataProvider
{
    private static readonly IReadOnlyDictionary<string, McpArchitectureReferenceResponse> References =
        new Dictionary<string, McpArchitectureReferenceResponse>(StringComparer.Ordinal)
        {
            ["service-catalog"] = Reference(
                "service-catalog",
                "Service Catalog and Ownership",
                "Logical authority, owned data, approved dependencies, and deployable boundaries.",
                "https://marachert.atlassian.net/wiki/spaces/FA/pages/589905"),
            ["system-diagrams"] = Reference(
                "system-diagrams",
                "System Diagrams",
                "Service communication, trust boundaries, data ownership, and complete user flow.",
                "https://marachert.atlassian.net/wiki/spaces/FA/pages/557178"),
            ["event-envelope"] = Reference(
                "event-envelope",
                "Event Envelope and Naming",
                "Versioned event envelope, routing keys, privacy rules, and producer ownership.",
                "https://marachert.atlassian.net/wiki/spaces/FA/pages/262498"),
            ["architecture-root"] = Reference(
                "architecture-root",
                "Financial Assistant Architecture",
                "Canonical architecture concept and its governed child pages.",
                "https://marachert.atlassian.net/wiki/spaces/FA/pages/262398")
        };

    public Task<McpPromptEvalSummaryResponse> GetPromptEvalSummaryAsync(
        CancellationToken cancellationToken)
    {
        var value = options.Value.PromptEvaluations;
        return Task.FromResult(new McpPromptEvalSummaryResponse(
            value.Status,
            value.EvaluatedCount,
            value.PassedCount,
            value.FailedCount,
            value.EvaluationSetVersion,
            "aggregate-evaluation-only"));
    }

    public McpArchitectureReferenceResponse GetArchitectureReference(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return References.TryGetValue(normalized, out var reference)
            ? reference
            : throw new ArgumentException(
                "Architecture key must be one of: architecture-root, event-envelope, service-catalog, system-diagrams.",
                nameof(key));
    }

    private static McpArchitectureReferenceResponse Reference(
        string key,
        string title,
        string summary,
        string url) =>
        new(key, title, summary, url, "internal-documentation-only");
}
