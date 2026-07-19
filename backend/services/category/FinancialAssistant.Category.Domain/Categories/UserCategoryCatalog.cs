namespace FinancialAssistant.Category.Domain.Categories;

public sealed record UserCategoryCatalog
{
    private UserCategoryCatalog(string userId, IReadOnlyList<CategoryDefinition> categories)
    {
        UserId = userId;
        Categories = categories;
    }

    public string UserId { get; }

    public IReadOnlyList<CategoryDefinition> Categories { get; }

    public static UserCategoryCatalog CreateDefault(string userId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        return new UserCategoryCatalog(userId.Trim(), DefaultCategoryTaxonomy.Create(createdAtUtc));
    }

    public CategoryDefinition? Find(string categoryId) =>
        Categories.FirstOrDefault(category =>
            string.Equals(category.Id, categoryId, StringComparison.Ordinal));

    public UserCategoryCatalog Replace(CategoryDefinition category)
    {
        ArgumentNullException.ThrowIfNull(category);

        var categories = Categories
            .Select(existing => existing.Id == category.Id ? category : existing)
            .OrderBy(existing => existing.SortOrder)
            .ThenBy(existing => existing.Id, StringComparer.Ordinal)
            .ToArray();

        return new UserCategoryCatalog(UserId, Array.AsReadOnly(categories));
    }
}
