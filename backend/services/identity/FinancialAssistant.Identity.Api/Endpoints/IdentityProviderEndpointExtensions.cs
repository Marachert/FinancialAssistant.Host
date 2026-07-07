using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentityProviderEndpointExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static RouteGroupBuilder MapIdentityProviderEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(IdentityApiRoutes.GoogleSignInRelative, GoogleSignInAsync)
            .WithName("Identity_GoogleSignIn_v1")
            .WithSummary("Authenticate with a Google ID token")
            .WithDescription("Validates a Google-issued ID token server-side and returns an Identity Service session.")
            .Accepts<GoogleSignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson);

        group.MapPost(IdentityApiRoutes.AppleSignInRelative, AppleSignInAsync)
            .WithName("Identity_AppleSignIn_v1")
            .WithSummary("Authenticate with Apple")
            .WithDescription("Validates an Apple identity token and nonce, then returns an Identity Service session.")
            .Accepts<AppleSignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson);

        return group;
    }
}
