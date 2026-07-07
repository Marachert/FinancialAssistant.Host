namespace FinancialAssistant.Identity.Infrastructure.Storage.Documents;

public sealed record ProviderLinkDocument
{
    public const int CurrentSchemaVersion = 1;

    public required string Id { get; init; }

    public required string AccountId { get; init; }

    public required string Provider { get; init; }

    public required string ProviderSubjectHash { get; init; }

    public string? ProviderTenantHash { get; init; }

    public DateTimeOffset LinkedAtUtc { get; init; }

    public DateTimeOffset? LastAuthenticatedAtUtc { get; init; }

    public DateTimeOffset? UnlinkedAtUtc { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
