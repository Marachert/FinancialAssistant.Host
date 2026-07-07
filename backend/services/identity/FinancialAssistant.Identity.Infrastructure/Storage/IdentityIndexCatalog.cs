using System.Text.RegularExpressions;

namespace FinancialAssistant.Identity.Infrastructure.Storage;

public static partial class IdentityIndexCatalog
{
    public const string ServiceNamespace = "identity";

    public const string AccountsEntity = "accounts";
    public const string CredentialsEntity = "credentials";
    public const string SessionsEntity = "sessions";
    public const string ExternalIdentitiesEntity = "external-identities";

    private static readonly string[] OwnedEntities =
    [
        AccountsEntity,
        CredentialsEntity,
        SessionsEntity,
        ExternalIdentitiesEntity
    ];

    public static IReadOnlyList<IdentityOwnedIndexDefinition> Create(
        string environment,
        int schemaVersion = 1,
        int generation = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);

        var normalizedEnvironment = environment.Trim().ToLowerInvariant();
        if (!SegmentPattern().IsMatch(normalizedEnvironment))
        {
            throw new ArgumentException(
                "Environment must contain lowercase letters, numbers, or single hyphen separators.",
                nameof(environment));
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be positive.");
        }

        if (generation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation must be positive.");
        }

        return Array.AsReadOnly(
            OwnedEntities
                .Select(entity => CreateDefinition(normalizedEnvironment, entity, schemaVersion, generation))
                .ToArray());
    }

    private static IdentityOwnedIndexDefinition CreateDefinition(
        string environment,
        string entity,
        int schemaVersion,
        int generation)
    {
        var prefix = $"fa-{environment}-{ServiceNamespace}-{entity}";

        return new IdentityOwnedIndexDefinition(
            entity,
            schemaVersion,
            $"{prefix}-v{schemaVersion}-{generation:D6}",
            $"{prefix}-read",
            $"{prefix}-write");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
