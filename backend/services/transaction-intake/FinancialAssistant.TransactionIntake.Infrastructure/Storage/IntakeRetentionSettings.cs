namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class IntakeRetentionSettings
{
    public int SourcePayloadRetentionHours { get; init; }

    public void Validate()
    {
        if (SourcePayloadRetentionHours is < 1 or > 24)
        { throw new InvalidOperationException("Native Intake requires explicit source retention between 1 and 24 hours."); }
    }
}
