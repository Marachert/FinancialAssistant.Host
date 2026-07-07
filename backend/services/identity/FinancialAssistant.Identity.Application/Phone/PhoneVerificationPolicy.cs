namespace FinancialAssistant.Identity.Application.Phone;

public sealed record PhoneVerificationPolicy(
    TimeSpan ChallengeLifetime,
    TimeSpan ResendCooldown,
    TimeSpan StartWindow,
    int MaximumAttempts,
    int MaximumStartsPerPhone,
    int MaximumStartsPerClient,
    int CodeLength);
