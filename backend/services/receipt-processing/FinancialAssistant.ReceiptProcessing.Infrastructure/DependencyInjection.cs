using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReceiptProcessingInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<EncryptedInMemoryReceiptObjectStore>();
        services.AddSingleton<IReceiptObjectStore>(provider =>
            provider.GetRequiredService<EncryptedInMemoryReceiptObjectStore>());
        services.AddSingleton<InMemoryReceiptMetadataStore>();
        services.AddSingleton<IReceiptMetadataStore>(provider =>
            provider.GetRequiredService<InMemoryReceiptMetadataStore>());
        services.AddSingleton<InMemoryOcrProcessingStore>();
        services.AddSingleton<IOcrProcessingStore>(provider =>
            provider.GetRequiredService<InMemoryOcrProcessingStore>());
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
        services.TryAddSingleton<IOcrProvider, ResilientOcrProvider>();
        return services;
    }
}
