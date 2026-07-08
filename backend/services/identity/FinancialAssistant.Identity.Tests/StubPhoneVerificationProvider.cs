using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;

namespace FinancialAssistant.Identity.Tests;

public sealed class StubPhoneVerificationProvider : IPhoneVerificationProvider
{
    public const string AcceptedCode = "246810";
    private readonly ConcurrentDictionary<string, PhoneBehavior> behaviors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PhoneBehavior> references = new(StringComparer.Ordinal);

    public void SetBehavior(
        string phoneNumber,
        PhoneVerificationDispatchStatus startStatus,
        PhoneVerificationCheckStatus checkStatus = PhoneVerificationCheckStatus.Approved,
        int? retryAfterSeconds = null)
    {
        behaviors[phoneNumber] = new PhoneBehavior(startStatus, checkStatus, retryAfterSeconds);
    }

    public Task<PhoneVerificationDispatchResult> StartAsync(
        PhoneVerificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var behavior = behaviors.TryGetValue(request.PhoneNumber, out var configured)
            ? configured
            : new PhoneBehavior(
                PhoneVerificationDispatchStatus.Accepted,
                PhoneVerificationCheckStatus.Approved,
                null);
        if (behavior.StartStatus != PhoneVerificationDispatchStatus.Accepted)
        {
            return Task.FromResult(new PhoneVerificationDispatchResult(
                behavior.StartStatus,
                null,
                behavior.RetryAfterSeconds));
        }

        var reference = $"test-phone-{request.VerificationId}";
        references[reference] = behavior;
        return Task.FromResult(new PhoneVerificationDispatchResult(
            PhoneVerificationDispatchStatus.Accepted,
            reference));
    }

    public Task<PhoneVerificationCheckResult> CheckAsync(
        string providerReference,
        string code,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!references.TryGetValue(providerReference, out var behavior))
        {
            return Task.FromResult(new PhoneVerificationCheckResult(
                PhoneVerificationCheckStatus.Rejected));
        }

        var status = behavior.CheckStatus == PhoneVerificationCheckStatus.Approved
            && !string.Equals(code, AcceptedCode, StringComparison.Ordinal)
                ? PhoneVerificationCheckStatus.Rejected
                : behavior.CheckStatus;
        return Task.FromResult(new PhoneVerificationCheckResult(status));
    }

    private sealed record PhoneBehavior(
        PhoneVerificationDispatchStatus StartStatus,
        PhoneVerificationCheckStatus CheckStatus,
        int? RetryAfterSeconds);
}
