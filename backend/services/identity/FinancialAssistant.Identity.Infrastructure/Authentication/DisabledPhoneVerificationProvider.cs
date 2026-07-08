using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class DisabledPhoneVerificationProvider : IPhoneVerificationProvider
{
    public Task<PhoneVerificationDispatchResult> StartAsync(
        PhoneVerificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PhoneVerificationDispatchResult(
            PhoneVerificationDispatchStatus.Unavailable,
            null));
    }

    public Task<PhoneVerificationCheckResult> CheckAsync(
        string providerReference,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PhoneVerificationCheckResult(
            PhoneVerificationCheckStatus.Unavailable));
    }
}
