using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Infrastructure.Storage;

internal sealed class TransactionalCategoryService(CategoryService inner, CategoryPersistenceContext context) : ICategoryService
{
    public Task<IReadOnlyList<CategoryResponse>> SeedDefaultsAsync(UserRegisteredCategoryEvent integrationEvent, CancellationToken cancellationToken) =>
        context.RunAsync(integrationEvent.UserId.Trim(), token => inner.SeedDefaultsAsync(integrationEvent, token), cancellationToken);

    public Task<IReadOnlyList<CategoryResponse>?> SearchAsync(string userId, string? query, CancellationToken cancellationToken) =>
        inner.SearchAsync(userId, query, cancellationToken);

    public Task<CategoryResponse?> ReplaceAliasesAsync(string userId, string categoryId, UpdateCategoryAliasesRequest request, CancellationToken cancellationToken) =>
        context.RunAsync(userId.Trim(), token => inner.ReplaceAliasesAsync(userId, categoryId, request, token), cancellationToken);
}
