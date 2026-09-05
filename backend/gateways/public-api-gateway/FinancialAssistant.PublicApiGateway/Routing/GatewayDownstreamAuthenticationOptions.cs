namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayDownstreamAuthenticationOptions
{
    public const string HeaderName = "X-Gateway-Authentication";
    public const int MinimumSharedSecretLength = 32;

    public string SharedSecret { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SharedSecret)
        && SharedSecret.Length >= MinimumSharedSecretLength;
}
