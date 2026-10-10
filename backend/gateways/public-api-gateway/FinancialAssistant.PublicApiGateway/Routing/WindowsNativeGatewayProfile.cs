using FinancialAssistant.PublicApiGateway.Diagnostics;
using FinancialAssistant.PublicApiGateway.Security;
using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.PublicApiGateway.Routing;

public static class WindowsNativeGatewayProfile
{
    private static readonly IReadOnlyDictionary<string, string> DestinationServices = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["auth-service"] = "identity",
        ["profile-service"] = "profile",
        ["category-service"] = "category",
        ["transaction-intake-service"] = "transaction-intake",
        ["receipt-file-intake-service"] = "receipt-processing",
        ["income-service"] = "income",
        ["expense-service"] = "expense",
        ["analytics-service"] = "analytics",
        ["financial-score-service"] = "financial-score",
        ["recommendation-service"] = "recommendations-notifications",
        ["notification-service"] = "recommendations-notifications",
        ["monitoring-admin-service"] = "monitoring",
        ["audit-service"] = "audit"
    };

    private static readonly IReadOnlyDictionary<string, string> RouteDestinations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["auth"] = "auth-service",
        ["profile-me"] = "profile-service",
        ["categories"] = "category-service",
        ["transaction-intake"] = "transaction-intake-service",
        ["transaction-draft-review"] = "transaction-intake-service",
        ["transaction-receipt-draft"] = "transaction-intake-service",
        ["transaction-draft-confirm"] = "transaction-intake-service",
        ["transaction-draft-reject"] = "transaction-intake-service",
        ["receipts"] = "receipt-file-intake-service",
        ["incomes"] = "income-service",
        ["expenses"] = "expense-service",
        ["analytics"] = "analytics-service",
        ["score"] = "financial-score-service",
        ["recommendations"] = "recommendation-service",
        ["notifications"] = "notification-service",
        ["notification-preferences"] = "notification-service",
        ["admin-monitoring"] = "monitoring-admin-service",
        ["admin-audit"] = "audit-service"
    };

    private static readonly string[] PublicAuthenticationPaths =
    [
        "/auth/v1/register", "/auth/v1/sign-in", "/auth/v1/refresh",
        "/auth/v1/providers/google/sign-in", "/auth/v1/providers/apple/sign-in",
        "/auth/v1/providers/phone/verifications", "/auth/v1/providers/phone/verifications/confirm"
    ];

    public static WebApplicationBuilder AddWindowsNativeGatewayProfile(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment))
        {
            return builder;
        }

        Apply(builder.Configuration);
        builder.Services.AddHttpClient(NativeGatewayReadinessHealthCheck.ClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(2))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        builder.Services.AddHealthChecks()
            .AddCheck<NativeGatewayReadinessHealthCheck>("native_downstream_readiness", tags: ["ready"]);
        return builder;
    }

    public static void Apply(ConfigurationManager configuration)
    {
        var destinations = configuration.GetSection("Gateway:DestinationMap").Get<GatewayDestinationMapOptions>()?.Destinations
            ?? [];
        var routes = configuration.GetSection("Gateway:RouteMap").Get<GatewayRouteMapOptions>()?.Routes ?? [];
        if (destinations.Length != DestinationServices.Count
            || destinations.Select(destination => destination.DestinationKey).Distinct(StringComparer.Ordinal).Count() != DestinationServices.Count
            || destinations.Any(destination => !DestinationServices.ContainsKey(destination.DestinationKey))
            || routes.Length != RouteDestinations.Count
            || routes.Select(route => route.RouteKey).Distinct(StringComparer.Ordinal).Count() != RouteDestinations.Count
            || routes.Any(route => !RouteDestinations.TryGetValue(route.RouteKey, out var destination) || route.InternalDestination != destination))
        {
            throw new InvalidOperationException("Native gateway requires the complete known route and destination catalog.");
        }

        var publicEndpoints = configuration.GetSection("Gateway:Security").Get<GatewaySecurityOptions>()?.PublicEndpoints ?? [];
        if (publicEndpoints.Length != PublicAuthenticationPaths.Length
            || publicEndpoints.Select(endpoint => endpoint.Path).Distinct(StringComparer.Ordinal).Count() != PublicAuthenticationPaths.Length
            || publicEndpoints.Any(endpoint => endpoint.Method != "POST" || !PublicAuthenticationPaths.Contains(endpoint.Path, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Native gateway public allowlist must contain only the known authentication endpoints.");
        }

        var overrides = new Dictionary<string, string?>
        {
            ["Gateway:Security:Mode"] = "enforce",
            ["Gateway:Security:AuthenticationHeaderName"] = "Authorization"
        };
        for (var index = 0; index < destinations.Length; index++)
        {
            var service = NativeServiceCatalog.Get(DestinationServices[destinations[index].DestinationKey]);
            overrides[$"Gateway:DestinationMap:Destinations:{index}:BaseAddress"] = service.BaseAddress;
            overrides[$"Gateway:DestinationMap:Destinations:{index}:Enabled"] = "true";
            overrides[$"Gateway:DestinationMap:Destinations:{index}:RequiresGatewayAuthentication"] = service.Id == "identity" ? "false" : "true";
        }

        for (var index = 0; index < routes.Length; index++)
        {
            overrides[$"Gateway:RouteMap:Routes:{index}:Status"] = GatewayRouteStatuses.Active;
            overrides[$"Gateway:RouteMap:Routes:{index}:AccessPolicy"] = routes[index].RouteKey.StartsWith("admin-", StringComparison.Ordinal)
                ? GatewayAccessPolicies.Admin : GatewayAccessPolicies.Authenticated;
        }

        configuration.AddInMemoryCollection(overrides);
    }
}
