using FinancialAssistant.Identity.Application.Abstractions;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
