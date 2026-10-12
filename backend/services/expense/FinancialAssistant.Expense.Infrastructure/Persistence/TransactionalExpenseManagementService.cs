using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;

namespace FinancialAssistant.Expense.Infrastructure.Persistence;

internal sealed class TransactionalExpenseManagementService(ExpenseManagementService inner, ExpensePersistenceContext context) : IExpenseManagementService
{
    public Task<ExpenseRecordResponse> CreateAsync(string userId, CreateExpenseRequest request, CancellationToken token)
    {
        inner.ValidateCreateRequest(userId, request);
        return context.RunAsync(Owner(userId), cancellation => inner.CreateAsync(userId, request, cancellation), token);
    }
    public Task<ExpenseRecordResponse?> GetAsync(string userId, string id, CancellationToken token) => inner.GetAsync(userId, id, token);
    public Task<ExpenseListResponse> ListAsync(string userId, DateOnly from, DateOnly to, bool includeArchived, CancellationToken token) =>
        inner.ListAsync(userId, from, to, includeArchived, token);
    public Task<ExpenseRecordResponse?> UpdateAsync(string userId, string id, UpdateExpenseRequest request, CancellationToken token)
    {
        inner.ValidateUpdateRequest(userId, id, request);
        return context.RunAsync(Owner(userId), cancellation => inner.UpdateAsync(userId, id, request, cancellation), token);
    }
    public Task<ExpenseRecordResponse?> ArchiveAsync(string userId, string id, CancellationToken token)
    {
        _ = Owner(id);
        return context.RunAsync(Owner(userId), cancellation => inner.ArchiveAsync(userId, id, cancellation), token);
    }
    public Task<ExpenseRecordResponse?> RestoreAsync(string userId, string id, CancellationToken token)
    {
        _ = Owner(id);
        return context.RunAsync(Owner(userId), cancellation => inner.RestoreAsync(userId, id, cancellation), token);
    }
    private static string Owner(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { throw new ArgumentException("User ID is required."); }
        var owner = value.Trim();
        ExpenseRecordSnapshot.ValidateIdentifier(owner);
        return owner;
    }
}
