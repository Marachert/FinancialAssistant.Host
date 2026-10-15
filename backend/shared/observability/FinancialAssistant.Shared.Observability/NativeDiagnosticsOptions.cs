namespace FinancialAssistant.Shared.Observability;

public sealed class NativeDiagnosticsOptions
{
    public bool Enabled { get; init; }
    public string RootPath { get; init; } = "";
    public string ServiceSid { get; init; } = "";
    public int RetentionHours { get; init; }
    public int MaximumBatches { get; init; } = 32;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RootPath) || string.IsNullOrWhiteSpace(ServiceSid) || RetentionHours is < 1 or > 24 || MaximumBatches is < 4 or > 128)
        { throw new InvalidOperationException("Native diagnostics require an owned protected directory and explicit bounded retention/capacity."); }
    }
}
