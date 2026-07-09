using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Profile.Domain.Profiles;

namespace FinancialAssistant.Profile.Application.Profiles;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IProfileStore store;
    private readonly IProfileClock clock;

    public UserProfileService(IProfileStore store, IProfileClock clock)
    {
        this.store = store;
        this.clock = clock;
    }

    public async Task<UserProfileResponse> CreateFromRegisteredUserAsync(
        UserRegisteredProfileEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var createdAt = integrationEvent.OccurredAtUtc == default
            ? clock.UtcNow
            : integrationEvent.OccurredAtUtc.ToUniversalTime();
        var profile = UserProfile.CreateDefault(integrationEvent.UserId, createdAt);
        var stored = await store.CreateDefaultIfMissingAsync(profile, cancellationToken);

        return ToResponse(stored);
    }

    public async Task<UserProfileResponse?> GetCurrentAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await store.GetAsync(NormalizeUserId(userId), cancellationToken);
        return profile is null ? null : ToResponse(profile);
    }

    public async Task<UserProfileResponse?> UpdatePreferencesAsync(
        string userId,
        UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeUserId(userId);
        var updated = await store.UpdateAsync(
            normalizedUserId,
            existing =>
            {
                var preferences = UserProfilePreferences.Create(
                    request.Locale ?? existing.Preferences.Locale,
                    request.TimeZone ?? existing.Preferences.TimeZone,
                    request.CurrencyCode ?? existing.Preferences.CurrencyCode,
                    request.PrivacyMode ?? existing.Preferences.PrivacyMode,
                    request.AiPersonalizationEnabled ?? existing.Preferences.AiPersonalizationEnabled);

                return existing.UpdatePreferences(preferences, clock.UtcNow);
            },
            cancellationToken);

        return updated is null ? null : ToResponse(updated);
    }

    private static string NormalizeUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        return userId.Trim();
    }

    private static UserProfileResponse ToResponse(UserProfile profile) =>
        new(
            profile.UserId,
            profile.Preferences.Locale,
            profile.Preferences.TimeZone,
            profile.Preferences.CurrencyCode,
            profile.Preferences.PrivacyMode,
            profile.Preferences.AiPersonalizationEnabled,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
}
