namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionIntakeMutationScope
{
    Task<T> RunAsync<T>(string owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
