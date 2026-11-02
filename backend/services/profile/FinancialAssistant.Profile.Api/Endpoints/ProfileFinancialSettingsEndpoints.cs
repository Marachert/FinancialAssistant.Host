using FinancialAssistant.Profile.Api.Security;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Profile.Infrastructure.Storage;

namespace FinancialAssistant.Profile.Api.Endpoints;

public static class ProfileFinancialSettingsEndpoints
{
    public static IEndpointRouteBuilder MapNativeProfileFinancialSettings(this IEndpointRouteBuilder app)
    {
        app.MapPost(ProfileFinancialSettingsApi.Path, async (HttpContext context, ProfileFinancialSettingsQuery query,
            ProfileFinancialReaderAuthenticator authentication, PostgresProfileStore store, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!authentication.IsAuthenticated(context)) { return Results.StatusCode(StatusCodes.Status401Unauthorized); }
            try
            {
                var snapshot = await store.GetFinancialSettingsAsync(query.OwnerHash, DateTimeOffset.UtcNow, token);
                return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
            }
            catch (ArgumentException) { return Results.BadRequest(); }
        }).WithName("ReadNativeProfileFinancialSettings").Produces<ProfileFinancialSettingsSnapshot>()
            .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status404NotFound);
        return app;
    }
}
