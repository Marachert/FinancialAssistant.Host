using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewaySecurityBoundary
{
    private const string GatewayAccessPolicyHeader = "X-Gateway-Access-Policy";
    private const string GatewaySecurityModeHeader = "X-Gateway-Security-Mode";

    private readonly GatewaySecurityOptions options;
    private readonly ILogger<GatewaySecurityBoundary> logger;

    public GatewaySecurityBoundary(
        IOptions<GatewaySecurityOptions> options,
        ILogger<GatewaySecurityBoundary> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<bool> AuthorizeAsync(HttpContext context, GatewayRouteDefinition route)
    {
        var accessPolicy = GatewayAccessPolicies.Normalize(route.AccessPolicy);
        AddPolicyHeaders(context, accessPolicy);

        if (!GatewaySecurityModes.IsEnforceMode(options.Mode))
        {
            logger.LogDebug(
                "Gateway security boundary evaluated route {RouteKey} with access policy {AccessPolicy} in placeholder mode.",
                route.RouteKey,
                accessPolicy);

            return true;
        }

        if (string.Equals(accessPolicy, GatewayAccessPolicies.Public, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!HasAuthenticationHeader(context))
        {
            await WriteSecurityProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                route,
                accessPolicy,
                "Authentication is required for this route.");

            return false;
        }

        if (string.Equals(accessPolicy, GatewayAccessPolicies.Admin, StringComparison.OrdinalIgnoreCase) && !HasAdminPlaceholderScope(context))
        {
            await WriteSecurityProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "forbidden",
                route,
                accessPolicy,
                "Admin access is required for this route.");

            return false;
        }

        return true;
    }

    private void AddPolicyHeaders(HttpContext context, string accessPolicy)
    {
        if (!options.IncludePolicyHeaders)
        {
            return;
        }

        context.Response.Headers[GatewayAccessPolicyHeader] = accessPolicy;
        context.Response.Headers[GatewaySecurityModeHeader] = options.Mode;
    }

    private bool HasAuthenticationHeader(HttpContext context)
    {
        var headerName = string.IsNullOrWhiteSpace(options.AuthenticationHeaderName)
            ? "Authorization"
            : options.AuthenticationHeaderName;

        return context.Request.Headers.TryGetValue(headerName, out var values)
            && values.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    private bool HasAdminPlaceholderScope(HttpContext context)
    {
        var headerName = string.IsNullOrWhiteSpace(options.AdminScopeHeaderName)
            ? "X-Gateway-Admin-Scope"
            : options.AdminScopeHeaderName;

        return context.Request.Headers.TryGetValue(headerName, out var values)
            && values.Any(value => string.Equals(value, GatewayAccessPolicies.Admin, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteSecurityProblemAsync(
        HttpContext context,
        int statusCode,
        string status,
        GatewayRouteDefinition route,
        string accessPolicy,
        string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            status,
            routeKey = route.RouteKey,
            accessPolicy,
            correlationId = CorrelationHeaders.GetCorrelationId(context),
            message
        });
    }
}
