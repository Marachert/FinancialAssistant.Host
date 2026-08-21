namespace FinancialAssistant.TransactionIntake.Contracts;

public static class TransactionIntakeApiRoutes
{
    public const string Intake = "/api/v1/transactions/intake";

    public const string GatewayIntake = "/transactions/intake";

    public const string ReviewDraft = "/api/v1/transactions/drafts/{draftId}";

    public const string GatewayReviewDraft = "/transactions/drafts/{draftId}";

    public const string ReviewReceiptDraft = "/api/v1/transactions/drafts/receipts/{receiptId}";

    public const string GatewayReviewReceiptDraft = "/transactions/drafts/receipts/{receiptId}";

    public const string ConfirmDraft = "/api/v1/transactions/drafts/{draftId}/confirm";

    public const string GatewayConfirmDraft = "/transactions/drafts/{draftId}/confirm";

    public const string RejectDraft = "/api/v1/transactions/drafts/{draftId}/reject";

    public const string GatewayRejectDraft = "/transactions/drafts/{draftId}/reject";
}
