namespace FinancialAssistant.Identity.Application.Authentication;

public sealed record IdentityOperationResult<T>(T? Value, IdentityOperationFailure? Failure)
{
    public bool IsSuccess => Failure is null;

    public static IdentityOperationResult<T> Success(T value) => new(value, null);

    public static IdentityOperationResult<T> Failed(
        IdentityFailureKind kind,
        string code,
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(default, new IdentityOperationFailure(kind, code, title, detail, errors));
}

public sealed record IdentityOperationFailure(
    IdentityFailureKind Kind,
    string Code,
    string Title,
    string Detail,
    IReadOnlyDictionary<string, string[]>? Errors);

public enum IdentityFailureKind
{
    Validation = 1,
    Conflict = 2,
    Authentication = 3,
    ServiceUnavailable = 4
}
