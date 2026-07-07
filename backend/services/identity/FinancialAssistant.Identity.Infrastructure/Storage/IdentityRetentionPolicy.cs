namespace FinancialAssistant.Identity.Infrastructure.Storage;

public sealed record IdentityRetentionPolicy(
    string Entity,
    string TerminalStateTrigger,
    TimeSpan RetainAfterTerminalState,
    TimeSpan? HardMaximumDocumentAge,
    string CleanupOwner,
    string Notes);

public static class IdentityRetentionPolicies
{
    public static readonly IReadOnlyList<IdentityRetentionPolicy> All = Array.AsReadOnly(
    [
        new(
            IdentityIndexCatalog.AccountsEntity,
            "account deletion accepted and no legal/security hold remains",
            TimeSpan.FromDays(30),
            null,
            "Identity Service account-deletion worker",
            "Active accounts are retained. Deleted account tombstones are removed after the recovery window."),
        new(
            IdentityIndexCatalog.CredentialsEntity,
            "credential removed, replaced, or owning account deleted",
            TimeSpan.FromDays(30),
            null,
            "Identity Service credential cleanup worker",
            "Only the current secret hash is stored. Superseded secret hashes are removed during rotation."),
        new(
            IdentityIndexCatalog.SessionsEntity,
            "session expired or revoked",
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(90),
            "Identity Service session cleanup worker",
            "Active sessions are never removed before expiry. Revoked and expired sessions remain briefly for replay detection."),
        new(
            IdentityIndexCatalog.ExternalIdentitiesEntity,
            "provider link removed or owning account deleted",
            TimeSpan.FromDays(30),
            null,
            "Identity Service provider-link cleanup worker",
            "Provider links are deleted after the unlink recovery window; audit history belongs to Audit Service events."),
    ]);
}
