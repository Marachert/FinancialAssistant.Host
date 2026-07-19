using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionInputParser
{
    Task<ParsedTransactionCandidate> ParseAsync(
        string normalizedInput,
        DateOnly currentDate,
        CancellationToken cancellationToken);
}
