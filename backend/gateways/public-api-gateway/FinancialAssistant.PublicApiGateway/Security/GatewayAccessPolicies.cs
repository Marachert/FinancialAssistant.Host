namespace FinancialAssistant.PublicApiGateway.Security;

public static class GatewayAccessPolicies
{
    public const string Public = "public";
    public const string Authenticated = "authenticated";
    public const string Admin = "admin";

    public static string Normalize(string? accessPolicy)
    {
        if (string.IsNullOrWhiteSpace(accessPolicy))
        {
            return Authenticated;
        }

        return accessPolicy.Trim().ToLowerInvariant();
    }
}
