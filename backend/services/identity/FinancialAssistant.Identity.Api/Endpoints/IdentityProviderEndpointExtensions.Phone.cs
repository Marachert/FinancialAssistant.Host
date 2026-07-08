using System.Globalization;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentityProviderEndpointExtensions
{
    private static async Task<IResult> PhoneVerificationStartAsync(
        [FromBody] PhoneVerificationStartRequest request,
        HttpContext context,
        IPhoneVerificationAuthenticationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.StartAsync(
            request,
            ResolveCorrelationId(context),
            cancellationToken);
        return result.IsSuccess
            ? Results.Accepted(IdentityApiRoutes.PhoneVerificationStart, result.Value)
            : ToPhoneProblem(result.Failure!, context);
    }

    private static async Task<IResult> PhoneVerificationConfirmAsync(
        [FromBody] PhoneVerificationConfirmRequest request,
        HttpContext context,
        IPhoneVerificationAuthenticationService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmAsync(
            request,
            ResolveCorrelationId(context),
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToPhoneProblem(result.Failure!, context);
    }

    private static IResult ToPhoneProblem(
        IdentityOperationFailure failure,
        HttpContext context)
    {
        var status = failure.Kind switch
        {
            IdentityFailureKind.Validation => StatusCodes.Status400BadRequest,
            IdentityFailureKind.Authentication => StatusCodes.Status401Unauthorized,
            IdentityFailureKind.RateLimited => StatusCodes.Status429TooManyRequests,
            IdentityFailureKind.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        if (failure.RetryAfterSeconds is > 0)
        {
            context.Response.Headers.RetryAfter = failure.RetryAfterSeconds.Value
                .ToString(CultureInfo.InvariantCulture);
        }

        var response = new IdentityApiErrorResponse(
            $"https://errors.financial-assistant.app/identity/{failure.Code.Replace('_', '-')}",
            failure.Title,
            status,
            failure.Code,
            failure.Detail,
            ResolveCorrelationId(context),
            failure.Errors,
            failure.RetryAfterSeconds);
        return Results.Json(response, statusCode: status, contentType: ProblemJson);
    }
}
