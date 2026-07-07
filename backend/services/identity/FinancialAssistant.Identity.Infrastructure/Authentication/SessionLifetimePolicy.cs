using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class SessionLifetimePolicy : ISessionLifetimePolicy
{
    public SessionLifetimePolicy(IOptions<IdentityServiceOptions> options)
    {
        AccessLifetime = TimeSpan.FromMinutes(options.Value.Authentication.AccessTokenLifetimeMinutes);
        RenewalLifetime = TimeSpan.FromDays(options.Value.Authentication.RefreshTokenLifetimeDays);
    }

    public TimeSpan AccessLifetime { get; }

    public TimeSpan RenewalLifetime { get; }
}
