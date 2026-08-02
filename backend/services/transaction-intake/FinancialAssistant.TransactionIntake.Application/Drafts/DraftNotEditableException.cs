namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class DraftNotEditableException : Exception
{
    public DraftNotEditableException(string status)
        : base($"A transaction draft with status '{status}' cannot be changed.")
    {
    }
}

public sealed class DraftMutationConflictException : Exception
{
    public DraftMutationConflictException()
        : base("The transaction draft revision is stale. Reload the draft and retry with its current revision.")
    {
    }
}
