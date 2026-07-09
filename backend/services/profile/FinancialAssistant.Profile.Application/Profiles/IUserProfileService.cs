using FinancialAssistant.Profile.Contracts;

namespace FinancialAssistant.Profile.Application.Profiles;

public interface IUserProfileService
{
    Task<UserProfileResponse> CreateFromRegisteredUserAsync(
        UserRegisteredProfileEvent integrationEvent,
        CancellationToken cancellationToken);

    Task<UserProfileResponse?> GetCurrentAsync(string userId, CancellationToken cancellationToken);

    Task<UserProfileResponse?> UpdatePreferencesAsync(
        string userId,
        UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken);
}
