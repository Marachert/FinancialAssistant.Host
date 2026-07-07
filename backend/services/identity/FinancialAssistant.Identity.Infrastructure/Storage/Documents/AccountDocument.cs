namespace FinancialAssistant.Identity.Infrastructure.Storage.Documents;

public sealed record AccountDocument
{
    public const int CurrentSchemaVersion = 1;

    public required string Id { get; init; }

    public required string Status { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? DeletedAtUtc { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
