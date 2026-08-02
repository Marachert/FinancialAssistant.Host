using FinancialAssistant.Expense.Contracts;

namespace FinancialAssistant.Expense.Application;

public interface IExpenseManagementService
{
    Task<ExpenseRecordResponse> CreateAsync(
        string userId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseRecordResponse?> GetAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken);

    Task<ExpenseListResponse> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<ExpenseRecordResponse?> UpdateAsync(
        string userId,
        string expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseRecordResponse?> ArchiveAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken);

    Task<ExpenseRecordResponse?> RestoreAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken);
}

public sealed class ExpenseRecordNotEditableException : Exception
{
    public ExpenseRecordNotEditableException(string status)
        : base($"An Expense record with status '{status}' cannot be updated.")
    {
    }
}

public sealed class ExpenseMutationConflictException : Exception
{
    public ExpenseMutationConflictException()
        : base("The Expense record changed while the request was being processed. Retry the request.")
    {
    }
}
