namespace FinancialAssistant.Category.Domain.Categories;

public static class DefaultCategoryTaxonomy
{
    private static readonly CategoryTemplate[] Templates =
    {
        new("income.salary", "Salary", CategoryKinds.Income, 10, new[] { "paycheck", "wages" }),
        new("income.other", "Other income", CategoryKinds.Income, 20, new[] { "bonus", "refund" }),
        new("expense.housing", "Housing", CategoryKinds.Expense, 100, new[] { "mortgage", "rent" }),
        new("expense.food", "Food and dining", CategoryKinds.Expense, 110, new[] { "cafe", "groceries", "restaurant" }),
        new("expense.transportation", "Transportation", CategoryKinds.Expense, 120, new[] { "fuel", "public transport", "taxi" }),
        new("expense.utilities", "Utilities", CategoryKinds.Expense, 130, new[] { "electricity", "internet", "water" }),
        new("expense.health", "Health", CategoryKinds.Expense, 140, new[] { "doctor", "medicine", "pharmacy" }),
        new("expense.education", "Education", CategoryKinds.Expense, 150, new[] { "books", "course", "tuition" }),
        new("expense.entertainment", "Entertainment", CategoryKinds.Expense, 160, new[] { "cinema", "games", "streaming" }),
        new("expense.shopping", "Shopping", CategoryKinds.Expense, 170, new[] { "clothing", "electronics", "store" }),
        new("expense.other", "Other expense", CategoryKinds.Expense, 180, new[] { "miscellaneous", "other" })
    };

    public static IReadOnlyList<CategoryDefinition> Create(DateTimeOffset createdAtUtc) =>
        Array.AsReadOnly(
            Templates
                .Select(template => CategoryDefinition.CreateDefault(
                    template.Key,
                    template.DisplayName,
                    template.Kind,
                    template.SortOrder,
                    template.Aliases,
                    createdAtUtc))
                .ToArray());

    private sealed record CategoryTemplate(
        string Key,
        string DisplayName,
        string Kind,
        int SortOrder,
        IReadOnlyList<string> Aliases);
}
