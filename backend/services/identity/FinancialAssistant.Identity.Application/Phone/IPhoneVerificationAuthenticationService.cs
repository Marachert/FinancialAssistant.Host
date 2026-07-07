using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Phone;

public interface IPhoneVerificationAuthenticationService
{
    Task<IdentityOperationResult<PhoneVerificationStartResponse>> StartAsync(
        PhoneVerificationStartRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<AuthSessionResponse>> ConfirmAsync(
        PhoneVerificationConfirmRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
