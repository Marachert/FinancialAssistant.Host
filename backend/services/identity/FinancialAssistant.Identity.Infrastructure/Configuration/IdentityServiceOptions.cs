namespace FinancialAssistant.Identity.Infrastructure.Configuration;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "Identity";

    public string ServiceName { get; set; } = "financial-assistant-identity-service";

    public IdentityStorageOptions Storage { get; set; } = new();

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

public sealed class IdentityEventPublishingOptions
{
    public string Mode { get; set; } = "placeholder";

    public string Exchange { get; set; } = string.Empty;
}
