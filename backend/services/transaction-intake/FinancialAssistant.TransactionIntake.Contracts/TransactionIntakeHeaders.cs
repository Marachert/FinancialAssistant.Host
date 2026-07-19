namespace FinancialAssistant.TransactionIntake.Contracts;

public static class TransactionIntakeHeaders
{
    public const string GatewayAuthentication = "X-Gateway-Authentication";
    public const string GatewayUserId = "X-Gateway-User-Id";
    public const string IdempotencyKey = "Idempotency-Key";
}
