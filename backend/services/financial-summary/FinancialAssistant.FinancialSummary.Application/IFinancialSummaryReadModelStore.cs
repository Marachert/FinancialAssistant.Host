using FinancialAssistant.FinancialSummary.Domain;

namespace FinancialAssistant.FinancialSummary.Application;

public interface IFinancialSummaryReadModelStore
{
    Task UpsertIfNewerAsync(
        FinancialRecordProjection projection,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialRecordProjection>> ListAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
