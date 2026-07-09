namespace FinancialAssistant.ServiceTemplate.Contracts;

public sealed record ServiceInfoResponse(
    string Name,
    string Version,
    string Environment);
