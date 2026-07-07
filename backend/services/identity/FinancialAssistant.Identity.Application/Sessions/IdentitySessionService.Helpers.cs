using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Sessions;

public sealed partial class IdentitySessionService
{
    private async Task PublishRevocationAsync(
        IdentitySessionRecord session,
        string reason,
        string correlationId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await eventPublisher.PublishAsync(
            new IdentityEventPublication(
                "token.revoked.v1",
                "1",
                occurredAtUtc,
                correlationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["userId"] = session.AccountId,
                    ["sessionId"] = session.Id,
                    ["reason"] = reason
                }),
            cancellationToken);
    }

    private static IdentityOperationResult<T> ValidationFailed<T>(
        string title,
        IReadOnlyDictionary<string, string[]> errors)
    {
        return IdentityOperationResult<T>.Failed(
            IdentityFailureKind.Validation,
            IdentityErrorCodes.ValidationFailed,
            title,
            "Correct the highlighted fields and submit the request again.",
            errors);
    }

    private static IdentityOperationResult<T> SessionFailed<T>(string code, string detail)
    {
        return IdentityOperationResult<T>.Failed(
            IdentityFailureKind.Authentication,
            code,
            "Session operation failed.",
            detail);
    }
}
