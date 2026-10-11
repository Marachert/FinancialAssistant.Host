using FinancialAssistant.Identity.Application;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers.Apple;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Identity.Infrastructure;
using FinancialAssistant.Identity.Infrastructure.Authentication;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Messaging;
using FinancialAssistant.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.Identity.Tests;

public sealed class NativeIdentityConfigurationTests
{
    [Fact]
    public async Task InitialSessionIssuerDoesNotBlockAndPropagatesCancellation()
    {
        var sessions = new WaitingSessionService();
        var issuer = new OpaqueInitialSessionIssuer(sessions);
        using var cancellation = new CancellationTokenSource();
        var result = issuer.IssueAsync(IdentityAccount.Create(DateTimeOffset.UtcNow),
            new IdentityClientContext("synthetic", "web", "0.0-test"), "email_password", DateTimeOffset.UtcNow, cancellation.Token);
        Assert.False(result.IsCompleted);
        Assert.Equal(cancellation.Token, sessions.Cancellation);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await result);
    }
    [Fact]
    public void NativeConfigurationRejectsDefaultsAndMalformedOrReusedKeys()
    {
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(new IdentityServiceOptions()));
        var options = Options();
        NativeIdentityPersistence.Validate(options);
        options.Storage.Postgres.IndexKey = "not base64";
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(options));
        options = Options();
        options.Storage.Postgres.EncryptionKeys["v1"] = options.Storage.Postgres.IndexKey;
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(options));
        options = Options();
        options.Providers.Phone.Enabled = true;
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(options));
        options = Options();
        options.Events.Mode = "InMemoryDevelopment";
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(options));
        options = Options();
        options.Authentication.RefreshTokenHashKey = "";
        Assert.Throws<InvalidOperationException>(() => NativeIdentityPersistence.Validate(options));
    }

    [Fact]
    public async Task NativeCompositionHasNoInMemoryFallbackOrFalsePublishingDispatcher()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { Identity = Options() });
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration, windowsNative: true);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.IsType<PostgresIdentityAccountStore>(provider.GetRequiredService<IIdentityAccountStore>());
        Assert.IsType<PostgresIdentitySessionStore>(provider.GetRequiredService<IIdentitySessionStore>());
        Assert.IsType<PostgresPhoneVerificationChallengeStore>(provider.GetRequiredService<IPhoneVerificationChallengeStore>());
        Assert.IsType<PostgresIdentityEventOutbox>(provider.GetRequiredService<IIdentityEventOutbox>());
        Assert.Empty(provider.GetServices<IIdentityEventTransport>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service => service is IdentityOutboxDispatcher);
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdentityAuthenticationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdentitySessionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGoogleProviderAuthenticationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAppleProviderAuthenticationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPhoneVerificationAuthenticationService>());
    }

    [Fact]
    public void RehydrationPreservesIdentityStatusRolesAndTimestampWithoutSharingMutableRoles()
    {
        var id = Guid.NewGuid().ToString("N");
        var roles = new[] { "user", "auditor" };
        var created = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var account = IdentityAccount.Rehydrate(id, IdentityAccountStatus.Disabled, roles, created);
        roles[0] = "admin";
        Assert.Equal(id, account.Id);
        Assert.Equal(created, account.CreatedAtUtc);
        Assert.Equal(IdentityAccountStatus.Disabled, account.Status);
        Assert.False(account.CanAuthenticate);
        Assert.Equal(new[] { "user", "auditor" }, account.Roles);
        Assert.Throws<ArgumentException>(() => IdentityAccount.Rehydrate(id, (IdentityAccountStatus)99, ["user"], created));
        Assert.Throws<ArgumentException>(() => IdentityAccount.Rehydrate("bad", IdentityAccountStatus.Active, ["user"], created));
        Assert.Throws<ArgumentException>(() => IdentityAccount.Rehydrate(id, IdentityAccountStatus.Active, ["user", "user"], created));
    }

    private static IdentityServiceOptions Options() => new()
    {
        Storage = new()
        {
            Provider = "PostgreSQL",
            Postgres = new()
            {
                Database = "fa_synthetic",
                Port = 5432,
                Password = new string('p', 64),
                IndexKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
                ActiveEncryptionKeyId = "v1",
                EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
            }
        },
        Authentication = new()
        {
            RuntimeAdapter = "NativePostgres",
            LookupHmacKey = new string('l', 64),
            RefreshTokenHashKey = new string('r', 64),
            AccessTokenSigningKey = new string('a', 64)
        },
        Providers = new() { IdentifierHmacKey = new string('i', 64) },
        Events = new() { Mode = "DurableOutboxPendingTransport", UserIdHmacKey = new string('e', 64) }
    };

    private sealed class WaitingSessionService : IIdentitySessionService
    {
        private readonly TaskCompletionSource<AuthSessionResponse> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Cancellation { get; private set; }
        public Task<AuthSessionResponse> IssueAsync(IdentityAccount account, IdentityClientContext client, string method, CancellationToken token = default)
        {
            Cancellation = token;
            token.Register(() => completion.TrySetCanceled(token));
            return completion.Task;
        }
        public Task<IdentityOperationResult<AuthSessionResponse>> RefreshAsync(RefreshSessionRequest request, string correlationId, CancellationToken token = default) => throw new NotSupportedException();
        public Task<IdentityOperationResult<bool>> LogoutAsync(LogoutRequest request, AccessSessionContext access, string correlationId, CancellationToken token = default) => throw new NotSupportedException();
        public Task<IdentityOperationResult<CurrentUserContextResponse>> GetCurrentAsync(AccessSessionContext access, CancellationToken token = default) => throw new NotSupportedException();
    }
}
