using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FinancialAssistant.Mcp.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Mcp.Api.Security;

public sealed class McpHeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "McpHeader";
    public const string SharedSecretConfigurationKey = "Mcp:Authentication:SharedSecret";
    private static readonly HashSet<string> AllowedRoles = new(
        [McpRoles.Admin, McpRoles.Operator, McpRoles.Developer, McpRoles.QualityAssurance],
        StringComparer.OrdinalIgnoreCase);
    private readonly byte[] expectedSecretHash;

    public McpHeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        var secret = configuration[SharedSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException(
                $"{SharedSecretConfigurationKey} must contain at least 32 characters.");
        }

        expectedSecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers[McpHeaders.Authentication];
        if (values.Count != 1 || string.IsNullOrEmpty(values[0]) || values[0]!.Length > 256
            || !CryptographicOperations.FixedTimeEquals(
                expectedSecretHash,
                SHA256.HashData(Encoding.UTF8.GetBytes(values[0]!))))
        {
            return Task.FromResult(AuthenticateResult.Fail("Trusted MCP authentication is required."));
        }

        var roles = Request.Headers[McpHeaders.Roles]
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries) ?? [])
            .Where(AllowedRoles.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roles.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("At least one allowlisted MCP role is required."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "trusted-internal-mcp-client"),
            new(ClaimTypes.Name, "trusted-internal-mcp-client")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role.ToLowerInvariant())));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationScheme));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, AuthenticationScheme)));
    }
}
