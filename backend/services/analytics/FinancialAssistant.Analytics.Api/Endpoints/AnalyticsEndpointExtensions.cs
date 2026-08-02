using FinancialAssistant.Analytics.Api.Security;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;

namespace FinancialAssistant.Analytics.Api.Endpoints;

public static class AnalyticsEndpointExtensions
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        MapDashboard(app, AnalyticsApiRoutes.Dashboard, "GetAnalyticsDashboard");
        MapDashboard(app, AnalyticsApiRoutes.GatewayDashboard, "GetAnalyticsDashboardFromGateway");
        return app;
    }

    private static void MapDashboard(IEndpointRouteBuilder app, string route, string name)
    {
        app.MapGet(route, HandleDashboardAsync)
            .WithName(name)
            .Produces<AnalyticsDashboardResponse>()
            .Produces<AnalyticsApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<AnalyticsApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleDashboardAsync(
        HttpContext httpContext,
        string currency,
        string timeZoneId,
        DateOnly? referenceDate,
        decimal? dailyExpenseLimit,
        int? trendDays,
        AnalyticsProjector projector,
        AnalyticsGatewayAuthenticator authenticator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var now = timeProvider.GetUtcNow();
            var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
            var result = await projector.GetDashboardAsync(
                AnalyticsOwnerHasher.Hash(userId!),
                currency,
                referenceDate ?? DateOnly.FromDateTime(localNow.DateTime),
                dailyExpenseLimit,
                trendDays ?? 7,
                now,
                StaleAfter,
                cancellationToken);
            return Results.Ok(AnalyticsDashboardMapper.Map(result, timeZone.Id));
        }
        catch (TimeZoneNotFoundException exception)
        {
            return Invalid(httpContext, exception.Message);
        }
        catch (InvalidTimeZoneException exception)
        {
            return Invalid(httpContext, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception.Message);
        }
    }

    private static IResult? Authenticate(
        HttpContext httpContext,
        AnalyticsGatewayAuthenticator authenticator,
        out string? userId)
    {
        userId = null;
        if (!authenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Analytics requests are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = httpContext.Request.Headers[AnalyticsGatewayHeaders.UserId]
            .FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Analytics requests require a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static IResult Invalid(HttpContext context, string detail) =>
        Problem(
            context,
            "Analytics request is invalid.",
            detail,
            "invalid_analytics_request",
            StatusCodes.Status400BadRequest);

    private static IResult Problem(
        HttpContext context,
        string title,
        string detail,
        string code,
        int statusCode) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
                ["traceId"] = context.TraceIdentifier
            });
}
