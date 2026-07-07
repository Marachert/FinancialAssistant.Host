using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Authentication;

public interface IIdentityAuthenticationService
{
    Task<IdentityOperationResult<AuthSessionResponse>> RegisterAsync(RegisterAccountRequest request, string? idempotencyKey, string correlationId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult<AuthSessionResponse>> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);
}
