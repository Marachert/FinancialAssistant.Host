using FinancialAssistant.AiOrchestration.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.AiOrchestration.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiOrchestrationApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<IAiOrchestrationClock, SystemAiOrchestrationClock>();
        services.TryAddSingleton<IAiCallIdGenerator, GuidAiCallIdGenerator>();
        services.AddSingleton<IAiOrchestrationService, AiOrchestrationService>();
        return services;
    }

    private sealed class SystemAiOrchestrationClock : IAiOrchestrationClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class GuidAiCallIdGenerator : IAiCallIdGenerator
    {
        public string CreateCallId() => $"aicall_{Guid.NewGuid():N}";
    }
}
