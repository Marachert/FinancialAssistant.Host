using FinancialAssistant.FinancialScore.Domain;

namespace FinancialAssistant.FinancialScore.Application;

public interface IFinancialScoreProfileSettingsProvider
{
    Task<FinancialScoreProfileSettings> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);
}
