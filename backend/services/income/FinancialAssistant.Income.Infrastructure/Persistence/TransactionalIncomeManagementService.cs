using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;

namespace FinancialAssistant.Income.Infrastructure.Persistence;

internal sealed class TransactionalIncomeManagementService(IncomeManagementService inner, IncomePersistenceContext context) : IIncomeManagementService
{
    public Task<IncomeRecordResponse> CreateAsync(string userId, CreateIncomeRequest request, CancellationToken token)
    {
        inner.ValidateCreateRequest(userId, request);
        return context.RunAsync(Owner(userId), cancellation => inner.CreateAsync(userId, request, cancellation), token);
    }
    public Task<IncomeRecordResponse?> GetAsync(string userId, string id, CancellationToken token) => inner.GetAsync(userId, id, token);
    public Task<IncomeListResponse> ListAsync(string userId, DateOnly from, DateOnly to, bool includeArchived, CancellationToken token) =>
        inner.ListAsync(userId, from, to, includeArchived, token);
    public Task<IncomeRecordResponse?> UpdateAsync(string userId, string id, UpdateIncomeRequest request, CancellationToken token)
    {
        inner.ValidateUpdateRequest(userId, id, request);
        return context.RunAsync(Owner(userId), cancellation => inner.UpdateAsync(userId, id, request, cancellation), token);
    }
    public Task<IncomeRecordResponse?> ArchiveAsync(string userId, string id, CancellationToken token)
    {
        _ = Owner(id);
        return context.RunAsync(Owner(userId), cancellation => inner.ArchiveAsync(userId, id, cancellation), token);
    }
    public Task<IncomeRecordResponse?> RestoreAsync(string userId, string id, CancellationToken token)
    {
        _ = Owner(id);
        return context.RunAsync(Owner(userId), cancellation => inner.RestoreAsync(userId, id, cancellation), token);
    }
    private static string Owner(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { throw new ArgumentException("User ID is required."); }
        var owner = value.Trim();
        IncomeRecordSnapshot.ValidateIdentifier(owner);
        return owner;
    }
}
