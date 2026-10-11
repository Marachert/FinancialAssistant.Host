using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers.Apple;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

internal static class IdentityTransactionResult
{
    internal static Task<IdentityOperationResult<T>> RunAsync<T>(IdentityPersistenceContext context,
        Func<CancellationToken, Task<IdentityOperationResult<T>>> operation, CancellationToken cancellationToken) =>
        MapErrorsAsync(token => context.RunAsync(operation, token), cancellationToken);

    internal static async Task<IdentityOperationResult<T>> MapErrorsAsync<T>(
        Func<CancellationToken, Task<IdentityOperationResult<T>>> operation, CancellationToken cancellationToken)
    {
        try { return await operation(cancellationToken); }
        catch (Exception exception) when (exception is StorageUnavailableException or StorageConflictException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return IdentityOperationResult<T>.Failed(IdentityFailureKind.ServiceUnavailable,
                IdentityErrorCodes.ServiceUnavailable, "Identity operation unavailable.", "Please retry the operation later.");
        }
    }
}

internal sealed class TransactionalIdentityAuthenticationService(
    IdentityAuthenticationService inner, IdentityPersistenceContext context) : IIdentityAuthenticationService
{
    public Task<IdentityOperationResult<AuthSessionResponse>> RegisterAsync(RegisterAccountRequest request,
        string? idempotencyKey, string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.RegisterAsync(request, idempotencyKey, correlationId, token), cancellationToken);

    public Task<IdentityOperationResult<AuthSessionResponse>> SignInAsync(SignInRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.SignInAsync(request, correlationId, token), cancellationToken);
}

internal sealed class TransactionalIdentitySessionService(
    IdentitySessionService inner, IdentityPersistenceContext context) : IIdentitySessionService
{
    public Task<AuthSessionResponse> IssueAsync(IdentityAccount account, IdentityClientContext client,
        string authenticationMethod, CancellationToken cancellationToken = default) =>
        context.RunAsync(token => inner.IssueAsync(account, client, authenticationMethod, token), cancellationToken);

    public Task<IdentityOperationResult<AuthSessionResponse>> RefreshAsync(RefreshSessionRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.RefreshAsync(request, correlationId, token), cancellationToken);

    public Task<IdentityOperationResult<bool>> LogoutAsync(LogoutRequest request, AccessSessionContext access,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.LogoutAsync(request, access, correlationId, token), cancellationToken);

    public Task<IdentityOperationResult<CurrentUserContextResponse>> GetCurrentAsync(AccessSessionContext access,
        CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.GetCurrentAsync(access, token), cancellationToken);
}

internal sealed class TransactionalGoogleAuthenticationService(
    GoogleProviderAuthenticationService inner) : IGoogleProviderAuthenticationService
{
    public Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateAsync(GoogleSignInRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.MapErrorsAsync(token => inner.AuthenticateAsync(request, correlationId, token), cancellationToken);
}

internal sealed class TransactionalAppleAuthenticationService(
    AppleProviderAuthenticationService inner) : IAppleProviderAuthenticationService
{
    public Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateAsync(AppleSignInRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.MapErrorsAsync(token => inner.AuthenticateAsync(request, correlationId, token), cancellationToken);
}

internal sealed class TransactionalPhoneAuthenticationService(
    PhoneVerificationAuthenticationService inner, IdentityPersistenceContext context) : IPhoneVerificationAuthenticationService
{
    public Task<IdentityOperationResult<PhoneVerificationStartResponse>> StartAsync(PhoneVerificationStartRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.StartAsync(request, correlationId, token), cancellationToken);

    public Task<IdentityOperationResult<AuthSessionResponse>> ConfirmAsync(PhoneVerificationConfirmRequest request,
        string correlationId, CancellationToken cancellationToken = default) =>
        IdentityTransactionResult.RunAsync(context, token => inner.ConfirmAsync(request, correlationId, token), cancellationToken);
}
