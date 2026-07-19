using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Application.Categories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Category.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCategoryApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<ICategoryClock, SystemCategoryClock>();
        services.AddSingleton<ICategoryService, CategoryService>();
        return services;
    }
}
