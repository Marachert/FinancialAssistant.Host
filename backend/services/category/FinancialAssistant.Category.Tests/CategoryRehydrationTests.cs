using FinancialAssistant.Category.Domain.Categories;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryRehydrationTests
{
    [Fact]
    public void RehydrationPreservesAliasesVersionPresentationAndTimeWithoutSharingLists()
    {
        var created = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        var aliases = new[] { "market", "shop" };
        var category = CategoryDefinition.Rehydrate("expense.food", "expense.food", "Food", "categories.expense.food",
            "category.icon.expense.food", "category.color.expense.food", CategoryKinds.Expense, 4, aliases, 17, created, created.AddHours(1));
        aliases[0] = "changed";
        Assert.Equal(17, category.Version);
        Assert.Equal("market", category.Aliases[0]);
        Assert.Equal(created, category.CreatedAtUtc);
        Assert.Equal(created.AddHours(1), category.UpdatedAtUtc);
        Assert.Equal("category.icon.expense.food", category.IconKey);
        Assert.Same(category, category.ReplaceAliases(["shop", " MARKET "], created.AddHours(2)));
        var catalog = UserCategoryCatalog.Rehydrate("synthetic-owner", [category]);
        Assert.Same(category, catalog.Find("expense.food"));
        Assert.Throws<ArgumentException>(() => UserCategoryCatalog.Rehydrate("synthetic-owner", [category, category]));
        Assert.Throws<ArgumentException>(() => CategoryDefinition.Rehydrate(category.Id, category.Key, category.DisplayName,
            category.LocalizationKey, category.IconKey, category.ColorKey, category.Kind, 4, ["shop", "market"], 17, created, created));
    }

    [Fact]
    public void AliasVersionCannotOverflowIntoInvalidPersistedState()
    {
        var now = DateTimeOffset.UtcNow;
        var category = CategoryDefinition.Rehydrate("expense.food", "expense.food", "Food", "categories.expense.food",
            "category.icon.expense.food", "category.color.expense.food", CategoryKinds.Expense, 4, ["market"], int.MaxValue, now, now);
        Assert.Throws<OverflowException>(() => category.ReplaceAliases(["shop"], now));
    }
}
