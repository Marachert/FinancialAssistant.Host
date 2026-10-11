namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityMutationScope
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

public sealed class DirectIdentityMutationScope : IIdentityMutationScope
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return operation(cancellationToken);
    }
}
