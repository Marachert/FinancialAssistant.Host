using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public interface INotificationPreferenceProvider
{
    Task<NotificationPreferences> GetAsync(
        string userIdHash,
        CancellationToken cancellationToken);

    Task<NotificationPreferences> UpdateAsync(
        string userIdHash,
        NotificationPreferences preferences,
        CancellationToken cancellationToken);
}
