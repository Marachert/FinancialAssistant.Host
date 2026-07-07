using System.Diagnostics;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace FinancialAssistant.Identity.Api.Endpoints;

public static class IdentityContractEndpointExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static IEndpointRouteBuilder MapIdentityContractEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(IdentityApiRoutes.Base).WithTags("Identity v1");

        group.MapPost(
                IdentityApiRoutes.RegisterRelative,
                RegisterAsync)
            .WithName("Identity_Register_v1")
            .WithSummary("Create an account and initial authenticated session")
            .WithDescription("Creates an email identity, stores only protected credential data, and emits user.registered.v1 through the event abstraction.")
            .Accepts<RegisterAccountRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson);

        group.MapPost(
                IdentityApiRoutes.SignInRelative,
                SignInAsync)
            .WithName("Identity_SignIn_v1")
            .WithSummary("Authenticate with email and password")
            .WithDescription("Returns the same generic authentication error for unknown identifiers and invalid credentials.")
            .Accepts<SignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson);

        group.MapPost(
                IdentityApiRoutes.RefreshRelative,
                ([FromBody] RefreshSessionRequest request, HttpContext context) => ContractPlaceholder(context))
            .WithName("Identity_Refresh_v1")
            .WithSummary("Rotate a refresh session and issue new tokens")
            .WithDescription("Refresh-token rotation and replay handling are implemented by FIN-76.")
            .Accepts<RefreshSessionRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status501NotImplemented, ProblemJson);

        group.MapPost(
                IdentityApiRoutes.LogoutRelative,
                ([FromBody] LogoutRequest request, HttpContext context) => ContractPlaceholder(context))
            .WithName("Identity_Logout_v1")
            .WithSummary("Revoke the current refresh session")
            .WithDescription("Requires a bearer access token and matching current refresh session when activated.")
            .Accepts<LogoutRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status501NotImplemented, ProblemJson)
            .WithBearerSecurityContract();

        group.MapGet(
                IdentityApiRoutes.CurrentUserRelative,
                (HttpContext context) => ContractPlaceholder(context))
            .WithName("Identity_CurrentUser_v1")
            .WithSummary("Get the current authenticated identity context")
            .WithDescription("Returns identity and session context only; profile and financial data belong to other services.")
            .Produces<CurrentUserContextResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status501NotImplemented, ProblemJson)
            .WithBearerSecurityContract();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterAccountRequest request,
        [FromHeader(Name = IdentityApiHeaders.IdempotencyKey)] string? idempotencyKey,
        HttpContext context,
        IIdentityAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RegisterAsync(
            request,
            idempotencyKey,
            ResolveCorrelationId(context),
            cancellationToken);

        return result.IsSuccess
            ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
            : ToProblem(result.Failure!, context);
    }

    private static async Task<IResult> SignInAsync(
        [FromBody] SignInRequest request,
        HttpContext context,
        IIdentityAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.SignInAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Failure!, context);
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
            ResolveCorrelationId(context),
            failure.Errors);

        return Results.Json(response, statusCode: status, contentType: ProblemJson);
    }

    private static IResult ContractPlaceholder(HttpContext context)
    {
        var response = new IdentityApiErrorResponse(
            "https://errors.financial-assistant.app/identity/not-implemented",
            "Identity operation is not active yet.",
            StatusCodes.Status501NotImplemented,
            IdentityErrorCodes.NotImplemented,
            "The versioned client contract is published, but the operation has not been activated.",
            ResolveCorrelationId(context));

        return Results.Json(response, statusCode: StatusCodes.Status501NotImplemented, contentType: ProblemJson);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[IdentityApiHeaders.CorrelationId].FirstOrDefault();
        return string.IsNullOrWhiteSpace(supplied)
            ? Activity.Current?.Id ?? context.TraceIdentifier
            : supplied;
    }

    private static RouteHandlerBuilder WithBearerSecurityContract(this RouteHandlerBuilder builder)
    {
        return builder.WithOpenApi(operation =>
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                }
            };

            return operation;
        });
    }
}
