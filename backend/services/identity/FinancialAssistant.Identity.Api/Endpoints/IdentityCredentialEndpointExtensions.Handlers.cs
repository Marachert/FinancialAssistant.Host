using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentityCredentialEndpointExtensions
{
    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterAccountRequest request,
        [FromHeader(Name = IdentityApiHeaders.IdempotencyKey)] string? idempotencyKey,
        HttpContext context,
        IIdentityAuthenticationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(
            request,
            idempotencyKey,
            IdentityCorrelationId.Resolve(context),
            cancellationToken);

        return result.IsSuccess
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : ToProblem(result.Failure!, context);
    }

    private static async Task<IResult> SignInAsync(
        [FromBody] SignInRequest request,
        HttpContext context,
        IIdentityAuthenticationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SignInAsync(
            request,
            IdentityCorrelationId.Resolve(context),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Failure!, context);
    }

    private static IResult ToProblem(IdentityOperationFailure failure, HttpContext context)
    {
        var status = failure.Kind switch
        {
            IdentityFailureKind.Validation => StatusCodes.Status400BadRequest,
            IdentityFailureKind.Conflict => StatusCodes.Status409Conflict,
            IdentityFailureKind.Authentication => StatusCodes.Status401Unauthorized,
            IdentityFailureKind.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        var response = new IdentityApiErrorResponse(
            $"https://errors.financial-assistant.app/identity/{failure.Code.Replace('_', '-')}",
            failure.Title,
            status,
            failure.Code,
            failure.Detail,
            IdentityCorrelationId.Resolve(context),
            failure.Errors);

        return Results.Json(response, statusCode: status, contentType: ProblemJson);
    }
}
