namespace FinancialAssistant.Identity.Domain.Accounts;

public sealed record IdentityAccount
{
    private IdentityAccount(string id, IdentityAccountStatus status, IReadOnlyList<string> roles, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Status = status;
        Roles = roles;
        CreatedAtUtc = createdAtUtc;
    }

    public string Id { get; }
    public IdentityAccountStatus Status { get; }
    public IReadOnlyList<string> Roles { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public bool CanAuthenticate => Status == IdentityAccountStatus.Active;

    public static IdentityAccount Create(DateTimeOffset createdAtUtc) => new(
        Guid.NewGuid().ToString("N"),
        IdentityAccountStatus.Active,
        Array.AsReadOnly(new[] { "user" }),
        createdAtUtc);
}

public enum IdentityAccountStatus
{
    Active = 1,
    Locked = 2,
    Disabled = 3,
    DeletionPending = 4,
    Deleted = 5
}
