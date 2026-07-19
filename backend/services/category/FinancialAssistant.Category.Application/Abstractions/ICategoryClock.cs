namespace FinancialAssistant.Category.Application.Abstractions;

public interface ICategoryClock
{
    DateTimeOffset UtcNow { get; }
}
