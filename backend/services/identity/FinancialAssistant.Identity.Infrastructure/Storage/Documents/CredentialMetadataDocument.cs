namespace FinancialAssistant.Identity.Infrastructure.Storage.Documents;

public sealed record CredentialMetadataDocument
{
    public const int CurrentSchemaVersion = 1;

    public required string Id { get; init; }

    public required string AccountId { get; init; }

    public required string CredentialKind { get; init; }

    public required string LookupKeyHash { get; init; }

    public required string SecretHash { get; init; }

    public required string SecretHashAlgorithm { get; init; }

    public required string SecretHashParameters { get; init; }

    public bool IsVerified { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? VerifiedAtUtc { get; init; }

    public DateTimeOffset? LastRotatedAtUtc { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
