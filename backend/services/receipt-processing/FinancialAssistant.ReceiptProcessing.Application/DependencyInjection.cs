using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddReceiptProcessingApplication(this IServiceCollection services)
    {
        services.AddSingleton<IReceiptProcessingClock, SystemReceiptProcessingClock>();
        services.AddSingleton<IReceiptProcessingIdGenerator, GuidReceiptProcessingIdGenerator>();
        services.AddSingleton<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddSingleton<IReceiptUploadedConsumer, ReceiptOcrProcessor>();
        return services;
    }

    private sealed class SystemReceiptProcessingClock : IReceiptProcessingClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class GuidReceiptProcessingIdGenerator : IReceiptProcessingIdGenerator
    {
        public string CreateReceiptId() => $"receipt_{Guid.NewGuid():N}";

        public string CreateEventId() => $"event_{Guid.NewGuid():N}";
    }
}
