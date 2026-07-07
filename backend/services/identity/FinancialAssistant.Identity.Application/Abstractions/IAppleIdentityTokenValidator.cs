using FinancialAssistant.Identity.Application.Providers.Apple;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IAppleIdentityTokenValidator
{
    Task<AppleIdentityTokenValidationResult> ValidateAsync(
        string identityToken,
        string nonce,
        CancellationToken cancellationToken = default);
}
