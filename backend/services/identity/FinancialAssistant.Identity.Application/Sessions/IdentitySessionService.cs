using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Sessions;

public sealed partial class IdentitySessionService : IIdentitySessionService
{
    private readonly IIdentitySessionStore sessionStore;
    private readonly IIdentityAccountStore accountStore;
    private readonly IRefreshTokenService renewalValues;
    private readonly IAccessTokenService accessValues;
    private readonly ISessionLifetimePolicy policy;
    private readonly IIdentityEventPublisher eventPublisher;
    private readonly ISystemClock clock;

    public IdentitySessionService(
        IIdentitySessionStore sessionStore,
        IIdentityAccountStore accountStore,
        IRefreshTokenService renewalValues,
        IAccessTokenService accessValues,
        ISessionLifetimePolicy policy,
        IIdentityEventPublisher eventPublisher,
        ISystemClock clock)
    {
        this.sessionStore = sessionStore;
        this.accountStore = accountStore;
        this.renewalValues = renewalValues;
        this.accessValues = accessValues;
        this.policy = policy;
        this.eventPublisher = eventPublisher;
        this.clock = clock;
    }

    public async Task<AuthSessionResponse> IssueAsync(
        IdentityAccount account,
        IdentityClientContext client,
        string authenticationMethod,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var sessionId = Guid.NewGuid().ToString("N");
        var familyHash = renewalValues.Hash($"family:{Guid.NewGuid():N}");
        var renewalValue = renewalValues.Create(sessionId);
        var session = new IdentitySessionRecord(
            sessionId,
            account.Id,
            familyHash,
            renewalValues.Hash(renewalValue),
            renewalValues.Hash($"client:{client.ClientInstanceId}"),
            IdentitySessionStatus.Active,
            authenticationMethod,
            now,
            now.Add(policy.AccessLifetime),
            now.Add(policy.RenewalLifetime));

        if (!await sessionStore.TryCreateAsync(session, cancellationToken))
        {
            throw new InvalidOperationException("A unique identity session could not be created.");
        }

        return CreateResponse(account, session, renewalValue);
    }

    private AuthSessionResponse CreateResponse(
        IdentityAccount account,
        IdentitySessionRecord session,
        string renewalValue)
    {
        var access = accessValues.Issue(
            account,
            session.Id,
            session.AuthenticationMethod,
            session.IssuedAtUtc,
            session.AccessTokenExpiresAtUtc);
        var user = new CurrentUserContextResponse(
            account.Id,
            session.Id,
            account.Roles,
            session.AuthenticationMethod,
            session.IssuedAtUtc,
            session.RefreshTokenExpiresAtUtc);

        return new AuthSessionResponse(
            "Bearer",
            access.Token,
            access.ExpiresAtUtc,
            renewalValue,
            session.RefreshTokenExpiresAtUtc,
            user);
    }
}
