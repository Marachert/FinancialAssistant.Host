using FinancialAssistant.Mcp.Contracts;

namespace FinancialAssistant.Mcp.Application;

public sealed class McpToolExecutor(
    McpToolRegistry registry,
    IMcpOperationalDataProvider operationalData,
    IMcpReferenceDataProvider referenceData,
    IMcpAuditSink auditSink,
    TimeProvider timeProvider)
{
    public Task<McpSystemHealthResponse> GetSystemHealthAsync(
        McpCallContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.SystemHealth,
            context,
            operationalData.GetSystemHealthAsync,
            cancellationToken);

    public Task<McpAiCostSummaryResponse> GetAiCostSummaryAsync(
        McpCallContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.AiCostSummary,
            context,
            operationalData.GetAiCostSummaryAsync,
            cancellationToken);

    public Task<McpParsingQualityResponse> GetParsingQualityAsync(
        McpCallContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.ParsingQuality,
            context,
            operationalData.GetParsingQualityAsync,
            cancellationToken);

    public Task<McpPromptEvalSummaryResponse> GetPromptEvalSummaryAsync(
        McpCallContext context,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.PromptEvalSummary,
            context,
            referenceData.GetPromptEvalSummaryAsync,
            cancellationToken);

    public Task<McpJiraIssueDraftResponse> CreateJiraIssueDraftAsync(
        McpCallContext context,
        string summary,
        string goal,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.JiraIssueDraft,
            context,
            _ => Task.FromResult(CreateJiraDraft(summary, goal)),
            cancellationToken);

    public Task<McpArchitectureReferenceResponse> GetArchitectureReferenceAsync(
        McpCallContext context,
        string key,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            McpToolNames.ArchitectureLookup,
            context,
            _ => Task.FromResult(referenceData.GetArchitectureReference(key)),
            cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        string toolName,
        McpCallContext context,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ValidateContext(context);
        if (!registry.IsAllowed(toolName, context.Roles))
        {
            await RecordAsync(toolName, context.CorrelationId, "denied", "role", cancellationToken);
            throw new UnauthorizedAccessException("The caller role is not allowed for this MCP tool.");
        }

        T result;
        try
        {
            result = await operation(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await RecordAsync(
                toolName,
                context.CorrelationId,
                "failed",
                SafeFailureCategory(exception),
                cancellationToken);
            throw;
        }

        await RecordAsync(toolName, context.CorrelationId, "succeeded", null, cancellationToken);
        return result;
    }

    private Task RecordAsync(
        string toolName,
        string correlationId,
        string outcome,
        string? failureCategory,
        CancellationToken cancellationToken) =>
        auditSink.RecordAsync(
            new McpAuditEntry(
                correlationId,
                toolName,
                outcome,
                failureCategory,
                timeProvider.GetUtcNow()),
            cancellationToken);

    private static McpJiraIssueDraftResponse CreateJiraDraft(string summary, string goal)
    {
        var safeSummary = RequireBoundedText(summary, nameof(summary), 120);
        var safeGoal = RequireBoundedText(goal, nameof(goal), 1000);
        return new McpJiraIssueDraftResponse(
            "FIN",
            "Task",
            safeSummary,
            $"Goal: {safeGoal}\n\nAcceptance criteria:\n- [ ] Define bounded behavior\n- [ ] Add synthetic verification\n- [ ] Update architecture evidence",
            ["mcp-draft"],
            true);
    }

    private static string RequireBoundedText(string value, string parameterName, int maximumLength)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0 || normalized.Length > maximumLength
            || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Value must contain 1-{maximumLength} characters without control characters.",
                parameterName);
        }

        return normalized;
    }

    private static void ValidateContext(McpCallContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CorrelationId)
            || context.CorrelationId.Length > 128
            || context.CorrelationId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException("A safe correlation identifier is required.", nameof(context));
        }
    }

    private static string SafeFailureCategory(Exception exception) => exception switch
    {
        ArgumentException => "validation",
        HttpRequestException => "dependency",
        UnauthorizedAccessException => "authorization",
        _ => "internal"
    };
}
