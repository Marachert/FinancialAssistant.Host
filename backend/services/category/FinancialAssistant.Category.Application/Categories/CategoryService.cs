using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Contracts;
using FinancialAssistant.Category.Domain.Categories;

namespace FinancialAssistant.Category.Application.Categories;

public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryStore store;
    private readonly ICategoryEventPublisher eventPublisher;
    private readonly ICategoryClock clock;

    public CategoryService(
        ICategoryStore store,
        ICategoryEventPublisher eventPublisher,
        ICategoryClock clock)
    {
        this.store = store;
        this.eventPublisher = eventPublisher;
        this.clock = clock;
    }

    public async Task<IReadOnlyList<CategoryResponse>> SeedDefaultsAsync(
        UserRegisteredCategoryEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var createdAtUtc = integrationEvent.OccurredAtUtc == default
            ? clock.UtcNow
            : integrationEvent.OccurredAtUtc.ToUniversalTime();
        var catalog = UserCategoryCatalog.CreateDefault(integrationEvent.UserId, createdAtUtc);
        var stored = await store.CreateDefaultIfMissingAsync(catalog, cancellationToken);

        return ToResponses(stored.Categories);
    }

    public async Task<IReadOnlyList<CategoryResponse>?> SearchAsync(
        string userId,
        string? query,
        CancellationToken cancellationToken)
    {
        var catalog = await store.GetAsync(NormalizeUserId(userId), cancellationToken);
        if (catalog is null)
        {
            return null;
        }

        var normalizedQuery = CategoryDefinition.NormalizeSearchTerm(query);
        return catalog.Categories
            .Select(category => new
            {
                Category = category,
                Score = category.GetMatchScore(normalizedQuery)
            })
            .Where(match => match.Score != int.MaxValue)
            .OrderBy(match => match.Score)
            .ThenBy(match => match.Category.SortOrder)
            .ThenBy(match => match.Category.Id, StringComparer.Ordinal)
            .Select(match => ToResponse(match.Category))
            .ToArray();
    }

    public async Task<CategoryResponse?> ReplaceAliasesAsync(
        string userId,
        string categoryId,
        UpdateCategoryAliasesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Aliases is null)
        {
            throw new ArgumentException("Aliases are required.", nameof(request));
        }

        var normalizedUserId = NormalizeUserId(userId);
        var normalizedCategoryId = NormalizeCategoryId(categoryId);
        var updatedAtUtc = clock.UtcNow;
        var result = await store.ReplaceAliasesAsync(
            normalizedUserId,
            normalizedCategoryId,
            request.Aliases,
            updatedAtUtc,
            cancellationToken);

        if (result is null)
        {
            return null;
        }

        if (result.Changed)
        {
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? $"category-{result.Category.Id}-{result.Category.Version}"
                : request.CorrelationId.Trim();
            await eventPublisher.PublishAsync(
                new CategoryUpdatedIntegrationEvent(
                    normalizedUserId,
                    result.Category.Id,
                    result.Category.Version,
                    "aliases_replaced",
                    result.Category.UpdatedAtUtc,
                    correlationId),
                cancellationToken);
        }

        return ToResponse(result.Category);
    }

    private static string NormalizeUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        return userId.Trim();
    }

    private static string NormalizeCategoryId(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw new ArgumentException("Category ID is required.", nameof(categoryId));
        }

        return categoryId.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<CategoryResponse> ToResponses(
        IEnumerable<CategoryDefinition> categories) =>
        categories
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id, StringComparer.Ordinal)
            .Select(ToResponse)
            .ToArray();

    private static CategoryResponse ToResponse(CategoryDefinition category) =>
        new(
            category.Id,
            category.Key,
            category.DisplayName,
            category.Kind,
            category.SortOrder,
            category.Aliases,
            category.Version,
            category.CreatedAtUtc,
            category.UpdatedAtUtc);
}
