using FinancialAssistant.Monitoring.Api.Security;
using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Api.Endpoints;

public static class MonitoringEndpointExtensions
{
    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder app)
    {
        MapDashboard(app, MonitoringApiRoutes.Dashboard, "GetMonitoringDashboardFromGateway");
        MapDashboard(app, MonitoringApiRoutes.ServiceDashboard, "GetMonitoringDashboard");
        app.MapPost(MonitoringApiRoutes.AiUsageSignals, RecordAiUsage)
            .WithName("RecordMonitoringAiUsage")
            .Produces<MonitoringSignalAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status401Unauthorized);
        app.MapPost(MonitoringApiRoutes.ParsingQualitySignals, RecordParsingQuality)
            .WithName("RecordMonitoringParsingQuality")
            .Produces<MonitoringSignalAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status401Unauthorized);
        app.MapPost(MonitoringApiRoutes.UiFunnelSignals, RecordUiFunnel)
            .WithName("RecordMonitoringUiFunnel")
            .Produces<MonitoringSignalAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status401Unauthorized);
        return app;
    }

    private static void MapDashboard(IEndpointRouteBuilder app, string route, string name)
    {
        app.MapGet(route, GetDashboardAsync)
            .WithName(name)
            .Produces<MonitoringDashboardResponse>()
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<MonitoringApiErrorResponse>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext context,
        MonitoringGatewayAuthenticator authenticator,
        MonitoringSnapshotService service,
        CancellationToken cancellationToken)
    {
        var authenticationError = AuthenticateAdmin(context, authenticator);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await service.GetAsync(cancellationToken));
    }

    private static IResult RecordAiUsage(
        HttpContext context,
        MonitoringAiUsageSignalRequest request,
        MonitoringSignalAuthenticator authenticator,
        IMonitoringMetricStore store) =>
        RecordSignal(context, authenticator, () => store.Record(request));

    private static IResult RecordParsingQuality(
        HttpContext context,
        MonitoringParsingQualitySignalRequest request,
        MonitoringSignalAuthenticator authenticator,
        IMonitoringMetricStore store) =>
        RecordSignal(context, authenticator, () => store.Record(request));

    private static IResult RecordUiFunnel(
        HttpContext context,
        MonitoringUiFunnelSignalRequest request,
        MonitoringSignalAuthenticator authenticator,
        IMonitoringMetricStore store) =>
        RecordSignal(context, authenticator, () => store.Record(request));

    private static IResult RecordSignal(
        HttpContext context,
        MonitoringSignalAuthenticator authenticator,
        Action record)
    {
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted service authentication is required.",
                "Monitoring signals are accepted only from authenticated services.",
                "trusted_service_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            record();
            return Results.Json(
                new MonitoringSignalAcceptedResponse("accepted"),
                statusCode: StatusCodes.Status202Accepted);
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                "Monitoring signal is invalid.",
                "The signal source, stage, or numeric values are not allowlisted.",
                "invalid_monitoring_signal",
                StatusCodes.Status400BadRequest);
        }
        catch (OverflowException)
        {
            return Problem(
                context,
                "Monitoring signal is invalid.",
                "The aggregate numeric limit would be exceeded.",
                "monitoring_signal_limit_exceeded",
                StatusCodes.Status400BadRequest);
        }
    }

    private static IResult? AuthenticateAdmin(
        HttpContext context,
        MonitoringGatewayAuthenticator authenticator)
    {
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted gateway authentication is required.",
                "Monitoring dashboards are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return authenticator.IsAdmin(context)
            ? null
            : Problem(
                context,
                "Admin access is required.",
                "Monitoring dashboards require the admin role.",
                "admin_role_required",
                StatusCodes.Status403Forbidden);
    }

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
