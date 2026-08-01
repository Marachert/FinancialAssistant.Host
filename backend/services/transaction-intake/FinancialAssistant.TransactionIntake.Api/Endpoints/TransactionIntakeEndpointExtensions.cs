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
        MapReviewRoutes(app, TransactionIntakeApiRoutes.ReviewDraft, "TransactionDraft");
        MapReviewRoutes(app, TransactionIntakeApiRoutes.GatewayReviewDraft, "TransactionDraftFromGateway");
        MapConfirmRoute(app, TransactionIntakeApiRoutes.ConfirmDraft, "ConfirmTransactionDraft");
        MapConfirmRoute(app, TransactionIntakeApiRoutes.GatewayConfirmDraft, "ConfirmTransactionDraftFromGateway");
        MapRejectRoute(app, TransactionIntakeApiRoutes.RejectDraft, "RejectTransactionDraft");
        MapRejectRoute(app, TransactionIntakeApiRoutes.GatewayRejectDraft, "RejectTransactionDraftFromGateway");
        return app;
    }

    private static void MapReviewRoutes(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapGet(pattern, HandleReviewAsync)
            .WithName($"Review{name}")
            .Produces<TransactionDraftResponse>(StatusCodes.Status200OK)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPut(pattern, HandleUpdateAsync)
            .WithName($"Update{name}")
            .Produces<TransactionDraftResponse>(StatusCodes.Status200OK)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static void MapRejectRoute(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleRejectionAsync)
            .WithName(name)
            .Produces<TransactionDraftResponse>(StatusCodes.Status200OK)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status409Conflict);
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

    private static async Task<IResult> HandleReviewAsync(
        HttpContext httpContext,
        string draftId,
        ITransactionDraftReviewService reviewService,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = AuthenticateDraftRequest(
            httpContext,
            gatewayAuthenticator,
            out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var draft = await reviewService.ReviewAsync(userId!, draftId, cancellationToken);
            return draft is null
                ? DraftNotFound(httpContext)
                : Results.Ok(draft);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                httpContext,
                "Transaction draft review is invalid.",
                exception.Message,
                "invalid_transaction_draft_review",
                StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext httpContext,
        string draftId,
        TransactionDraftUpdateRequest request,
        ITransactionDraftReviewService reviewService,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = AuthenticateDraftRequest(
            httpContext,
            gatewayAuthenticator,
            out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var draft = await reviewService.UpdateAsync(
                userId!,
                draftId,
                request,
                cancellationToken);
            return draft is null
                ? DraftNotFound(httpContext)
                : Results.Ok(draft);
        }
        catch (DraftNotEditableException exception)
        {
            return DraftConflict(httpContext, exception.Message);
        }
        catch (DraftMutationConflictException exception)
        {
            return DraftConflict(httpContext, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                httpContext,
                "Transaction draft update is invalid.",
                exception.Message,
                "invalid_transaction_draft_update",
                StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HandleRejectionAsync(
        HttpContext httpContext,
        string draftId,
        ITransactionDraftReviewService reviewService,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = AuthenticateDraftRequest(
            httpContext,
            gatewayAuthenticator,
            out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var draft = await reviewService.RejectAsync(userId!, draftId, cancellationToken);
            return draft is null
                ? DraftNotFound(httpContext)
                : Results.Ok(draft);
        }
        catch (DraftNotEditableException exception)
        {
            return DraftConflict(httpContext, exception.Message);
        }
        catch (DraftMutationConflictException exception)
        {
            return DraftConflict(httpContext, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                httpContext,
                "Transaction draft rejection is invalid.",
                exception.Message,
                "invalid_transaction_draft_rejection",
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

    private static IResult? AuthenticateDraftRequest(
        HttpContext httpContext,
        TransactionIntakeGatewayAuthenticator gatewayAuthenticator,
        out string? userId)
    {
        userId = null;
        if (!gatewayAuthenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Transaction draft access is accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = GetHeader(httpContext, TransactionIntakeHeaders.GatewayUserId);
        if (userId is null)
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Transaction draft access requires a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static IResult DraftNotFound(HttpContext httpContext) =>
        Problem(
            httpContext,
            "Transaction draft was not found.",
            "The draft does not exist for the authenticated user.",
            "transaction_draft_not_found",
            StatusCodes.Status404NotFound);

    private static IResult DraftConflict(HttpContext httpContext, string detail) =>
        Problem(
            httpContext,
            "Transaction draft cannot be changed.",
            detail,
            "transaction_draft_not_editable",
            StatusCodes.Status409Conflict);

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
