using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Sessions;

internal static class IdentitySessionRequestValidator
{
    private static readonly HashSet<string> SupportedPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        "ios",
        "android",
        "web"
    };

    public static IReadOnlyDictionary<string, string[]> Validate(
        string renewalValue,
        IdentityClientContext? client)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(renewalValue) || renewalValue.Length is < 32 or > 4096)
        {
            errors["refreshToken"] = new[] { "A valid session renewal value is required." };
        }

        if (client is null)
        {
            errors["client"] = new[] { "Client context is required." };
            return errors;
        }

        if (string.IsNullOrWhiteSpace(client.ClientInstanceId)
            || client.ClientInstanceId.Length is < 8 or > 128)
        {
            errors["client.clientInstanceId"] = new[] { "Client instance identifier is invalid." };
        }

        if (string.IsNullOrWhiteSpace(client.Platform) || !SupportedPlatforms.Contains(client.Platform))
        {
            errors["client.platform"] = new[] { "Platform must be ios, android, or web." };
        }

        return errors;
    }
}
