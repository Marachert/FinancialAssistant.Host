using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Providers.Apple;

public interface IAppleProviderAuthenticationService
{
    Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateAsync(
        AppleSignInRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
