using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class InMemoryNotificationPreferenceProvider :
    INotificationPreferenceProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, NotificationPreferences> preferences =
        new(StringComparer.Ordinal);

    public Task<NotificationPreferences> GetAsync(
        string userIdHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = NormalizeOwner(userIdHash);
        lock (gate)
        {
            return Task.FromResult(
                preferences.TryGetValue(owner, out var value)
                    ? value
                    : NotificationPreferences.AllEnabled);
        }
    }

    public Task<NotificationPreferences> UpdateAsync(
        string userIdHash,
        NotificationPreferences value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            preferences[NormalizeOwner(userIdHash)] = value;
            return Task.FromResult(value);
        }
    }

    public void Set(
        string userIdHash,
        NotificationPreferences value)
    {
        UpdateAsync(userIdHash, value, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static string NormalizeOwner(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Owner hash is required.", nameof(value))
            : value.Trim();
}
