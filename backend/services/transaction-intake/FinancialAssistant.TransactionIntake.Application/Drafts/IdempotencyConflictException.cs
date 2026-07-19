namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("The idempotency key was already used for different transaction input.")
    {
    }
}
