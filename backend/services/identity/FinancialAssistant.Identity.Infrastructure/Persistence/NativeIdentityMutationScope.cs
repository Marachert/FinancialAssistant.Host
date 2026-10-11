using FinancialAssistant.Identity.Application.Abstractions;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

internal sealed class NativeIdentityMutationScope(IdentityPersistenceContext context) : IIdentityMutationScope
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        context.RunAsync(operation, cancellationToken);
}
