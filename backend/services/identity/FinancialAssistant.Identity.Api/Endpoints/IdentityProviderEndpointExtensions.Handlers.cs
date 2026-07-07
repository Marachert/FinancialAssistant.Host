using System.Diagnostics;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentityProviderEndpointExtensions
{
    private static async Task<IResult> GoogleSignInAsync(
        [FromBody] GoogleSignInRequest request,
        HttpContext context,
        IGoogleProviderAuthenticationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.AuthenticateAsync(
            request,
            ResolveCorrelationId(context),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Failure!, context);
    }

    private static IResult ToProblem(
        IdentityOperationFailure failure,
        HttpContext context)
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
            ResolveCorrelationId(context),
            failure.Errors);

        return Results.Json(response, statusCode: status, contentType: ProblemJson);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[IdentityApiHeaders.CorrelationId].FirstOrDefault();
        return string.IsNullOrWhiteSpace(supplied)
            ? Activity.Current?.Id ?? context.TraceIdentifier
            : supplied;
    }
}
