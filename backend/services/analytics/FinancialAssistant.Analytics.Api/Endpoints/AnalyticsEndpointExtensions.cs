using System.Globalization;
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
        MapCategoryBreakdown(
            app,
            AnalyticsApiRoutes.CategoryBreakdown,
            "GetAnalyticsCategoryBreakdown");
        MapCategoryBreakdown(
            app,
            AnalyticsApiRoutes.GatewayCategoryBreakdown,
            "GetAnalyticsCategoryBreakdownFromGateway");
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

    private static void MapCategoryBreakdown(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapGet(route, HandleCategoryBreakdownAsync)
            .WithName(name)
            .Produces<AnalyticsCategoryBreakdownResponse>()
            .Produces<AnalyticsApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<AnalyticsApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleCategoryBreakdownAsync(
        HttpContext httpContext,
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
            var currency = ReadRequiredQuery(httpContext, "currency");
            var timeZoneId = ReadRequiredQuery(httpContext, "timeZoneId");
            var period = ReadRequiredQuery(httpContext, "period");
            var referenceDate = ReadOptionalDate(httpContext, "referenceDate");
            var top = ReadOptionalInteger(httpContext, "top") ?? 5;
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
            var effectiveReferenceDate =
                referenceDate ?? DateOnly.FromDateTime(localNow.DateTime);
            var result = await projector.GetCategoryBreakdownAsync(
                AnalyticsOwnerHasher.Hash(userId!),
                currency,
                effectiveReferenceDate,
                period,
                top,
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

    private static async Task<IResult> HandleDashboardAsync(
        HttpContext httpContext,
        AnalyticsProjector projector,
        IAnalyticsDailyLimitProvider dailyLimitProvider,
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
            var currency = ReadRequiredQuery(httpContext, "currency");
            var timeZoneId = ReadRequiredQuery(httpContext, "timeZoneId");
            var referenceDate = ReadOptionalDate(httpContext, "referenceDate");
            var trendDays = ReadOptionalInteger(httpContext, "trendDays") ?? 7;
            if (httpContext.Request.Query.ContainsKey("dailyExpenseLimit"))
            {
                throw new ArgumentException(
                    "Daily expense limits are resolved from the authoritative server-side source.");
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var now = timeProvider.GetUtcNow();
            var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
            var ownerHash = AnalyticsOwnerHasher.Hash(userId!);
            var effectiveReferenceDate =
                referenceDate ?? DateOnly.FromDateTime(localNow.DateTime);
            var dailyExpenseLimit = await dailyLimitProvider.GetDailyExpenseLimitAsync(
                ownerHash,
                currency,
                effectiveReferenceDate,
                cancellationToken);
            var result = await projector.GetDashboardAsync(
                ownerHash,
                currency,
                effectiveReferenceDate,
                dailyExpenseLimit,
                trendDays,
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

    private static string ReadRequiredQuery(HttpContext context, string name)
    {
        var value = context.Request.Query[name].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"Query parameter '{name}' is required.")
            : value;
    }

    private static DateOnly? ReadOptionalDate(HttpContext context, string name)
    {
        var value = context.Request.Query[name].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
            ? result
            : throw new ArgumentException(
                $"Query parameter '{name}' must use yyyy-MM-dd format.");
    }

    private static int? ReadOptionalInteger(HttpContext context, string name)
    {
        var value = context.Request.Query[name].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new ArgumentException($"Query parameter '{name}' must be an integer.");
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
