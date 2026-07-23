namespace FinancialAssistant.AiOrchestration.Application;

public interface IAiOrchestrationService
{
    Task<AiCapabilityResult> ExecuteAsync(
        AiCapabilityRequest request,
        CancellationToken cancellationToken);
}
