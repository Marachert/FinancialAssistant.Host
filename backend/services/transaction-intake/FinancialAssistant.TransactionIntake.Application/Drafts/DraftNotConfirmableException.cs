namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class DraftNotConfirmableException : Exception
{
    public DraftNotConfirmableException()
        : base("Only complete income or expense drafts without ambiguities can be confirmed.")
    {
    }
}
