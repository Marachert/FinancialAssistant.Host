using FinancialAssistant.Identity.Application.Providers.Google;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IGoogleIdentityTokenValidator
{
    Task<GoogleIdentityTokenValidationResult> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
