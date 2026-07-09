using FinancialAssistant.Profile.Domain.Profiles;

namespace FinancialAssistant.Profile.Application.Abstractions;

public interface IProfileStore
{
    Task<UserProfile> CreateDefaultIfMissingAsync(UserProfile profile, CancellationToken cancellationToken);

    Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken);

    Task<UserProfile?> UpdateAsync(
        string userId,
        Func<UserProfile, UserProfile> update,
        CancellationToken cancellationToken);
}
