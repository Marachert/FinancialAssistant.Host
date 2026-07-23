using FinancialAssistant.ReceiptProcessing.Api.Security;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.ReceiptProcessing.Api.Endpoints;

public static class ReceiptEndpointExtensions
{
    public static IEndpointRouteBuilder MapReceiptProcessingEndpoints(this IEndpointRouteBuilder app)
    {
        MapUpload(app, ReceiptProcessingApiRoutes.Upload, "UploadReceipt");
        MapUpload(app, ReceiptProcessingApiRoutes.GatewayUpload, "UploadReceiptFromGateway");
        MapGet(app, ReceiptProcessingApiRoutes.Get, "GetReceipt");
        MapGet(app, ReceiptProcessingApiRoutes.GatewayGet, "GetReceiptFromGateway");
        return app;
    }

    private static void MapUpload(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleUploadAsync)
            .WithName(name)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ReceiptResponse>(StatusCodes.Status201Created)
            .Produces<ReceiptResponse>(StatusCodes.Status200OK)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status415UnsupportedMediaType);
    }

    private static void MapGet(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapGet(pattern, HandleGetAsync)
            .WithName(name)
            .Produces<ReceiptResponse>()
            .Produces<ReceiptErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ReceiptErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleUploadAsync(
        HttpContext context,
        [FromHeader(Name = ReceiptProcessingHeaders.IdempotencyKey)] string? idempotencyKey,
        IReceiptProcessingService service,
        ReceiptGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authorizationError = Authorize(context, authenticator);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        if (idempotencyKey is null)
        {
            return Problem(
                context,
                "Idempotency key is required.",
                "Supply an opaque Idempotency-Key header when uploading a receipt.",
                "idempotency_key_required",
                StatusCodes.Status400BadRequest);
        }

        if (!context.Request.HasFormContentType)
        {
            return Problem(
                context,
                "Multipart form data is required.",
                "Upload one receipt image in the 'file' form field.",
                "multipart_form_required",
                StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null || form.Files.Count != 1)
            {
                return Problem(
                    context,
                    "Exactly one receipt image is required.",
                    "Upload one receipt image in the 'file' form field.",
                    "receipt_file_required",
                    StatusCodes.Status400BadRequest);
            }

            if (file.Length > ReceiptProcessingService.MaximumReceiptSizeBytes)
            {
                return Problem(
                    context,
                    "Receipt image is too large.",
                    $"Receipt images cannot exceed {ReceiptProcessingService.MaximumReceiptSizeBytes} bytes.",
                    "receipt_too_large",
                    StatusCodes.Status413PayloadTooLarge);
            }

            await using var stream = file.OpenReadStream();
            var result = await service.UploadAsync(
                GetUserId(context)!,
                idempotencyKey,
                file.ContentType,
                stream,
                cancellationToken);
            return result.Replayed
                ? Results.Ok(result.Receipt)
                : Results.Created(
                    $"/api/v1/receipts/{result.Receipt.ReceiptId}",
                    result.Receipt);
        }
        catch (ReceiptIdempotencyConflictException exception)
        {
            return Problem(
                context,
                "Idempotency key conflict.",
                exception.Message,
                "idempotency_key_conflict",
                StatusCodes.Status409Conflict);
        }
        catch (ReceiptFileTooLargeException exception)
        {
            return Problem(
                context,
                "Receipt image is too large.",
                exception.Message,
                "receipt_too_large",
                StatusCodes.Status413PayloadTooLarge);
        }
        catch (UnsupportedReceiptMediaTypeException exception)
        {
            return Problem(
                context,
                "Receipt media type is unsupported.",
                exception.Message,
                "unsupported_receipt_media_type",
                StatusCodes.Status415UnsupportedMediaType);
        }
        catch (InvalidDataException)
        {
            return Problem(
                context,
                "Receipt upload is invalid.",
                "The multipart receipt upload could not be read.",
                "invalid_receipt_upload",
                StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                context,
                "Receipt upload is invalid.",
                exception.Message,
                "invalid_receipt_upload",
                StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        string receiptId,
        IReceiptProcessingService service,
        ReceiptGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authorizationError = Authorize(context, authenticator);
        if (authorizationError is not null)
        {
            return authorizationError;
        }

        try
        {
            var receipt = await service.GetAsync(
                GetUserId(context)!,
                receiptId,
                cancellationToken);
            return receipt is null
                ? Problem(
                    context,
                    "Receipt was not found.",
                    "The receipt does not exist for the authenticated user.",
                    "receipt_not_found",
                    StatusCodes.Status404NotFound)
                : Results.Ok(receipt);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                context,
                "Receipt identifier is invalid.",
                exception.Message,
                "invalid_receipt_id",
                StatusCodes.Status400BadRequest);
        }
    }

    private static IResult? Authorize(
        HttpContext context,
        ReceiptGatewayAuthenticator authenticator)
    {
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted gateway authentication is required.",
                "Receipt processing is accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return GetUserId(context) is null
            ? Problem(
                context,
                "Authentication is required.",
                "Receipt processing requires a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized)
            : null;
    }

    private static string? GetUserId(HttpContext context)
    {
        var value = context.Request.Headers[ReceiptProcessingHeaders.GatewayUserId].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
