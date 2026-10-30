using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Profile.Contracts;

namespace FinancialAssistant.Profile.Api.Security;

public sealed class ProfileFinancialReaderAuthenticator
{
    private readonly Dictionary<string, byte[]> readers = new(StringComparer.Ordinal);

    public ProfileFinancialReaderAuthenticator(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Profile:FinancialReaders:Enabled")) { return; }
        var secrets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reader in new[] { "analytics", "financial-score", "recommendations-notifications" })
        {
            var secret = configuration["Profile:FinancialReaders:" + reader + ":SharedSecret"];
            if (secret is not { Length: >= 32 and <= 256 } || secret.Any(c => c is < '!' or > '~') ||
                secret == configuration["Profile:Gateway:SharedSecret"] || !secrets.Add(secret))
            { throw new InvalidOperationException("Native Profile financial readers require distinct scoped secrets."); }
            readers.Add(reader, SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
        }
    }

    public bool IsAuthenticated(HttpContext context)
    {
        if (context.Request.Method != "POST" || !string.Equals(context.Request.Path.Value, ProfileFinancialSettingsApi.Path, StringComparison.Ordinal)) { return false; }
        var role = context.Request.Headers[ProfileFinancialSettingsApi.ReaderHeader];
        var supplied = context.Request.Headers[ProfileFinancialSettingsApi.AuthenticationHeader];
        return role.Count == 1 && role[0] is { } reader && readers.TryGetValue(reader, out var expected) &&
            supplied.Count == 1 && supplied[0] is { Length: >= 32 and <= 256 } secret && !secret.Any(c => c is < '!' or > '~') &&
            CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
    }
}
