using FinancialAssistant.Income.Contracts;

namespace FinancialAssistant.Income.Application;

public interface IIncomeManagementService
{
    Task<IncomeRecordResponse> CreateAsync(
        string userId,
        CreateIncomeRequest request,
        CancellationToken cancellationToken);

    Task<IncomeRecordResponse?> GetAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken);

    Task<IncomeListResponse> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<IncomeRecordResponse?> UpdateAsync(
        string userId,
        string incomeId,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken);

    Task<IncomeRecordResponse?> ArchiveAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken);

    Task<IncomeRecordResponse?> RestoreAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken);
}

public sealed class IncomeRecordNotEditableException : Exception
{
    public IncomeRecordNotEditableException(string status)
        : base($"An Income record with status '{status}' cannot be updated.")
    {
    }
}

public sealed class IncomeMutationConflictException : Exception
{
    public IncomeMutationConflictException()
        : base("The Income record changed while the request was being processed. Retry the request.")
    {
    }
}
