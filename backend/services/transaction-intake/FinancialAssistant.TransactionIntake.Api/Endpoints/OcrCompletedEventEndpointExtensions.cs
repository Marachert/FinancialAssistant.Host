using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.TransactionIntake.Api.Security;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Api.Endpoints;

public static class OcrCompletedEventEndpointExtensions
{
    public static IEndpointRouteBuilder MapOcrCompletedEventEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost(
                ReceiptProcessingApiRoutes.OcrCompletedEvent,
                HandleAsync)
            .WithName("ConsumeOcrCompletedEvent")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<TransactionIntakeErrorResponse>(StatusCodes.Status409Conflict);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        OcrCompletedIntegrationEvent integrationEvent,
        IOcrCompletedConsumer consumer,
        ReceiptEventAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        if (!authenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Receipt event authentication is required.",
                "OCR completion events are accepted only from Receipt Processing.",
                "receipt_event_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        try
        {
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
            return Results.NoContent();
        }
        catch (OcrCompletedDraftConflictException exception)
        {
            return Problem(
                httpContext,
                "OCR completion conflicts with the stored draft.",
                exception.Message,
                "ocr_completed_conflict",
                StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                httpContext,
                "OCR completion event is invalid.",
                exception.Message,
                "invalid_ocr_completed_event",
                StatusCodes.Status400BadRequest);
        }
    }

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
