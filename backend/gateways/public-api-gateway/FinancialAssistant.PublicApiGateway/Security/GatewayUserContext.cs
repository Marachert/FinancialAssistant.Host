namespace FinancialAssistant.PublicApiGateway.Security;

public sealed record GatewayUserContext(
    string UserId,
    string SessionId,
    IReadOnlyList<string> Roles)
{
    public const string ContextItemKey = "FinancialAssistant.GatewayUserContext";

    public bool IsInRole(string role) =>
        Roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));

    public static GatewayUserContext? Get(HttpContext context)
    {
        return context.Items.TryGetValue(ContextItemKey, out var value)
            ? value as GatewayUserContext
            : null;
    }
}

public static class GatewayUserContextHeaders
{
    public const string UserId = "X-Gateway-User-Id";
    public const string SessionId = "X-Gateway-Session-Id";
    public const string Roles = "X-Gateway-Roles";
    public const string LegacyAdminScope = "X-Gateway-Admin-Scope";
}
