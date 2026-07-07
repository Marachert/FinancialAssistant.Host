namespace FinancialAssistant.Identity.Application.Authentication;

public sealed record EmailCredentialRecord(
    string Id,
    string AccountId,
    string LookupKeyHash,
    string SecretHash,
    string SecretHashAlgorithm,
    string SecretHashParameters,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastRotatedAtUtc = null);
