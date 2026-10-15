using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Usage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReceiptProcessingInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        if (windowsNative)
        {
            var files = configuration?.GetSection("ReceiptProcessing:Storage:Files").Get<NativeReceiptObjectOptions>()
                ?? throw new InvalidOperationException("Native receipt file configuration is required.");
            files.Validate();
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton(files);
            services.AddSingleton<NativeReceiptObjectStore>();
            services.AddSingleton<IReceiptObjectStore>(provider => provider.GetRequiredService<NativeReceiptObjectStore>());
            services.AddSingleton<PendingNativeReceiptMetadata>();
            services.AddSingleton<IReceiptMetadataStore>(provider => provider.GetRequiredService<PendingNativeReceiptMetadata>());
            services.AddSingleton<IOcrProcessingStore>(provider => provider.GetRequiredService<PendingNativeReceiptMetadata>());
            services.AddHostedService<NativeReceiptRetentionWorker>();
            services.AddHealthChecks().AddCheck("receipt-native-workflow", () =>
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Durable metadata and delivery qualification remain FIN-285/276."), tags: new[] { "ready" });
        }
        else
        {
            services.AddSingleton<EncryptedInMemoryReceiptObjectStore>();
            services.AddSingleton<IReceiptObjectStore>(provider => provider.GetRequiredService<EncryptedInMemoryReceiptObjectStore>());
            services.AddSingleton<InMemoryReceiptMetadataStore>();
            services.AddSingleton<IReceiptMetadataStore>(provider => provider.GetRequiredService<InMemoryReceiptMetadataStore>());
            services.AddSingleton<InMemoryOcrProcessingStore>();
            services.AddSingleton<IOcrProcessingStore>(provider => provider.GetRequiredService<InMemoryOcrProcessingStore>());
        }
        services.AddSingleton<InMemoryReceiptUploadedPublisher>();
        services.AddSingleton<IReceiptUploadedPublisher>(provider =>
            provider.GetRequiredService<InMemoryReceiptUploadedPublisher>());
        services.AddSingleton<InMemoryOcrCompletedPublisher>();
        services
            .AddHttpClient(
                HttpOcrCompletedPublisher.ClientName,
                client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IOcrCompletedPublisher, HttpOcrCompletedPublisher>();
        services.AddSingleton<IOcrCandidateNormalizer, DeterministicReceiptCandidateNormalizer>();
        services.TryAddSingleton<IOcrProviderClient, DisabledOcrProviderClient>();
        services.TryAddSingleton(provider =>
            OcrProviderResilienceOptions.FromConfiguration(
                provider.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton(provider =>
            provider.GetRequiredService<OcrProviderResilienceOptions>()
                .UsageCostControlPolicy);
        services.TryAddSingleton<IOcrUsageLimiter, InMemoryOcrUsageLimiter>();
        services.TryAddSingleton<IOcrProvider>(provider =>
        {
            var options = provider.GetRequiredService<OcrProviderResilienceOptions>();
            if (!options.Enabled)
            {
                return new ResilientOcrProvider(
                    new DisabledOcrProviderClient(),
                    options);
            }

            var matchingClients = provider
                .GetServices<IOcrProviderClient>()
                .Where(client => string.Equals(
                    client.Name,
                    options.ProviderName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matchingClients.Length != 1)
            {
                throw new InvalidOperationException(
                    "Exactly one OCR provider adapter matching the configured provider name must be registered.");
            }

            return new ResilientOcrProvider(matchingClients[0], options);
        });
        return services;
    }
}
