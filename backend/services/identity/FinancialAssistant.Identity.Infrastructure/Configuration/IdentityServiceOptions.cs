namespace FinancialAssistant.Identity.Infrastructure.Configuration;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "Identity";

    public string ServiceName { get; set; } = "financial-assistant-identity-service";
    public IdentityStorageOptions Storage { get; set; } = new();
    public IdentityAuthenticationOptions Authentication { get; set; } = new();
    public IdentityProviderOptions Providers { get; set; } = new();
    public IdentityRateLimitingOptions RateLimiting { get; set; } = new();
    public IdentityEventPublishingOptions Events { get; set; } = new();
}

public sealed class IdentityStorageOptions
{
    public string Provider { get; set; } = "Elasticsearch";
    public string Environment { get; set; } = "dev";
    public int SchemaVersion { get; set; } = 1;
    public int InitialGeneration { get; set; } = 1;
    public IdentityCleanupOptions Cleanup { get; set; } = new();
}

public sealed class IdentityCleanupOptions
{
    public int DeletedAccountRetentionDays { get; set; } = 30;
    public int RemovedCredentialRetentionDays { get; set; } = 30;
    public int TerminalSessionRetentionDays { get; set; } = 30;
    public int HardMaximumSessionDocumentDays { get; set; } = 90;
    public int RemovedProviderLinkRetentionDays { get; set; } = 30;
}

public sealed class IdentityAuthenticationOptions
{
    public string RuntimeAdapter { get; set; } = "InMemoryDevelopment";
    public string LookupHmacKey { get; set; } = string.Empty;
    public string RefreshTokenHashKey { get; set; } = string.Empty;
    public string AccessTokenSigningKey { get; set; } = string.Empty;
    public string AccessTokenIssuer { get; set; } = "financial-assistant-identity";
    public string AccessTokenAudience { get; set; } = "financial-assistant-clients";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
}

public sealed class IdentityProviderOptions
{
    public string IdentifierHmacKey { get; set; } = string.Empty;
    public GoogleIdentityProviderOptions Google { get; set; } = new();
    public AppleIdentityProviderOptions Apple { get; set; } = new();
    public PhoneVerificationOptions Phone { get; set; } = new();
}

public sealed class GoogleIdentityProviderOptions
{
    public bool Enabled { get; set; }
    public List<string> ClientIds { get; set; } = new();
    public string? HostedDomain { get; set; }
    public int IssuedAtClockToleranceSeconds { get; set; } = 30;
    public int ExpirationClockToleranceSeconds { get; set; } = 30;
}

public sealed class AppleIdentityProviderOptions
{
    public bool Enabled { get; set; }
    public List<string> ClientIds { get; set; } = new();
    public string Issuer { get; set; } = "https://appleid.apple.com";
    public string DiscoveryEndpoint { get; set; } = "https://appleid.apple.com/.well-known/openid-configuration";
    public int ClockSkewSeconds { get; set; } = 60;
    public bool RequireNonce { get; set; } = true;
}

public sealed class PhoneVerificationOptions
{
    public bool Enabled { get; set; }
    public string Adapter { get; set; } = "Disabled";
    public int CodeLength { get; set; } = 6;
    public int ChallengeLifetimeMinutes { get; set; } = 10;
    public int ResendCooldownSeconds { get; set; } = 30;
    public int MaximumAttempts { get; set; } = 5;
    public int StartWindowMinutes { get; set; } = 60;
    public int MaximumStartsPerPhone { get; set; } = 5;
    public int MaximumStartsPerClient { get; set; } = 10;
}

public sealed class IdentityRateLimitingOptions
{
    public bool Enabled { get; set; } = true;
    public IdentityFixedWindowPolicyOptions Registration { get; set; } = new(5, 600);
    public IdentityFixedWindowPolicyOptions SignIn { get; set; } = new(10, 300);
    public IdentityFixedWindowPolicyOptions ProviderSignIn { get; set; } = new(10, 300);
    public IdentityFixedWindowPolicyOptions PhoneStart { get; set; } = new(5, 60);
    public IdentityFixedWindowPolicyOptions PhoneConfirm { get; set; } = new(20, 300);
    public IdentityFixedWindowPolicyOptions Session { get; set; } = new(60, 60);
}

public sealed class IdentityFixedWindowPolicyOptions
{
    public IdentityFixedWindowPolicyOptions()
    {
    }

    public IdentityFixedWindowPolicyOptions(int permitLimit, int windowSeconds)
    {
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }

    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class IdentityEventPublishingOptions
{
    public string Mode { get; set; } = "InMemoryDevelopment";
    public string Exchange { get; set; } = "fa.events";
    public string ConnectionString { get; set; } = string.Empty;
    public string UserIdHmacKey { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 25;
    public int DispatchIntervalMilliseconds { get; set; } = 1000;
    public int MaximumRetryDelaySeconds { get; set; } = 60;
}
