using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentitySessionEndpointExtensions
{
    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshSessionRequest request,
        HttpContext context,
        IIdentitySessionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RefreshAsync(
            request,
            IdentityCorrelationId.Resolve(context),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Failure!, context);
    }

    private static async Task<IResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        HttpContext context,
        IIdentitySessionService service,
        CancellationToken cancellationToken)
    {
        if (!AccessSessionContextFactory.TryCreate(context.User, out var access))
        {
            return UnauthorizedProblem(context);
        }

        var result = await service.LogoutAsync(
            request,
            access,
            IdentityCorrelationId.Resolve(context),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : ToProblem(result.Failure!, context);
    }

    private static async Task<IResult> CurrentAsync(
        HttpContext context,
        IIdentitySessionService service,
        CancellationToken cancellationToken)
    {
        if (!AccessSessionContextFactory.TryCreate(context.User, out var access))
        {
            return UnauthorizedProblem(context);
        }

        var result = await service.GetCurrentAsync(access, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Failure!, context);
    }

    private static IResult UnauthorizedProblem(HttpContext context)
    {
        var failure = new IdentityOperationFailure(
            IdentityFailureKind.Authentication,
            IdentityErrorCodes.SessionInvalid,
            "Session operation failed.",
            "The current session is not valid.",
            null);
        return ToProblem(failure, context);
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
