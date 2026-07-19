using FinancialAssistant.TransactionIntake.Api.Security;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.TransactionIntake.Api.Endpoints;

public static class TransactionIntakeEndpointExtensions
{
    public static IEndpointRouteBuilder MapTransactionIntakeEndpoints(this IEndpointRouteBuilder app)
    {
        MapIntakeRoute(app, TransactionIntakeApiRoutes.Intake, "CreateTransactionDraft");
        MapIntakeRoute(app, TransactionIntakeApiRoutes.GatewayIntake, "CreateTransactionDraftFromGateway");
        MapConfirmRoute(app, TransactionIntakeApiRoutes.ConfirmDraft, "ConfirmTransactionDraft");
        MapConfirmRoute(app, TransactionIntakeApiRoutes.GatewayConfirmDraft, "ConfirmTransactionDraftFromGateway");
        return app;
    }

    private static void MapConfirmRoute(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleConfirmationAsync)
            .WithName(name)
            .Produces<ConfirmedTransactionResponse>(StatusCodes.Status201Created)
            .Produces<ConfirmedTransactionResponse>(StatusCodes.Status200OK)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapIntakeRoute(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleIntakeAsync)
            .WithName(name)
            .Produces<TransactionDraftResponse>(StatusCodes.Status201Created)
            .Produces<TransactionDraftResponse>(StatusCodes.Status200OK)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleIntakeAsync(
        HttpContext httpContext,
        TransactionIntakeRequest request,
        [FromHeader(Name = TransactionIntakeHeaders.IdempotencyKey)] string? idempotencyKey,
        ITransactionIntakeService intakeService,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        CancellationToken cancellationToken)
    {
        if (!gatewayAuthenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Transaction intake is accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        var userId = GetHeader(httpContext, TransactionIntakeHeaders.GatewayUserId);
        if (userId is null)
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Transaction intake requires a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        if (idempotencyKey is null)
        {
            return Problem(
                httpContext,
                "Idempotency key is required.",
                "Supply an opaque Idempotency-Key header when creating a draft.",
                "idempotency_key_required",
                StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await intakeService.CreateDraftAsync(
                userId,
                idempotencyKey,
                request,
                cancellationToken);
            return result.Replayed
                ? Results.Ok(result.Draft)
                : Results.Created($"/api/v1/transactions/drafts/{result.Draft.Id}", result.Draft);
        }
        catch (IdempotencyConflictException exception)
        {
            return Problem(
                httpContext,
                "Idempotency key conflict.",
                exception.Message,
                "idempotency_key_conflict",
                StatusCodes.Status409Conflict);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                httpContext,
                "Transaction input is invalid.",
                exception.Message,
                "invalid_transaction_input",
                StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HandleConfirmationAsync(
        HttpContext httpContext,
        string draftId,
        ITransactionConfirmationService confirmationService,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        CancellationToken cancellationToken)
    {
        if (!gatewayAuthenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Transaction confirmation is accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        var userId = GetHeader(httpContext, TransactionIntakeHeaders.GatewayUserId);
        if (userId is null)
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Transaction confirmation requires a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await confirmationService.ConfirmAsync(
                userId,
                draftId,
                GetCorrelationId(httpContext),
                cancellationToken);
            if (result is null)
            {
                return Problem(
                    httpContext,
                    "Transaction draft was not found.",
                    "The draft does not exist for the authenticated user.",
                    "transaction_draft_not_found",
                    StatusCodes.Status404NotFound);
            }

            return result.Replayed
                ? Results.Ok(result.Transaction)
                : Results.Created(
                    $"/api/v1/transactions/{result.Transaction.TransactionId}",
                    result.Transaction);
        }
        catch (DraftNotConfirmableException exception)
        {
            return Problem(
                httpContext,
                "Transaction draft cannot be confirmed.",
                exception.Message,
                "transaction_draft_not_confirmable",
                StatusCodes.Status422UnprocessableEntity);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                httpContext,
                "Transaction confirmation is invalid.",
                exception.Message,
                "invalid_transaction_confirmation",
                StatusCodes.Status400BadRequest);
        }
    }

    private static string? GetHeader(HttpContext httpContext, string name)
    {
        var value = httpContext.Request.Headers[name].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? GetCorrelationId(HttpContext httpContext) =>
        GetHeader(httpContext, "correlationId") ?? GetHeader(httpContext, "X-Correlation-Id");

    private static IResult Problem(
        HttpContext httpContext,
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
                ["traceId"] = httpContext.TraceIdentifier
            });
}
