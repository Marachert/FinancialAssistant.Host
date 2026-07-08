using FinancialAssistant.Identity.Api.RateLimiting;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.OpenApi.Models;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentitySessionEndpointExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static RouteGroupBuilder MapIdentitySessionLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(IdentityApiRoutes.RefreshRelative, RefreshAsync)
            .WithName("Identity_Refresh_v1")
            .WithSummary("Rotate a refresh session and issue new tokens")
            .Accepts<RefreshSessionRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .RequireRateLimiting(IdentityRateLimitPolicies.Session);

        group.MapPost(IdentityApiRoutes.LogoutRelative, LogoutAsync)
            .WithName("Identity_Logout_v1")
            .WithSummary("Revoke the current session")
            .Accepts<LogoutRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .WithBearerSecurityContract()
            .RequireAuthorization()
            .RequireRateLimiting(IdentityRateLimitPolicies.Session);

        group.MapGet(IdentityApiRoutes.CurrentUserRelative, CurrentAsync)
            .WithName("Identity_CurrentUser_v1")
            .WithSummary("Get the current authenticated identity context")
            .Produces<CurrentUserContextResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .WithBearerSecurityContract()
            .RequireAuthorization()
            .RequireRateLimiting(IdentityRateLimitPolicies.Session);

        return group;
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
