using FinancialAssistant.Profile.Domain.Profiles;

namespace FinancialAssistant.Profile.Tests;

public sealed class ProfileRehydrationTests
{
    [Fact]
    public void RehydrationPreservesEveryPreferenceDecimalAndTimestamp()
    {
        var created = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        var updated = created.AddMinutes(5);
        var preferences = UserProfilePreferences.Create("en-US", "UTC", "USD", "strict", true, "sunday",
            12345678901234567890.12345678m, true, true, true, true);
        var profile = UserProfile.Rehydrate("synthetic-owner", preferences, created, updated);
        Assert.Equal("synthetic-owner", profile.UserId);
        Assert.Same(preferences, profile.Preferences);
        Assert.Equal(12345678901234567890.12345678m, profile.Preferences.MonthlyBudgetAmount);
        Assert.Equal(created, profile.CreatedAtUtc);
        Assert.Equal(updated, profile.UpdatedAtUtc);
        Assert.True(profile.Preferences.ProfileOnboardingCompleted);
        Assert.True(profile.Preferences.PreferencesOnboardingCompleted);
        Assert.Throws<ArgumentException>(() => UserProfile.Rehydrate(" other ", preferences, created, updated));
        Assert.Throws<ArgumentException>(() => UserProfile.Rehydrate("synthetic-owner", preferences, updated, created));
    }
}
