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

    public string AccountsAlias { get; set; } = string.Empty;

    public string SessionsAlias { get; set; } = string.Empty;
}

public sealed class IdentityEventPublishingOptions
{
    public string Mode { get; set; } = "placeholder";

    public string Exchange { get; set; } = string.Empty;
}
