namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewayPublicEndpointDefinition
{
    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}
