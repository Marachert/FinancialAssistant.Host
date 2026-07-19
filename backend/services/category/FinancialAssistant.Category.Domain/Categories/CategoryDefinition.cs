namespace FinancialAssistant.Category.Domain.Categories;

public sealed record CategoryDefinition
{
    public const int MaximumAliasCount = 20;
    public const int MaximumAliasLength = 80;
    public const int MaximumSearchLength = 100;

    private CategoryDefinition(
        string id,
        string key,
        string displayName,
        string kind,
        int sortOrder,
        IReadOnlyList<string> aliases,
        int version,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Key = key;
        DisplayName = displayName;
        Kind = kind;
        SortOrder = sortOrder;
        Aliases = aliases;
        Version = version;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Id { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public string Kind { get; }

    public int SortOrder { get; }

    public IReadOnlyList<string> Aliases { get; }

    public int Version { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public static CategoryDefinition CreateDefault(
        string key,
        string displayName,
        string kind,
        int sortOrder,
        IEnumerable<string> aliases,
        DateTimeOffset createdAtUtc)
    {
        var normalizedKey = NormalizeRequired(key, nameof(key), MaximumAliasLength).ToLowerInvariant();
        var normalizedName = NormalizeRequired(displayName, nameof(displayName), MaximumAliasLength);
        var normalizedKind = NormalizeRequired(kind, nameof(kind), MaximumAliasLength).ToLowerInvariant();
        if (normalizedKind is not CategoryKinds.Expense and not CategoryKinds.Income)
        {
            throw new ArgumentException("Category kind must be expense or income.", nameof(kind));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        return new CategoryDefinition(
            normalizedKey,
            normalizedKey,
            normalizedName,
            normalizedKind,
            sortOrder,
            NormalizeAliases(aliases),
            version: 1,
            createdAtUtc,
            createdAtUtc);
    }

    public CategoryDefinition ReplaceAliases(IEnumerable<string> aliases, DateTimeOffset updatedAtUtc)
    {
        var normalizedAliases = NormalizeAliases(aliases);
        if (Aliases.SequenceEqual(normalizedAliases, StringComparer.Ordinal))
        {
            return this;
        }

        return new CategoryDefinition(
            Id,
            Key,
            DisplayName,
            Kind,
            SortOrder,
            normalizedAliases,
            Version + 1,
            CreatedAtUtc,
            updatedAtUtc);
    }

    public int GetMatchScore(string normalizedQuery)
    {
        if (normalizedQuery.Length == 0)
        {
            return 0;
        }

        var candidates = Aliases
            .Append(Key)
            .Append(DisplayName)
            .Select(NormalizeForComparison)
            .ToArray();

        if (candidates.Any(value => string.Equals(value, normalizedQuery, StringComparison.Ordinal)))
        {
            return 0;
        }

        if (candidates.Any(value => value.StartsWith(normalizedQuery, StringComparison.Ordinal)))
        {
            return 1;
        }

        return candidates.Any(value => value.Contains(normalizedQuery, StringComparison.Ordinal))
            ? 2
            : int.MaxValue;
    }

    public static string NormalizeSearchTerm(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        return NormalizeRequired(query, nameof(query), MaximumSearchLength).ToLowerInvariant();
    }

    private static IReadOnlyList<string> NormalizeAliases(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var normalized = aliases
            .Select(alias => NormalizeRequired(alias, nameof(aliases), MaximumAliasLength).ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length > MaximumAliasCount)
        {
            throw new ArgumentException(
                $"A category can have at most {MaximumAliasCount} aliases.",
                nameof(aliases));
        }

        return Array.AsReadOnly(normalized);
    }

    private static string NormalizeForComparison(string value) =>
        NormalizeRequired(value, nameof(value), MaximumSearchLength).ToLowerInvariant();

    private static string NormalizeRequired(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
