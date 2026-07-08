namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewaySecurityOptions
{
    public string Mode { get; init; } = GatewaySecurityModes.Placeholder;

    public string AuthenticationHeaderName { get; init; } = "Authorization";

    public string AccessTokenSigningKey { get; init; } = string.Empty;

    public string AccessTokenIssuer { get; init; } = "financial-assistant-identity";

    public string AccessTokenAudience { get; init; } = "financial-assistant-clients";

    public int ClockSkewSeconds { get; init; } = 30;

    public string AdminRole { get; init; } = GatewayRoles.Admin;

    public bool IncludePolicyHeaders { get; init; } = true;

    public GatewayPublicEndpointDefinition[] PublicEndpoints { get; init; } = [];
}
