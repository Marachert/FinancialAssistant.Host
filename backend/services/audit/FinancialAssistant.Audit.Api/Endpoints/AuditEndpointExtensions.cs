using FinancialAssistant.Audit.Api.Security;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Api.Endpoints;

public static class AuditEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        MapQuery(app, AuditApiRoutes.Dashboard, "GetAuditTrailFromGateway");
        MapQuery(app, AuditApiRoutes.ServiceDashboard, "GetAuditTrail");
        app.MapPost(AuditApiRoutes.InternalEvents, RecordAsync)
            .WithName("RecordAuditEvent")
            .Produces<AuditAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<AuditApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<AuditApiErrorResponse>(StatusCodes.Status401Unauthorized);
        return app;
    }

    private static void MapQuery(IEndpointRouteBuilder app, string route, string name)
    {
        app.MapGet(route, QueryAsync)
            .WithName(name)
            .Produces<AuditQueryResponse>()
            .Produces<AuditApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<AuditApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<AuditApiErrorResponse>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> RecordAsync(
        HttpContext context,
        IntegrationEventEnvelope<AuditEventV1> request,
        AuditServiceAuthenticator authenticator,
        AuditEventService service,
        CancellationToken cancellationToken)
    {
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted service authentication is required.",
                "Audit events are accepted only from authenticated services.",
                "trusted_service_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            var auditId = await service.ConsumeAsync(request, cancellationToken);
            return Results.Json(
                new AuditAcceptedResponse("accepted", auditId),
                statusCode: StatusCodes.Status202Accepted);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return Problem(
                context,
                "Audit event is invalid.",
                "The event contract, producer, identifiers, or policy values are invalid.",
                "invalid_audit_event",
                StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> QueryAsync(
        HttpContext context,
        string correlationId,
        AuditGatewayAuthenticator authenticator,
        AuditEventService service,
        CancellationToken cancellationToken)
    {
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted gateway authentication is required.",
                "Audit queries are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        if (!authenticator.IsAdmin(context))
        {
            return Problem(
                context,
                "Admin access is required.",
                "Audit queries require the admin role.",
                "admin_role_required",
                StatusCodes.Status403Forbidden);
        }

        try
        {
            var records = await service.FindByCorrelationAsync(correlationId, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new AuditQueryResponse(
                correlationId,
                records.Select(ToResponse).ToArray(),
                "pseudonymous-audit-metadata-only"));
        }
        catch (ArgumentException)
        {
            return Problem(
                context,
                "Correlation identifier is invalid.",
                "Use a bounded safe correlation identifier.",
                "invalid_correlation_id",
                StatusCodes.Status400BadRequest);
        }
    }

    private static AuditRecordResponse ToResponse(AuditRecord record) =>
        new(
            record.AuditId,
            record.SourceEventId,
            record.OccurredAtUtc,
            record.RecordedAtUtc,
            record.Producer,
            record.CorrelationId,
            record.CausationId,
            record.SubjectIdHash,
            record.Domain,
            record.Action,
            record.Outcome,
            record.ResourceType,
            record.FailureCategory,
            record.RetentionClass,
            record.ExpiresAtUtc);

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
