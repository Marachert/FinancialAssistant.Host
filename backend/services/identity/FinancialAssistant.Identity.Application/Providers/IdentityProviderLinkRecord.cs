namespace FinancialAssistant.Identity.Application.Providers;

public sealed record IdentityProviderLinkRecord(
    string Id,
    string AccountId,
    string Provider,
    string ProviderSubjectHash,
    string? ProviderTenantHash,
    DateTimeOffset LinkedAtUtc,
    DateTimeOffset LastAuthenticatedAtUtc);
