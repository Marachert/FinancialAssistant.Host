using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Profile.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        if (windowsNative)
        {
            var settings = configuration?.GetSection("Profile:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Profile persistence configuration is missing.");
            var options = settings.CreateOptions("profile");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<PostgresProfileStore>();
            services.AddSingleton<IProfileStore>(provider => provider.GetRequiredService<PostgresProfileStore>());
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("profile-owned-storage", tags: new[] { "ready" });
            return services;
        }
        services.AddSingleton<IProfileStore, InMemoryProfileStore>();
        return services;
    }
}
