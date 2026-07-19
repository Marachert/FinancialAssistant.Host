namespace FinancialAssistant.Category.Contracts;

public sealed record CategoryResponse(
    string Id,
    string Key,
    string DisplayName,
    string Kind,
    int SortOrder,
    IReadOnlyList<string> Aliases,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
