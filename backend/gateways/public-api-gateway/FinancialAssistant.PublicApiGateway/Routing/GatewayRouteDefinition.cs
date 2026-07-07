namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRouteDefinition
{
    public string RouteKey { get; init; } = string.Empty;

    public string PublicPattern { get; init; } = string.Empty;

    public string? CatchAllPattern { get; init; }

    public string ServiceOwner { get; init; } = string.Empty;

    public string InternalDestination { get; init; } = string.Empty;

    public string AccessPolicy { get; init; } = string.Empty;

    public string Status { get; init; } = "placeholder";

    public string[] Methods { get; init; } = Array.Empty<string>();
}
