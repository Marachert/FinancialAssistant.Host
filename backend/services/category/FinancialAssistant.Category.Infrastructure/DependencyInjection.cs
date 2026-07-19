using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Infrastructure.Events;
using FinancialAssistant.Category.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Category.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCategoryInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryCategoryStore>();
        services.AddSingleton<ICategoryStore>(provider =>
            provider.GetRequiredService<InMemoryCategoryStore>());
        services.AddSingleton<InMemoryCategoryEventPublisher>();
        services.AddSingleton<ICategoryEventPublisher>(provider =>
            provider.GetRequiredService<InMemoryCategoryEventPublisher>());
        return services;
    }
}
