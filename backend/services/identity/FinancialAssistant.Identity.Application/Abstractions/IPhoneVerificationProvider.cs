using FinancialAssistant.Identity.Application.Phone;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IPhoneVerificationProvider
{
    Task<PhoneVerificationDispatchResult> StartAsync(
        PhoneVerificationDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task<PhoneVerificationCheckResult> CheckAsync(
        string providerReference,
        string code,
        CancellationToken cancellationToken = default);
}
