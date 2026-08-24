using FinancialAssistant.Mcp.Contracts;

namespace FinancialAssistant.Mcp.Application;

public sealed class McpToolRegistry
{
    private static readonly IReadOnlyList<McpToolDescriptor> Definitions =
    [
        Tool(
            McpToolNames.SystemHealth,
            "Returns privacy-safe service and infrastructure health.",
            McpRoles.Admin,
            McpRoles.Operator,
            McpRoles.Developer,
            McpRoles.QualityAssurance),
        Tool(
            McpToolNames.AiCostSummary,
            "Returns aggregate AI request, token, and estimated cost counters.",
            McpRoles.Admin,
            McpRoles.Operator),
        Tool(
            McpToolNames.ParsingQuality,
            "Returns aggregate parsing success, review, and failure counters.",
            McpRoles.Admin,
            McpRoles.Operator,
            McpRoles.QualityAssurance),
        Tool(
            McpToolNames.PromptEvalSummary,
            "Returns aggregate prompt evaluation status without prompts or responses.",
            McpRoles.Admin,
            McpRoles.Developer,
            McpRoles.QualityAssurance),
        Tool(
            McpToolNames.JiraIssueDraft,
            "Creates a local Jira issue draft without submitting or mutating Jira.",
            McpRoles.Admin,
            McpRoles.Developer),
        Tool(
            McpToolNames.ArchitectureLookup,
            "Returns one allowlisted Financial Assistant architecture reference.",
            McpRoles.Admin,
            McpRoles.Operator,
            McpRoles.Developer,
            McpRoles.QualityAssurance)
    ];

    private readonly IReadOnlyDictionary<string, McpToolDescriptor> byName = Definitions
        .ToDictionary(definition => definition.Name, StringComparer.Ordinal);

    public IReadOnlyList<McpToolDescriptor> All => Definitions;

    public McpToolDescriptor Get(string name) =>
        byName.TryGetValue(name, out var definition)
            ? definition
            : throw new ArgumentException("The MCP tool is not allowlisted.", nameof(name));

    public bool IsAllowed(string name, IEnumerable<string> roles)
    {
        var definition = Get(name);
        var callerRoles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return definition.AllowedRoles.Any(callerRoles.Contains);
    }

    private static McpToolDescriptor Tool(
        string name,
        string description,
        params string[] allowedRoles) =>
        new(name, description, allowedRoles, true, "aggregate-operational-only");
}
