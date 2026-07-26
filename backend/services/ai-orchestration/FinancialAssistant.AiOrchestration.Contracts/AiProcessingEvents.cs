namespace FinancialAssistant.AiOrchestration.Contracts;

public static class AiParsingJobStatuses
{
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string SuggestionReady = "suggestion_ready";
    public const string Failed = "failed";
}

public sealed record AiParsingJobCommand(
    string CommandId,
    string JobId,
    string DraftId,
    string UserId,
    string SourcePayloadReferenceId,
    int Attempt,
    DateTimeOffset RequestedAtUtc)
{
    public const string Name = "ai.parsing.requested.v1";

    public string CommandType => Name;
}

public sealed record AiSuggestionReadyIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string SuggestionReferenceId,
    decimal Confidence,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ai.suggestion-ready.v1";

    public string EventType => Name;
}

public sealed record AiParsingFailedIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string FailureCategory,
    bool Retryable,
    int Attempt,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ai.parsing-failed.v1";

    public string EventType => Name;
}

public sealed record AiParsingStatusUpdatedIntegrationEvent(
    string EventId,
    string JobId,
    string DraftId,
    string UserId,
    string Status,
    int Attempt,
    DateTimeOffset OccurredAtUtc)
{
    public const string Name = "ai.parsing-status-updated.v1";

    public string EventType => Name;
}

public interface IAiParsingJobConsumer
{
    Task ConsumeAsync(
        AiParsingJobCommand command,
        CancellationToken cancellationToken);
}

public interface IAiSuggestionReadyConsumer
{
    Task ConsumeAsync(
        AiSuggestionReadyIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IAiParsingFailedConsumer
{
    Task ConsumeAsync(
        AiParsingFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IAiParsingStatusUpdatedConsumer
{
    Task ConsumeAsync(
        AiParsingStatusUpdatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
