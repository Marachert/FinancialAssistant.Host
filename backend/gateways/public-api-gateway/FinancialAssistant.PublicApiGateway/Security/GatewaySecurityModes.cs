namespace FinancialAssistant.PublicApiGateway.Security;

public static class GatewaySecurityModes
{
    public const string Placeholder = "placeholder";
    public const string Enforce = "enforce";

    public static bool IsEnforceMode(string? mode)
    {
        return string.Equals(mode, Enforce, StringComparison.OrdinalIgnoreCase);
    }
}
