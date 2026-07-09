namespace FinancialAssistant.Profile.Application.Abstractions;

public interface IProfileClock
{
    DateTimeOffset UtcNow { get; }
}
