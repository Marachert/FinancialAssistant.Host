namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record PhoneVerificationStartResponse(
    string VerificationId,
    string MaskedDestination,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc);
