using FinancialAssistant.Category.Application.Abstractions;

namespace FinancialAssistant.Category.Application;

public sealed class SystemCategoryClock : ICategoryClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
