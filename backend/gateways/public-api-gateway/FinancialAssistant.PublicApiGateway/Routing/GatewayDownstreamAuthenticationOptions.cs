namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDownstreamAuthenticationOptions
{
    public const string HeaderName = "X-Gateway-Authentication";

    public string SharedSecret { get; init; } = string.Empty;
}
