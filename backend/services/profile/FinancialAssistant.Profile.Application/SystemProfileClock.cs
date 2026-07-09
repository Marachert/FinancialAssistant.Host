using FinancialAssistant.Profile.Application.Abstractions;

namespace FinancialAssistant.Profile.Application;

public sealed class SystemProfileClock : IProfileClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
