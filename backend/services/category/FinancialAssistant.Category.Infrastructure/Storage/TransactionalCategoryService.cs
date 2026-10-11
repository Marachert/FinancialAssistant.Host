using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Infrastructure.Storage;

internal sealed class TransactionalCategoryService(CategoryService inner, CategoryPersistenceContext context) : ICategoryService
{
    public Task<IReadOnlyList<CategoryResponse>> SeedDefaultsAsync(UserRegisteredCategoryEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (string.IsNullOrWhiteSpace(integrationEvent.UserId)) { throw new ArgumentException("User ID is required.", nameof(integrationEvent)); }
        return context.RunAsync(integrationEvent.UserId.Trim(), token => inner.SeedDefaultsAsync(integrationEvent, token), cancellationToken);
    }

    public Task<IReadOnlyList<CategoryResponse>?> SearchAsync(string userId, string? query, CancellationToken cancellationToken) =>
        inner.SearchAsync(userId, query, cancellationToken);

    public Task<CategoryResponse?> ReplaceAliasesAsync(string userId, string categoryId, UpdateCategoryAliasesRequest request, CancellationToken cancellationToken)
    {
        var owner = CategoryService.ValidateAliasRequest(userId, categoryId, request);
        return context.RunAsync(owner, token => inner.ReplaceAliasesAsync(userId, categoryId, request, token), cancellationToken);
    }
}
