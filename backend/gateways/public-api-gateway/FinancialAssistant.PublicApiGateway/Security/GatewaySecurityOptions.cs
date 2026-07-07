namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewaySecurityOptions
{
    public string Mode { get; init; } = GatewaySecurityModes.Placeholder;

    public string AuthenticationHeaderName { get; init; } = "Authorization";

    public string AdminScopeHeaderName { get; init; } = "X-Gateway-Admin-Scope";

    public bool IncludePolicyHeaders { get; init; } = true;
}
