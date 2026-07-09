using FinancialAssistant.Profile.Application.Profiles;
using FinancialAssistant.Profile.Contracts;

namespace FinancialAssistant.Profile.Api.Endpoints;

public static class ProfileEndpointExtensions
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                ProfileApiRoutes.UserRegisteredEvent,
                async (
                    UserRegisteredProfileEvent integrationEvent,
                    IUserProfileService profileService,
                    CancellationToken cancellationToken) =>
                {
                    var profile = await profileService.CreateFromRegisteredUserAsync(
                        integrationEvent,
                        cancellationToken);

                    return Results.Accepted(ProfileApiRoutes.CurrentProfile, profile);
                })
            .WithName("CreateProfileFromUserRegistered")
            .Produces<UserProfileResponse>(StatusCodes.Status202Accepted)
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status400BadRequest);

        app.MapGet(
                ProfileApiRoutes.CurrentProfile,
                async (
                    HttpContext httpContext,
                    IUserProfileService profileService,
                    CancellationToken cancellationToken) =>
                {
                    var userId = GetGatewayUserId(httpContext);
                    if (userId is null)
                    {
                        return MissingGatewayUser(httpContext);
                    }

                    var profile = await profileService.GetCurrentAsync(userId, cancellationToken);
                    return profile is null
                        ? Results.Problem(
                            title: "Profile was not found.",
                            detail: "The authenticated user does not have a profile yet.",
                            statusCode: StatusCodes.Status404NotFound,
                            extensions: ErrorExtensions("profile_not_found", httpContext))
                        : Results.Ok(profile);
                })
            .WithName("GetCurrentProfile")
            .Produces<UserProfileResponse>()
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPut(
                ProfileApiRoutes.CurrentProfilePreferences,
                async (
                    HttpContext httpContext,
                    UpdateUserPreferencesRequest request,
                    IUserProfileService profileService,
                    CancellationToken cancellationToken) =>
                {
                    var userId = GetGatewayUserId(httpContext);
                    if (userId is null)
                    {
                        return MissingGatewayUser(httpContext);
                    }

                    try
                    {
                        var profile = await profileService.UpdatePreferencesAsync(
                            userId,
                            request,
                            cancellationToken);

                        return profile is null
                            ? Results.Problem(
                                title: "Profile was not found.",
                                detail: "Preferences can only be updated after profile creation.",
                                statusCode: StatusCodes.Status404NotFound,
                                extensions: ErrorExtensions("profile_not_found", httpContext))
                            : Results.Ok(profile);
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.Problem(
                            title: "Profile preferences are invalid.",
                            detail: exception.Message,
                            statusCode: StatusCodes.Status400BadRequest,
                            extensions: ErrorExtensions("invalid_preferences", httpContext));
                    }
                })
            .WithName("UpdateCurrentProfilePreferences")
            .Produces<UserProfileResponse>()
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ProfileApiErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static string? GetGatewayUserId(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers[ProfileGatewayHeaders.UserId].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IResult MissingGatewayUser(HttpContext httpContext) =>
        Results.Problem(
            title: "Authentication is required.",
            detail: "Profile requests must be forwarded with a trusted gateway user context.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: ErrorExtensions("authentication_required", httpContext));

    private static Dictionary<string, object?> ErrorExtensions(string code, HttpContext httpContext) =>
        new(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["traceId"] = httpContext.TraceIdentifier
        };
}
