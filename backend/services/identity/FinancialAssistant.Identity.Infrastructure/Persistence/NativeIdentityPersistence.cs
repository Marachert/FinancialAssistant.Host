using System.Security.Cryptography;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers.Apple;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public static class NativeIdentityPersistence
{
    public static void Validate(IdentityServiceOptions options)
    {
        if (!string.Equals(options.Storage.Provider, "PostgreSQL", StringComparison.Ordinal)
            || !string.Equals(options.Authentication.RuntimeAdapter, "NativePostgres", StringComparison.Ordinal)
            || !string.Equals(options.Events.Mode, "DurableOutboxPendingTransport", StringComparison.Ordinal)
            || options.Providers.Phone.Enabled || !string.Equals(options.Providers.Phone.Adapter, "Disabled", StringComparison.Ordinal)
            || options.Authentication.RefreshTokenLifetimeDays is < 1 or > 90
            || options.Authentication.AccessTokenLifetimeMinutes is < 1 or > 60
            || options.Storage.Cleanup.TerminalSessionRetentionDays < 1 || options.Storage.Cleanup.DeletedAccountRetentionDays < 1
            || options.Storage.Cleanup.RemovedCredentialRetentionDays < 1 || options.Storage.Cleanup.RemovedProviderLinkRetentionDays < 1
            || options.Storage.Cleanup.HardMaximumSessionDocumentDays < options.Authentication.RefreshTokenLifetimeDays
            || options.Storage.Cleanup.HardMaximumSessionDocumentDays < options.Storage.Cleanup.TerminalSessionRetentionDays
            || options.Storage.Cleanup.HardMaximumSessionDocumentDays < (long)options.Authentication.RefreshTokenLifetimeDays + options.Storage.Cleanup.TerminalSessionRetentionDays
            || new[] { options.Storage.Cleanup.TerminalSessionRetentionDays, options.Storage.Cleanup.DeletedAccountRetentionDays,
                options.Storage.Cleanup.RemovedCredentialRetentionDays, options.Storage.Cleanup.RemovedProviderLinkRetentionDays,
                options.Storage.Cleanup.HardMaximumSessionDocumentDays }.Any(days => days > 3650)
            || options.Providers.Phone.MaximumStartsPerPhone is < 1 or > 100
            || options.Providers.Phone.MaximumStartsPerClient is < 1 or > 500
            || options.Providers.Phone.StartWindowMinutes is < 1 or > 1440
            || options.Providers.Phone.MaximumAttempts is < 1 or > 10
            || options.Providers.Phone.ChallengeLifetimeMinutes is < 1 or > 30
            || options.Providers.Phone.ResendCooldownSeconds is < 10 or > 600
            || new[] { options.Authentication.LookupHmacKey, options.Authentication.RefreshTokenHashKey,
                options.Authentication.AccessTokenSigningKey, options.Providers.IdentifierHmacKey, options.Events.UserIdHmacKey }
                .Any(key => string.IsNullOrWhiteSpace(key) || key.Length < 32))
        { throw new InvalidOperationException("Native identity requires durable adapters, stable keys and disabled paid providers."); }
        _ = CreateOptions(options.Storage.Postgres);
        using var protection = CreateProtection(options.Storage.Postgres);
    }

    public static OwnedPostgresOptions CreateOptions(IdentityPostgresOptions options) =>
        new(new StorageIdentity("identity"), options.Database, options.Port, options.Password);

    public static RecordProtection CreateProtection(IdentityPostgresOptions options)
    {
        var material = new List<byte[]>();
        try
        {
            if (options.EncryptionKeys is null) { throw new ArgumentException(); }
            var index = Convert.FromBase64String(options.IndexKey);
            material.Add(index);
            var keys = options.EncryptionKeys.ToDictionary(pair => pair.Key, pair =>
            {
                var value = Convert.FromBase64String(pair.Value);
                material.Add(value);
                return value;
            }, StringComparer.Ordinal);
            return new RecordProtection(index, options.ActiveEncryptionKeyId, keys);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { throw new InvalidOperationException("Native identity persistence key configuration is invalid."); }
        finally { foreach (var bytes in material) { CryptographicOperations.ZeroMemory(bytes); } }
    }

    internal static void Register(IServiceCollection services, IdentityServiceOptions options)
    {
        Validate(options);
        services.AddSingleton(_ => CreateProtection(options.Storage.Postgres));
        services.AddSingleton(provider => new OwnedPostgresStore(CreateOptions(options.Storage.Postgres), provider.GetRequiredService<RecordProtection>()));
        services.AddSingleton<IdentityPersistenceContext>();
        services.AddSingleton<IIdentityMutationScope, NativeIdentityMutationScope>();
        services.AddSingleton<IdentityPersistenceMaintenance>();
        services.AddHostedService<IdentityPersistenceMaintenanceWorker>();
        services.AddSingleton<PostgresIdentityAccountStore>();
        services.AddSingleton<IIdentityAccountStore>(provider => provider.GetRequiredService<PostgresIdentityAccountStore>());
        services.AddSingleton<IIdentityFederatedAccountStore>(provider => provider.GetRequiredService<PostgresIdentityAccountStore>());
        services.AddSingleton<IIdentitySessionStore, PostgresIdentitySessionStore>();
        services.AddSingleton<IPhoneVerificationChallengeStore, PostgresPhoneVerificationChallengeStore>();
        services.AddSingleton<IIdentityEventOutbox, PostgresIdentityEventOutbox>();

        services.RemoveAll<IIdentityAuthenticationService>();
        services.RemoveAll<IIdentitySessionService>();
        services.RemoveAll<IGoogleProviderAuthenticationService>();
        services.RemoveAll<IAppleProviderAuthenticationService>();
        services.RemoveAll<IPhoneVerificationAuthenticationService>();
        services.AddScoped<IdentityAuthenticationService>();
        services.AddScoped<IdentitySessionService>();
        services.AddScoped<GoogleProviderAuthenticationService>();
        services.AddScoped<AppleProviderAuthenticationService>();
        services.AddScoped<PhoneVerificationAuthenticationService>();
        services.AddScoped<IIdentityAuthenticationService, TransactionalIdentityAuthenticationService>();
        services.AddScoped<IIdentitySessionService, TransactionalIdentitySessionService>();
        services.AddScoped<IGoogleProviderAuthenticationService, TransactionalGoogleAuthenticationService>();
        services.AddScoped<IAppleProviderAuthenticationService, TransactionalAppleAuthenticationService>();
        services.AddScoped<IPhoneVerificationAuthenticationService, TransactionalPhoneAuthenticationService>();
    }
}
