using System.Collections.Concurrent;
using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Domain.Profiles;

namespace FinancialAssistant.Profile.Infrastructure.Storage;

public sealed class InMemoryProfileStore : IProfileStore
{
    private readonly ConcurrentDictionary<string, UserProfile> profiles = new(StringComparer.Ordinal);

    public Task<UserProfile> CreateDefaultIfMissingAsync(
        UserProfile profile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = profiles.GetOrAdd(profile.UserId, profile);
        return Task.FromResult(stored);
    }

    public Task<UserProfile?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        profiles.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<UserProfile?> UpdateAsync(
        string userId,
        Func<UserProfile, UserProfile> update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!profiles.TryGetValue(userId, out var existing))
            {
                return Task.FromResult<UserProfile?>(null);
            }

            var updated = update(existing);
            if (profiles.TryUpdate(userId, updated, existing))
            {
                return Task.FromResult<UserProfile?>(updated);
            }
        }
    }
}
