using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> SeedDefaultsAsync(
        UserRegisteredCategoryEvent integrationEvent,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryResponse>?> SearchAsync(
        string userId,
        string? query,
        CancellationToken cancellationToken);

    Task<CategoryResponse?> ReplaceAliasesAsync(
        string userId,
        string categoryId,
        UpdateCategoryAliasesRequest request,
        CancellationToken cancellationToken);
}
