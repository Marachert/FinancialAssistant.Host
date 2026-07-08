using FinancialAssistant.Identity.Api.RateLimiting;
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
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson)
            .RequireRateLimiting(IdentityRateLimitPolicies.ProviderSignIn);

        group.MapPost(IdentityApiRoutes.AppleSignInRelative, AppleSignInAsync)
            .WithName("Identity_AppleSignIn_v1")
            .WithSummary("Authenticate with Apple")
            .WithDescription("Validates an Apple identity token and nonce, then returns an Identity Service session.")
            .Accepts<AppleSignInRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status409Conflict, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson)
            .RequireRateLimiting(IdentityRateLimitPolicies.ProviderSignIn);

        group.MapPost(IdentityApiRoutes.PhoneVerificationStartRelative, PhoneVerificationStartAsync)
            .WithName("Identity_PhoneVerificationStart_v1")
            .WithSummary("Request a phone verification message")
            .WithDescription("Reserves a rate-limited phone challenge and dispatches it through the configured provider boundary.")
            .Accepts<PhoneVerificationStartRequest>("application/json")
            .Produces<PhoneVerificationStartResponse>(StatusCodes.Status202Accepted)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson)
            .RequireRateLimiting(IdentityRateLimitPolicies.PhoneStart);

        group.MapPost(IdentityApiRoutes.PhoneVerificationConfirmRelative, PhoneVerificationConfirmAsync)
            .WithName("Identity_PhoneVerificationConfirm_v1")
            .WithSummary("Confirm a phone verification code")
            .WithDescription("Checks a provider-held verification code and returns an Identity Service session when approved.")
            .Accepts<PhoneVerificationConfirmRequest>("application/json")
            .Produces<AuthSessionResponse>(StatusCodes.Status200OK)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status400BadRequest, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status401Unauthorized, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status429TooManyRequests, ProblemJson)
            .Produces<IdentityApiErrorResponse>(StatusCodes.Status503ServiceUnavailable, ProblemJson)
            .RequireRateLimiting(IdentityRateLimitPolicies.PhoneConfirm);

        return group;
    }
}
