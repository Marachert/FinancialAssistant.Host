namespace FinancialAssistant.Category.Contracts;

public static class CategoryApiRoutes
{
    public const string Categories = "/categories";
    public const string CategoryAliases = "/categories/{categoryId}/aliases";
    public const string UserRegisteredEvent = "/internal/category/v1/events/user-registered";
}
