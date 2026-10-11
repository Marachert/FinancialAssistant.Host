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

    public static UserCategoryCatalog Rehydrate(string userId, IReadOnlyList<CategoryDefinition> categories)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > 128 || userId != userId.Trim() || userId.Any(char.IsControl)
            || categories is null || categories.Count is < 1 or > 100 || categories.Any(category => category is null)
            || categories.Select(category => category.Id).Distinct(StringComparer.Ordinal).Count() != categories.Count)
        { throw new ArgumentException("Stored category catalog is invalid."); }
        return new UserCategoryCatalog(userId, Array.AsReadOnly(categories.ToArray()));
    }

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
