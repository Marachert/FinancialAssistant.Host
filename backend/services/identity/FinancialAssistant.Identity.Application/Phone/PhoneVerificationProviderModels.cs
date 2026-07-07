namespace FinancialAssistant.Identity.Application.Phone;

public sealed record PhoneVerificationDispatchRequest(
    string VerificationId,
    string PhoneNumber,
    string Purpose,
    string CorrelationId);

public sealed record PhoneVerificationDispatchResult(
    PhoneVerificationDispatchStatus Status,
    string? ProviderReference,
    int? RetryAfterSeconds = null);

public enum PhoneVerificationDispatchStatus
{
    Accepted = 1,
    RateLimited = 2,
    Rejected = 3,
    Unavailable = 4
}

public sealed record PhoneVerificationCheckResult(PhoneVerificationCheckStatus Status);

public enum PhoneVerificationCheckStatus
{
    Approved = 1,
    Rejected = 2,
    Expired = 3,
    Unavailable = 4
}
