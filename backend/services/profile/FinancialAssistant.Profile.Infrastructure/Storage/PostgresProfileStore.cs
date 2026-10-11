using System.Text.Json.Serialization;
using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Domain.Profiles;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Profile.Infrastructure.Storage;

public sealed class PostgresProfileStore : IProfileStore
{
    private readonly OwnedPostgresStore database;
    public PostgresProfileStore(OwnedPostgresStore database)
    {
        if (database.ServiceId != "profile") { throw new ArgumentException("Profile requires its owned storage role."); }
        this.database = database;
    }
    public Task<UserProfile> CreateDefaultIfMissingAsync(UserProfile profile, CancellationToken cancellationToken) =>
        database.ExecuteAsync(new[] { Owner(profile.UserId) }, async (records, _) =>
        {
            var owner = Owner(profile.UserId);
            if (await records.GetAsync("deleted_profiles", owner, "marker") is not null) { throw new StorageUnavailableException(); }
            var existing = await records.GetAsync("profiles", owner, "profile");
            if (existing is not null) { return Restore(existing, owner); }
            await records.InsertAsync("profiles", owner, "profile", Snapshot(profile));
            return profile;
        }, cancellationToken);

    public Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken) =>
        database.ExecuteAsync(new[] { Owner(userId) }, async (records, _) =>
        {
            if (await records.GetAsync("deleted_profiles", userId, "marker") is not null) { return null; }
            var existing = await records.GetAsync("profiles", userId, "profile");
            return existing is null ? null : Restore(existing, userId);
        }, cancellationToken);

    public Task<UserProfile?> UpdateAsync(string userId, Func<UserProfile, UserProfile> update, CancellationToken cancellationToken) =>
        database.ExecuteAsync(new[] { Owner(userId) }, async (records, _) =>
        {
            if (await records.GetAsync("deleted_profiles", userId, "marker") is not null) { return null; }
            var existing = await records.GetAsync("profiles", userId, "profile");
            if (existing is null) { return null; }
            var current = Restore(existing, userId);
            var updated = update(current);
            if (updated is null || updated.UserId != userId || updated.CreatedAtUtc != current.CreatedAtUtc)
            { throw new StorageUnavailableException(); }
            await records.ReplaceAsync("profiles", userId, "profile", existing.Revision, Snapshot(updated));
            return updated;
        }, cancellationToken);

    public Task<bool> DeleteAsync(string userId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        database.ExecuteAsync(new[] { Owner(userId) }, async (records, _) =>
        {
            var existing = await records.GetAsync("profiles", userId, "profile");
            if (existing is not null) { await records.DeleteAsync("profiles", userId, "profile", existing.Revision); }
            if (await records.GetAsync("deleted_profiles", userId, "marker") is null)
            { await records.InsertAsync("deleted_profiles", userId, "marker", OwnedRecordCodec.Encode(1, new DeletionMarker(nowUtc))); }
            return existing is not null;
        }, cancellationToken);

    private static string Owner(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value != value.Trim() || value.Any(char.IsControl))
        { throw new ArgumentException("Invalid profile owner."); }
        return value;
    }

    private static byte[] Snapshot(UserProfile profile)
    {
        UserProfile.Rehydrate(profile.UserId, profile.Preferences, profile.CreatedAtUtc, profile.UpdatedAtUtc);
        var p = profile.Preferences;
        return OwnedRecordCodec.Encode(1, new ProfileState(profile.UserId,
            new PreferencesState(p.Locale, p.TimeZone, p.CurrencyCode, p.PrivacyMode, p.AiPersonalizationEnabled,
                p.FirstDayOfWeek, p.MonthlyBudgetAmount, p.BudgetNotificationsEnabled, p.WeeklySummaryNotificationsEnabled,
                p.ProfileOnboardingCompleted, p.PreferencesOnboardingCompleted), profile.CreatedAtUtc, profile.UpdatedAtUtc));
    }

    private static UserProfile Restore(DurableRecord record, string owner)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<ProfileState>(1, record);
            if (state.UserId != owner || state.Preferences is null) { throw new StorageUnavailableException(); }
            var p = state.Preferences;
            var preferences = UserProfilePreferences.Create(p.Locale, p.TimeZone, p.CurrencyCode, p.PrivacyMode,
                p.AiPersonalizationEnabled, p.FirstDayOfWeek, p.MonthlyBudgetAmount, p.BudgetNotificationsEnabled,
                p.WeeklySummaryNotificationsEnabled, p.ProfileOnboardingCompleted, p.PreferencesOnboardingCompleted);
            if (p.Locale != preferences.Locale || p.TimeZone != preferences.TimeZone || p.CurrencyCode != preferences.CurrencyCode
                || p.PrivacyMode != preferences.PrivacyMode || p.FirstDayOfWeek != preferences.FirstDayOfWeek)
            { throw new StorageUnavailableException(); }
            return UserProfile.Rehydrate(state.UserId, preferences, state.CreatedAtUtc, state.UpdatedAtUtc);
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record ProfileState([property: JsonRequired] string UserId,
        [property: JsonRequired] PreferencesState Preferences, [property: JsonRequired] DateTimeOffset CreatedAtUtc,
        [property: JsonRequired] DateTimeOffset UpdatedAtUtc);
    private sealed record PreferencesState([property: JsonRequired] string Locale, [property: JsonRequired] string TimeZone,
        [property: JsonRequired] string CurrencyCode, [property: JsonRequired] string PrivacyMode,
        [property: JsonRequired] bool AiPersonalizationEnabled, [property: JsonRequired] string FirstDayOfWeek,
        [property: JsonRequired] decimal MonthlyBudgetAmount, [property: JsonRequired] bool BudgetNotificationsEnabled,
        [property: JsonRequired] bool WeeklySummaryNotificationsEnabled, [property: JsonRequired] bool ProfileOnboardingCompleted,
        [property: JsonRequired] bool PreferencesOnboardingCompleted);
    private sealed record DeletionMarker(DateTimeOffset DeletedAtUtc);
}
