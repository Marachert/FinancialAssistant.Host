using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Application;

public interface IExpenseRecordEventPublisher
{
    Task PublishAsync(
        string eventType,
        ExpenseRecord record,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken);
}

internal sealed class NullExpenseRecordEventPublisher : IExpenseRecordEventPublisher
{
    public static NullExpenseRecordEventPublisher Instance { get; } = new();

    public Task PublishAsync(
        string eventType,
        ExpenseRecord record,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
