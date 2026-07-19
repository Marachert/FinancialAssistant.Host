namespace FinancialAssistant.TransactionIntake.Contracts;

public static class TransactionIntakeApiRoutes
{
    public const string Intake = "/api/v1/transactions/intake";

    public const string GatewayIntake = "/transactions/intake";

    public const string ConfirmDraft = "/api/v1/transactions/drafts/{draftId}/confirm";

    public const string GatewayConfirmDraft = "/transactions/drafts/{draftId}/confirm";
}
