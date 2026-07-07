using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Providers.Google;

public interface IGoogleProviderAuthenticationService
{
    Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateAsync(
        GoogleSignInRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
