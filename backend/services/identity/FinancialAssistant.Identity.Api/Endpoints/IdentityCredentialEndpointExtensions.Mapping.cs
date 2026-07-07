using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static partial class IdentityCredentialEndpointExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static RouteGroupBuilder MapIdentityCredentialEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(IdentityApiRoutes.RegisterRelative, RegisterAsync)
            .WithName("Identity_Register_v1")
            .WithSummary("Create an account and initial authenticated session")
            .Accepts<RegisterAccountRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status201Created)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson);

        group.MapPost(IdentityApiRoutes.SignInRelative, SignInAsync)
            .WithName("Identity_SignIn_v1")
            .WithSummary("Authenticate with email and password")
            .Accepts<SignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson);

        return group;
    }
}
