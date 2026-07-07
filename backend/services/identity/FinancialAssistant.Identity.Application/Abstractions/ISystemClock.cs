namespace FinancialAssistant.Identity.Application.Abstractions;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
