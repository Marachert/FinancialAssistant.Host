namespace FinancialAssistant.Mcp.Contracts;

public static class McpToolNames
{
    public const string SystemHealth = "system_health";
    public const string AiCostSummary = "ai_cost_summary";
    public const string ParsingQuality = "parsing_quality";
    public const string PromptEvalSummary = "prompt_eval_summary";
    public const string JiraIssueDraft = "jira_issue_draft";
    public const string ArchitectureLookup = "architecture_lookup";
}

public static class McpRoles
{
    public const string Admin = "admin";
    public const string Operator = "operator";
    public const string Developer = "developer";
    public const string QualityAssurance = "qa";
}

public static class McpHeaders
{
    public const string Authentication = "X-Mcp-Authentication";
    public const string Roles = "X-Mcp-Roles";
    public const string CorrelationId = "X-Correlation-Id";
}

public sealed record McpToolDescriptor(
    string Name,
    string Description,
    IReadOnlyList<string> AllowedRoles,
    bool IsReadOnly,
    string DataClassification);

public sealed record McpServiceStatus(
    string Service,
    string Status,
    long LatencyMilliseconds,
    string? ErrorCategory);

public sealed record McpSystemHealthResponse(
    string Status,
    IReadOnlyList<McpServiceStatus> Services,
    string RabbitMqStatus,
    long RabbitMqQueueDepth,
    string ElasticsearchStatus,
    string DataClassification);

public sealed record McpAiCostSummaryResponse(
    long RequestCount,
    long SuccessfulRequestCount,
    long InputTokenCount,
    long OutputTokenCount,
    long EstimatedCostMicros,
    string DataClassification);

public sealed record McpParsingQualityResponse(
    long ProcessedCount,
    long SuccessfulCount,
    long ReviewRequiredCount,
    long FailedCount,
    decimal SuccessPercent,
    string DataClassification);

public sealed record McpPromptEvalSummaryResponse(
    string Status,
    long EvaluatedCount,
    long PassedCount,
    long FailedCount,
    string EvaluationSetVersion,
    string DataClassification);

public sealed record McpJiraIssueDraftResponse(
    string ProjectKey,
    string IssueType,
    string Summary,
    string Description,
    IReadOnlyList<string> Labels,
    bool RequiresHumanSubmission);

public sealed record McpArchitectureReferenceResponse(
    string Key,
    string Title,
    string Summary,
    string Url,
    string DataClassification);

public sealed record McpAuditEntry(
    string CorrelationId,
    string ToolName,
    string Outcome,
    string? FailureCategory,
    DateTimeOffset OccurredAtUtc);

public sealed record McpServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    int AllowlistedToolCount,
    string DataClassification);
