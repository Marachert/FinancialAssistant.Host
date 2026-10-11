namespace FinancialAssistant.Profile.Domain.Profiles;

public sealed record UserProfile
{
    private UserProfile(
        string userId,
        UserProfilePreferences preferences,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        UserId = userId;
        Preferences = preferences;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string UserId { get; }

    public UserProfilePreferences Preferences { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public static UserProfile CreateDefault(string userId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        return new UserProfile(
            userId.Trim(),
            UserProfilePreferences.Default(),
            createdAtUtc,
            createdAtUtc);
    }

    public UserProfile UpdatePreferences(UserProfilePreferences preferences, DateTimeOffset updatedAtUtc) =>
        new(UserId, preferences, CreatedAtUtc, updatedAtUtc);

    public static UserProfile Rehydrate(string userId, UserProfilePreferences preferences,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > 128 || userId != userId.Trim()
            || userId.Any(char.IsControl) || preferences is null || updatedAtUtc < createdAtUtc)
        { throw new ArgumentException("Stored profile is invalid."); }
        return new UserProfile(userId, preferences, createdAtUtc, updatedAtUtc);
    }
}
