using System.Diagnostics;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace FinancialAssistant.Identity.Api.Endpoints;

public static class IdentityContractEndpointExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static IEndpointRouteBuilder MapIdentityContractEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup(IdentityApiRoutes.Base)
            .WithTags("Identity v1");

        group.MapPost(
                IdentityApiRoutes.RegisterRelative,
                ([FromBody] RegisterAccountRequest request,
                    [FromHeader(Name = IdentityApiHeaders.IdempotencyKey)] string? idempotencyKey,
                    HttpContext context) => ContractPlaceholder(context))
            .WithName("Identity_Register_v1")
            .WithSummary("Create an account and initial authenticated session")
            .WithDescription("Public contract only. The deterministic registration flow is implemented by FIN-75.")
            .Accepts<RegisterAccountRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status501NotImplemented, ProblemJson);

        group.MapPost(
                IdentityApiRoutes.SignInRelative,
                ([FromBody] SignInRequest request, HttpContext context) => ContractPlaceholder(context))
            .WithName("Identity_SignIn_v1")
            .WithSummary("Authenticate with email and password")
            .WithDescription("Returns the same generic authentication error for invalid identifiers and invalid passwords.")
            .Accepts<SignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status501NotImplemented, ProblemJson);

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

    private static IResult ContractPlaceholder(HttpContext context)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var response = new IdentityApiErrorResponse(
            "https://errors.financial-assistant.app/identity/not-implemented",
            "Identity operation is not active yet.",
            StatusCodes.Status501NotImplemented,
            IdentityErrorCodes.NotImplemented,
            "The versioned client contract is published, but the operation has not been activated.",
            traceId);

        return Results.Json(
            response,
            statusCode: StatusCodes.Status501NotImplemented,
            contentType: ProblemJson);
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
