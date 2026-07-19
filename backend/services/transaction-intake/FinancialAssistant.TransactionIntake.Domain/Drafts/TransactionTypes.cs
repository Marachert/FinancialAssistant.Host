namespace FinancialAssistant.TransactionIntake.Domain.Drafts;

public static class TransactionTypes
{
    public const string Expense = "expense";
    public const string Income = "income";
    public const string Transfer = "transfer";
    public const string Unknown = "unknown";

    public static bool IsSupported(string value) =>
        value is Expense or Income or Transfer or Unknown;
}
