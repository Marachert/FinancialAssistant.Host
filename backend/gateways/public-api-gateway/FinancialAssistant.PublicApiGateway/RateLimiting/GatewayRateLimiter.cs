using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> limiters =
        new(StringComparer.Ordinal);

    public async ValueTask<GatewayRateLimitLease> AcquireAsync(
        string partitionKey,
        GatewayRateLimitPolicyOptions policy,
        CancellationToken cancellationToken)
    {
        var limiter = limiters.GetOrAdd(
            partitionKey,
            _ => new FixedWindowRateLimiter(
                new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = policy.PermitLimit,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromSeconds(policy.WindowSeconds)
                }));
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        return new GatewayRateLimitLease(
            lease.IsAcquired,
            Math.Max(1, policy.WindowSeconds));
    }

    public void Dispose()
    {
        foreach (var limiter in limiters.Values)
        {
            limiter.Dispose();
        }

        limiters.Clear();
    }
}

public sealed record GatewayRateLimitLease(bool IsAcquired, int RetryAfterSeconds);
