namespace FinancialAssistant.Monitoring.Contracts;

public sealed record MonitoringJobSignalRequest(
    Guid OperationId,
    string SourceService,
    string Kind,
    string State,
    int Revision,
    string? ErrorCategory);

public sealed record MonitoringJobResponse(
    Guid OperationId,
    string Service,
    string Kind,
    string State,
    int Revision,
    DateTimeOffset ObservedAtUtc,
    string? ErrorCategory);

public sealed record MonitoringJobsResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<MonitoringJobResponse> Jobs,
    int Capacity,
    int RetentionHours,
    string DataClassification);
