using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRouteCatalog
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        GatewayRouteStatuses.Active,
        GatewayRouteStatuses.Placeholder
    };

    private static readonly HashSet<string> AllowedAccessPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        GatewayAccessPolicies.Public,
        GatewayAccessPolicies.Authenticated,
        GatewayAccessPolicies.Admin
    };

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
        HttpMethods.Head,
        HttpMethods.Options
    };

    private readonly IReadOnlyList<GatewayRouteDefinition> routes;
    private readonly IReadOnlyList<GatewayPublicRouteDescriptor> publicRoutes;

    public GatewayRouteCatalog(IOptions<GatewayRouteMapOptions> options)
    {
        var configuredRoutes = options.Value.Routes ?? Array.Empty<GatewayRouteDefinition>();
        Validate(configuredRoutes);

        routes = configuredRoutes
            .OrderBy(route => route.RouteKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        publicRoutes = routes
            .Select(route => new GatewayPublicRouteDescriptor(
                route.RouteKey,
                route.PublicPattern,
                route.CatchAllPattern,
                route.ServiceOwner,
                GatewayAccessPolicies.Normalize(route.AccessPolicy),
                route.Status.Trim().ToLowerInvariant(),
                route.Methods.Select(method => method.ToUpperInvariant()).ToArray()))
            .ToArray();
    }

    public IReadOnlyList<GatewayRouteDefinition> Routes => routes;

    public IReadOnlyList<GatewayPublicRouteDescriptor> PublicRoutes => publicRoutes;

    private static void Validate(IReadOnlyList<GatewayRouteDefinition> configuredRoutes)
    {
        if (configuredRoutes.Count == 0)
        {
            throw new InvalidOperationException("Gateway route configuration must contain at least one route.");
        }

        var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpointSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in configuredRoutes)
        {
            ValidateRoute(route);
            if (!routeKeys.Add(route.RouteKey))
            {
                throw new InvalidOperationException($"Duplicate gateway route key '{route.RouteKey}'.");
            }

            RegisterEndpointSignatures(endpointSignatures, route.RouteKey, route.PublicPattern, route.Methods);
            if (!string.IsNullOrWhiteSpace(route.CatchAllPattern))
            {
                RegisterEndpointSignatures(endpointSignatures, route.RouteKey, route.CatchAllPattern, route.Methods);
            }
        }
    }

    private static void ValidateRoute(GatewayRouteDefinition route)
    {
        if (string.IsNullOrWhiteSpace(route.RouteKey)
            || route.RouteKey.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character == '-')))
        {
            throw new InvalidOperationException("Gateway route keys must use lower-case kebab-case characters only.");
        }

        ValidatePattern(route.PublicPattern, route.RouteKey, isCatchAll: false);
        if (!string.IsNullOrWhiteSpace(route.CatchAllPattern))
        {
            ValidatePattern(route.CatchAllPattern, route.RouteKey, isCatchAll: true);
        }

        if (string.IsNullOrWhiteSpace(route.ServiceOwner)
            || string.IsNullOrWhiteSpace(route.InternalDestination))
        {
            throw new InvalidOperationException($"Gateway route '{route.RouteKey}' must declare a service owner and destination key.");
        }

        if (!AllowedAccessPolicies.Contains(GatewayAccessPolicies.Normalize(route.AccessPolicy)))
        {
            throw new InvalidOperationException($"Gateway route '{route.RouteKey}' has an unsupported access policy.");
        }

        if (string.IsNullOrWhiteSpace(route.Status) || !AllowedStatuses.Contains(route.Status))
        {
            throw new InvalidOperationException($"Gateway route '{route.RouteKey}' has an unsupported status.");
        }

        if (route.Methods is null || route.Methods.Length == 0)
        {
            throw new InvalidOperationException($"Gateway route '{route.RouteKey}' must declare explicit HTTP methods.");
        }

        var methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in route.Methods)
        {
            if (string.IsNullOrWhiteSpace(method) || !AllowedMethods.Contains(method))
            {
                throw new InvalidOperationException($"Gateway route '{route.RouteKey}' contains an unsupported HTTP method.");
            }

            if (!methods.Add(method))
            {
                throw new InvalidOperationException($"Gateway route '{route.RouteKey}' contains duplicate HTTP methods.");
            }
        }
    }

    private static void ValidatePattern(string pattern, string routeKey, bool isCatchAll)
    {
        if (string.IsNullOrWhiteSpace(pattern)
            || !pattern.StartsWith("/", StringComparison.Ordinal)
            || pattern.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || pattern.StartsWith("/gateway", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Gateway route '{routeKey}' contains an invalid public pattern.");
        }

        var containsCatchAll = pattern.Contains("{**", StringComparison.Ordinal);
        if (containsCatchAll != isCatchAll)
        {
            throw new InvalidOperationException($"Gateway route '{routeKey}' contains an invalid catch-all pattern.");
        }
    }

    private static void RegisterEndpointSignatures(
        HashSet<string> endpointSignatures,
        string routeKey,
        string pattern,
        IEnumerable<string> methods)
    {
        foreach (var method in methods)
        {
            var signature = $"{method.ToUpperInvariant()} {pattern}";
            if (!endpointSignatures.Add(signature))
            {
                throw new InvalidOperationException($"Gateway route '{routeKey}' duplicates endpoint '{signature}'.");
            }
        }
    }
}

public static class GatewayRouteStatuses
{
    public const string Active = "active";
    public const string Placeholder = "placeholder";
}

public sealed record GatewayPublicRouteDescriptor(
    string RouteKey,
    string PublicPattern,
    string? CatchAllPattern,
    string ServiceOwner,
    string AccessPolicy,
    string Status,
    IReadOnlyList<string> Methods);
