namespace FinancialAssistant.Category.Contracts;

public sealed record UpdateCategoryAliasesRequest(
    IReadOnlyCollection<string>? Aliases,
    string? CorrelationId);
