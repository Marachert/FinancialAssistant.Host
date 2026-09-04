namespace FinancialAssistant.Shared.Observability;

public static class SafeErrorFields
{
    public static IReadOnlyDictionary<string, string> From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new Dictionary<string, string>
        {
            ["FailureType"] = exception.GetType().Name
        };
    }
}
