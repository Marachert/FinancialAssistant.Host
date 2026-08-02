using FinancialAssistant.Income.Domain;

namespace FinancialAssistant.Income.Application;

public interface IIncomeRecordEventPublisher
{
    Task PublishAsync(
        string eventType,
        IncomeRecord record,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken);
}

internal sealed class NullIncomeRecordEventPublisher : IIncomeRecordEventPublisher
{
    public static NullIncomeRecordEventPublisher Instance { get; } = new();

    public Task PublishAsync(
        string eventType,
        IncomeRecord record,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
