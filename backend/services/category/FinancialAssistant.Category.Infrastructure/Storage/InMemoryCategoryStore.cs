using System.Collections.Concurrent;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Domain.Categories;

namespace FinancialAssistant.Category.Infrastructure.Storage;

public sealed class InMemoryCategoryStore : ICategoryStore
{
    private readonly ConcurrentDictionary<string, UserCategoryCatalog> catalogs = new(StringComparer.Ordinal);

    public Task<UserCategoryCatalog> CreateDefaultIfMissingAsync(
        UserCategoryCatalog catalog,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(catalogs.GetOrAdd(catalog.UserId, catalog));
    }

    public Task<UserCategoryCatalog?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        catalogs.TryGetValue(userId, out var catalog);
        return Task.FromResult(catalog);
    }

    public Task<CategoryStoreUpdateResult?> ReplaceAliasesAsync(
        string userId,
        string categoryId,
        IReadOnlyCollection<string> aliases,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (!catalogs.TryGetValue(userId, out var existingCatalog))
            {
                return Task.FromResult<CategoryStoreUpdateResult?>(null);
            }

            var existingCategory = existingCatalog.Find(categoryId);
            if (existingCategory is null)
            {
                return Task.FromResult<CategoryStoreUpdateResult?>(null);
            }

            var updatedCategory = existingCategory.ReplaceAliases(aliases, updatedAtUtc);
            if (ReferenceEquals(updatedCategory, existingCategory))
            {
                return Task.FromResult<CategoryStoreUpdateResult?>(
                    new CategoryStoreUpdateResult(existingCategory, Changed: false));
            }

            var updatedCatalog = existingCatalog.Replace(updatedCategory);
            if (catalogs.TryUpdate(userId, updatedCatalog, existingCatalog))
            {
                return Task.FromResult<CategoryStoreUpdateResult?>(
                    new CategoryStoreUpdateResult(updatedCategory, Changed: true));
            }
        }
    }
}
