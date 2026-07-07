namespace FinancialAssistant.Identity.Contracts.Auth;

public static class IdentityApiHeaders
{
    public const string Authorization = "Authorization";
    public const string CorrelationId = "X-Correlation-Id";
    public const string IdempotencyKey = "Idempotency-Key";
}
