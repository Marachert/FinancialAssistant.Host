using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewaySecurityBoundary
{
    private const string ProblemJson = "application/problem+json";
    private const string GatewayAccessPolicyHeader = "X-Gateway-Access-Policy";
    private const string GatewaySecurityModeHeader = "X-Gateway-Security-Mode";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GatewaySecurityOptions options;
    private readonly GatewayAccessTokenValidator accessTokenValidator;
    private readonly ILogger<GatewaySecurityBoundary> logger;

    public GatewaySecurityBoundary(
        IOptions<GatewaySecurityOptions> options,
        GatewayAccessTokenValidator accessTokenValidator,
        ILogger<GatewaySecurityBoundary> logger)
    {
        this.options = options.Value;
        this.accessTokenValidator = accessTokenValidator;
        this.logger = logger;
    }

    public async Task<bool> AuthorizeAsync(HttpContext context, GatewayRouteDefinition route)
    {
        var accessPolicy = GatewayAccessPolicies.Normalize(route.AccessPolicy);
        AddPolicyHeaders(context, accessPolicy);

        if (!GatewaySecurityModes.IsEnforceMode(options.Mode))
        {
            GatewayOperationalLog.SecurityPlaceholderEvaluated(
                logger,
                route.RouteKey,
                accessPolicy);
            return true;
        }

        if (accessTokenValidator.IsPublicEndpoint(context.Request)
            || string.Equals(accessPolicy, GatewayAccessPolicies.Public, StringComparison.OrdinalIgnoreCase))
        {
            GatewayOperationalLog.PublicRequestAllowed(logger, route.RouteKey);
            return true;
        }

        var validation = accessTokenValidator.Validate(context.Request.Headers);
        if (validation.Status != GatewayAccessTokenStatus.Valid || validation.UserContext is null)
        {
            var error = validation.Status switch
            {
                GatewayAccessTokenStatus.Missing => new SecurityError(
                    StatusCodes.Status401Unauthorized,
                    "authentication_required",
                    "Authentication is required.",
                    "A valid session is required for this request."),
                GatewayAccessTokenStatus.Expired => new SecurityError(
                    StatusCodes.Status401Unauthorized,
                    "session_expired",
                    "The session has expired.",
                    "Sign in again or refresh the session."),
                _ => new SecurityError(
                    StatusCodes.Status401Unauthorized,
                    "session_invalid",
                    "The session is invalid.",
                    "A valid session is required for this request.")
            };

            GatewayOperationalLog.AuthenticationRejected(
                logger,
                route.RouteKey,
                validation.Status.ToString());
            await WriteSecurityProblemAsync(context, error, includeBearerChallenge: true);
            return false;
        }

        context.Items[GatewayUserContext.ContextItemKey] = validation.UserContext;

        if (string.Equals(accessPolicy, GatewayAccessPolicies.Admin, StringComparison.OrdinalIgnoreCase)
            && !validation.UserContext.IsInRole(options.AdminRole))
        {
            GatewayOperationalLog.AdminRoleRejected(logger, route.RouteKey);
            await WriteSecurityProblemAsync(
                context,
                new SecurityError(
                    StatusCodes.Status403Forbidden,
                    "forbidden",
                    "Access is forbidden.",
                    "The current session does not have permission to access this resource."),
                includeBearerChallenge: false);
            return false;
        }

        GatewayOperationalLog.RequestAuthorized(
            logger,
            route.RouteKey,
            accessPolicy);
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

    private static async Task WriteSecurityProblemAsync(
        HttpContext context,
        SecurityError error,
        bool includeBearerChallenge)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = error.StatusCode;
        context.Response.ContentType = ProblemJson;
        context.Response.Headers.CacheControl = "no-store";
        if (includeBearerChallenge)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var response = new
        {
            type = $"https://errors.financial-assistant.app/gateway/{error.Code.Replace('_', '-')}",
            title = error.Title,
            status = error.StatusCode,
            code = error.Code,
            detail = error.Detail,
            correlationId = CorrelationHeaders.GetCorrelationId(context)
        };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            JsonOptions,
            context.RequestAborted);
    }

    private sealed record SecurityError(
        int StatusCode,
        string Code,
        string Title,
        string Detail);
}
