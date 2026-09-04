namespace FinancialAssistant.Shared.Observability;

public sealed record ObservabilityRuntimeIdentity(
    string ServiceName,
    string Environment);
