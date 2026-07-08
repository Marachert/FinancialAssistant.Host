using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimiter : IDisposable
{
    private readonly object gate = new();
    private readonly MemoryCache cache;
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> overflowLimiters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan idleExpiration;

    public GatewayRateLimiter(IOptions<GatewayRateLimitOptions> options)
    {
        var configuration = options.Value;
        if (configuration.MaximumPartitionCount < 1
            || configuration.PartitionIdleExpirationSeconds < 1)
        {
            throw new InvalidOperationException("Gateway rate limit partition cache configuration is invalid.");
        }

        cache = new MemoryCache(
            new MemoryCacheOptions
            {
                SizeLimit = configuration.MaximumPartitionCount
            });
        idleExpiration = TimeSpan.FromSeconds(configuration.PartitionIdleExpirationSeconds);
    }

    public int CachedPartitionCount => cache.Count;

    public async ValueTask<GatewayRateLimitLease> AcquireAsync(
        string policyName,
        string partitionKey,
        GatewayRateLimitPolicyOptions policy,
        CancellationToken cancellationToken)
    {
        var limiter = GetOrCreateLimiter(policyName, partitionKey, policy);
        limiter.TryReplenish();
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        return new GatewayRateLimitLease(
            lease.IsAcquired,
            Math.Max(1, policy.WindowSeconds));
    }

    public void Dispose()
    {
        cache.Dispose();
        foreach (var limiter in overflowLimiters.Values)
        {
            limiter.Dispose();
        }

        overflowLimiters.Clear();
    }

    private FixedWindowRateLimiter GetOrCreateLimiter(
        string policyName,
        string partitionKey,
        GatewayRateLimitPolicyOptions policy)
    {
        lock (gate)
        {
            if (cache.TryGetValue<FixedWindowRateLimiter>(partitionKey, out var existing)
                && existing is not null)
            {
                return existing;
            }

            var created = CreateLimiter(policy);
            cache.Set(
                partitionKey,
                created,
                new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetSlidingExpiration(idleExpiration));
            if (cache.TryGetValue<FixedWindowRateLimiter>(partitionKey, out var stored)
                && stored is not null)
            {
                return stored;
            }

            created.Dispose();
            return overflowLimiters.GetOrAdd(policyName, _ => CreateLimiter(policy));
        }
    }

    private static FixedWindowRateLimiter CreateLimiter(GatewayRateLimitPolicyOptions policy) =>
        new(
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = false,
                PermitLimit = policy.PermitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds)
            });
}

public sealed record GatewayRateLimitLease(bool IsAcquired, int RetryAfterSeconds);
