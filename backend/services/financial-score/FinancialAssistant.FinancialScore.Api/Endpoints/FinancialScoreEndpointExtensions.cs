using System.Globalization;
using FinancialAssistant.FinancialScore.Api.Security;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Contracts;
using FinancialAssistant.FinancialScore.Domain;

namespace FinancialAssistant.FinancialScore.Api.Endpoints;

public static class FinancialScoreEndpointExtensions
{
    public static IEndpointRouteBuilder MapFinancialScoreEndpoints(this IEndpointRouteBuilder app)
    {
        MapCurrent(app, FinancialScoreApiRoutes.Current, "GetCurrentFinancialScore");
        MapCurrent(app, FinancialScoreApiRoutes.GatewayCurrent, "GetCurrentFinancialScoreFromGateway");
        MapHistory(app, FinancialScoreApiRoutes.History, "GetFinancialScoreHistory");
        MapHistory(app, FinancialScoreApiRoutes.GatewayHistory, "GetFinancialScoreHistoryFromGateway");
        return app;
    }

    private static void MapCurrent(IEndpointRouteBuilder app, string route, string name)
    {
        app.MapGet(route, HandleCurrentAsync)
            .WithName(name)
            .Produces<FinancialScoreResponse>()
            .Produces<FinancialScoreApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<FinancialScoreApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<FinancialScoreApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static void MapHistory(IEndpointRouteBuilder app, string route, string name)
    {
        app.MapGet(route, HandleHistoryAsync)
            .WithName(name)
            .Produces<FinancialScoreHistoryResponse>()
            .Produces<FinancialScoreApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<FinancialScoreApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleCurrentAsync(
        HttpContext context,
        FinancialScoreService service,
        FinancialScoreGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(context, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var score = await service.GetCurrentAsync(
                FinancialScoreOwnerHasher.Hash(userId!),
                ReadRequiredQuery(context, "currency"),
                cancellationToken);
            return score is null
                ? Problem(
                    context,
                    "Financial score is not available.",
                    "A score is calculated after the first confirmed financial record event.",
                    "financial_score_not_found",
                    StatusCodes.Status404NotFound)
                : Results.Ok(Map(score));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
    }

    private static async Task<IResult> HandleHistoryAsync(
        HttpContext context,
        FinancialScoreService service,
        FinancialScoreGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(context, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var limit = ReadOptionalInteger(context, "limit") ?? 20;
            var beforeUtc = ReadOptionalTimestamp(context, "beforeUtc");
            var history = await service.GetHistoryAsync(
                FinancialScoreOwnerHasher.Hash(userId!),
                ReadRequiredQuery(context, "currency"),
                beforeUtc,
                limit,
                cancellationToken);
            return Results.Ok(
                new FinancialScoreHistoryResponse(
                    history.Take(limit).Select(Map).ToArray(),
                    limit,
                    history.Count > limit));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
    }

    private static FinancialScoreResponse Map(FinancialScoreCalculation calculation) =>
        new(
            calculation.CalculationId,
            calculation.Currency,
            calculation.Score,
            calculation.FormulaVersion,
            calculation.Factors
                .Select(item => new FinancialScoreFactorResponse(
                    item.Code,
                    item.Contribution,
                    item.Explanation))
                .ToArray(),
            calculation.CalculatedAtUtc);

    private static string ReadRequiredQuery(HttpContext context, string name)
    {
        var value = context.Request.Query[name].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"Query parameter '{name}' is required.")
            : value;
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

    private static DateTimeOffset? ReadOptionalTimestamp(HttpContext context, string name)
    {
        var value = context.Request.Query[name].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : throw new ArgumentException(
                $"Query parameter '{name}' must be an ISO 8601 timestamp.");
    }

    private static IResult? Authenticate(
        HttpContext context,
        FinancialScoreGatewayAuthenticator authenticator,
        out string? userId)
    {
        userId = null;
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted gateway authentication is required.",
                "Financial score requests are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = context.Request.Headers[FinancialScoreGatewayHeaders.UserId]
            .FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(userId)
            ? Problem(
                context,
                "Authentication is required.",
                "Financial score requests require a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized)
            : null;
    }

    private static IResult Invalid(HttpContext context, string detail) =>
        Problem(
            context,
            "Financial score request is invalid.",
            detail,
            "invalid_financial_score_request",
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
