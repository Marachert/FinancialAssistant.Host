namespace FinancialAssistant.Identity.Infrastructure.Storage.Documents;

public sealed record SessionDocument
{
    public const int CurrentSchemaVersion = 1;

    public required string Id { get; init; }

    public required string AccountId { get; init; }

    public required string TokenFamilyIdHash { get; init; }

    public required string RefreshTokenHash { get; init; }

    public required string Status { get; init; }

    public string? SecurityContextHash { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public DateTimeOffset? RotatedAtUtc { get; init; }

    public DateTimeOffset? RevokedAtUtc { get; init; }

    public string? ReplacedBySessionId { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
