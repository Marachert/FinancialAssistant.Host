namespace FinancialAssistant.AiOrchestration.Application;

public sealed class AiRequestAlreadyRecordedException(string? callId, string state)
    : Exception("This AI request was already reserved; automatic provider replay is not permitted.")
{
    public string? CallId { get; } = callId;
    public string State { get; } = state;
}
