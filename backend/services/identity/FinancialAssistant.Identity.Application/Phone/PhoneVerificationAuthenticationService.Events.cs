using FinancialAssistant.Identity.Application.Messaging;

namespace FinancialAssistant.Identity.Application.Phone;

public sealed partial class PhoneVerificationAuthenticationService
{
    private Task PublishRegistrationAsync(
        string accountId,
        string correlationId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            new IdentityEventPublication(
                "user.registered.v1",
                1,
                occurredAtUtc,
                correlationId,
                correlationId,
                accountId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["userId"] = accountId,
                    ["authenticationMethod"] = AuthenticationMethod
                }),
            cancellationToken);
    }

    private Task PublishSignInAsync(
        string accountId,
        string sessionId,
        string correlationId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            new IdentityEventPublication(
                "user.signed_in.v1",
                1,
                occurredAtUtc,
                correlationId,
                correlationId,
                accountId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["userId"] = accountId,
                    ["sessionId"] = sessionId,
                    ["authenticationMethod"] = AuthenticationMethod
                }),
            cancellationToken);
    }

    private Task PublishFailureAsync(
        string correlationId,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            new IdentityEventPublication(
                "authentication.failed.v1",
                1,
                clock.UtcNow,
                correlationId,
                correlationId,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["authenticationMethod"] = AuthenticationMethod,
                    ["reasonCode"] = "verification_not_accepted"
                }),
            cancellationToken);
    }
}
