using FinancialAssistant.Category.Domain.Categories;

namespace FinancialAssistant.Category.Application.Abstractions;

public interface ICategoryStore
{
    Task<UserCategoryCatalog> CreateDefaultIfMissingAsync(
        UserCategoryCatalog catalog,
        CancellationToken cancellationToken);

    Task<UserCategoryCatalog?> GetAsync(string userId, CancellationToken cancellationToken);

    Task<CategoryStoreUpdateResult?> ReplaceAliasesAsync(
        string userId,
        string categoryId,
        IReadOnlyCollection<string> aliases,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}

public sealed record CategoryStoreUpdateResult(CategoryDefinition Category, bool Changed);
