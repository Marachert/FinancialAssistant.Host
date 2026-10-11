using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Infrastructure.Events;
using FinancialAssistant.Category.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Category.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCategoryInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        if (windowsNative)
        {
            var settings = configuration?.GetSection("Category:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Category persistence configuration is missing.");
            var options = settings.CreateOptions("category");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<CategoryPersistenceContext>();
            services.AddSingleton<PostgresCategoryStore>();
            services.AddSingleton<ICategoryStore>(provider => provider.GetRequiredService<PostgresCategoryStore>());
            services.AddSingleton<PostgresCategoryEventPublisher>();
            services.AddSingleton<ICategoryEventPublisher>(provider => provider.GetRequiredService<PostgresCategoryEventPublisher>());
            services.AddHostedService<CategoryOutboxRetentionWorker>();
            services.RemoveAll<ICategoryService>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<ICategoryService, TransactionalCategoryService>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("category-owned-storage", tags: new[] { "ready" });
            return services;
        }
        services.AddSingleton<InMemoryCategoryStore>();
        services.AddSingleton<ICategoryStore>(provider =>
            provider.GetRequiredService<InMemoryCategoryStore>());
        services.AddSingleton<InMemoryCategoryEventPublisher>();
        services.AddSingleton<ICategoryEventPublisher>(provider =>
            provider.GetRequiredService<InMemoryCategoryEventPublisher>());
        return services;
    }
}
