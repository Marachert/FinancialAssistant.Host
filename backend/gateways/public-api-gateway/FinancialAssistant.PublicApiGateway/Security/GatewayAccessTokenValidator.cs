using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Primitives;

namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class GatewayAccessTokenValidator
{
    private const int MinimumSigningKeyBytes = 32;
    private const int MaximumBearerTokenLength = 8192;
    private const int MaximumOpaqueIdentifierLength = 128;
    private const int MaximumRoleLength = 64;

    private readonly GatewaySecurityOptions options;
    private readonly JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
    private readonly TokenValidationParameters? validationParameters;
    private readonly HashSet<string> publicEndpointSignatures;

    public GatewayAccessTokenValidator(IOptions<GatewaySecurityOptions> options)
    {
        this.options = options.Value;
        ValidateOptions(this.options);
        publicEndpointSignatures = this.options.PublicEndpoints
            .Select(endpoint => CreateEndpointSignature(endpoint.Method, endpoint.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (GatewaySecurityModes.IsEnforceMode(this.options.Mode))
        {
            validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.AccessTokenSigningKey)),
                ValidateIssuer = true,
                ValidIssuer = this.options.AccessTokenIssuer,
                ValidateAudience = true,
                ValidAudience = this.options.AccessTokenAudience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(this.options.ClockSkewSeconds),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            };
        }
    }

    public bool IsPublicEndpoint(HttpRequest request)
    {
        return publicEndpointSignatures.Contains(CreateEndpointSignature(request.Method, request.Path.Value ?? string.Empty));
    }

    public GatewayAccessTokenValidationResult Validate(IHeaderDictionary headers)
    {
        if (validationParameters is null)
        {
            throw new InvalidOperationException("Gateway access-token validation is unavailable outside enforce mode.");
        }

        var headerName = string.IsNullOrWhiteSpace(options.AuthenticationHeaderName)
            ? "Authorization"
            : options.AuthenticationHeaderName;

        if (!headers.TryGetValue(headerName, out var values) || values.Count == 0)
        {
            return GatewayAccessTokenValidationResult.Missing();
        }

        if (!TryReadBearerToken(values, out var token))
        {
            return GatewayAccessTokenValidationResult.Invalid();
        }

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt
                || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                return GatewayAccessTokenValidationResult.Invalid();
            }

            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var sessionId = principal.FindFirstValue("sid");
            if (!IsSafeOpaqueIdentifier(userId) || !IsSafeOpaqueIdentifier(sessionId))
            {
                return GatewayAccessTokenValidationResult.Invalid();
            }

            var roleClaims = principal.FindAll("role").Select(claim => claim.Value).ToArray();
            if (roleClaims.Any(role => !IsSafeRole(role)))
            {
                return GatewayAccessTokenValidationResult.Invalid();
            }

            var roles = roleClaims
                .Select(role => role.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray();

            return GatewayAccessTokenValidationResult.Valid(
                new GatewayUserContext(userId!, sessionId!, roles));
        }
        catch (SecurityTokenExpiredException)
        {
            return GatewayAccessTokenValidationResult.Expired();
        }
        catch (SecurityTokenException)
        {
            return GatewayAccessTokenValidationResult.Invalid();
        }
        catch (ArgumentException)
        {
            return GatewayAccessTokenValidationResult.Invalid();
        }
    }

    private static bool TryReadBearerToken(StringValues values, out string token)
    {
        token = string.Empty;
        if (values.Count != 1)
        {
            return false;
        }

        var header = values[0]?.Trim();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header[prefix.Length..].Trim();
        return token.Length > 0
            && token.Length <= MaximumBearerTokenLength
            && token.All(character => !char.IsWhiteSpace(character) && !char.IsControl(character));
    }

    private static bool IsSafeOpaqueIdentifier(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaximumOpaqueIdentifierLength
            && value.All(character => !char.IsControl(character));
    }

    private static bool IsSafeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) || role.Length > MaximumRoleLength)
        {
            return false;
        }

        return role.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_');
    }

    private static void ValidateOptions(GatewaySecurityOptions options)
    {
        if (!string.Equals(options.Mode, GatewaySecurityModes.Placeholder, StringComparison.OrdinalIgnoreCase)
            && !GatewaySecurityModes.IsEnforceMode(options.Mode))
        {
            throw new InvalidOperationException("Gateway security mode must be 'placeholder' or 'enforce'.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthenticationHeaderName)
            || options.AuthenticationHeaderName.Any(char.IsControl))
        {
            throw new InvalidOperationException("Gateway authentication header name is invalid.");
        }

        if (options.ClockSkewSeconds is < 0 or > 300)
        {
            throw new InvalidOperationException("Gateway access-token clock skew must be between 0 and 300 seconds.");
        }

        if (!IsSafeRole(options.AdminRole))
        {
            throw new InvalidOperationException("Gateway admin role is invalid.");
        }

        var endpointSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in options.PublicEndpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Method)
                || !endpoint.Method.All(character => char.IsLetter(character)))
            {
                throw new InvalidOperationException("Gateway public endpoint method is invalid.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.Path)
                || !endpoint.Path.StartsWith('/', StringComparison.Ordinal)
                || endpoint.Path.Contains('?', StringComparison.Ordinal)
                || endpoint.Path.Contains('#', StringComparison.Ordinal)
                || endpoint.Path.Any(char.IsControl))
            {
                throw new InvalidOperationException("Gateway public endpoint path is invalid.");
            }

            if (!endpointSignatures.Add(CreateEndpointSignature(endpoint.Method, endpoint.Path)))
            {
                throw new InvalidOperationException("Gateway public endpoint definitions must be unique.");
            }
        }

        if (!GatewaySecurityModes.IsEnforceMode(options.Mode))
        {
            return;
        }

        if (Encoding.UTF8.GetByteCount(options.AccessTokenSigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException("Gateway access-token signing key must contain at least 32 UTF-8 bytes in enforce mode.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessTokenIssuer)
            || string.IsNullOrWhiteSpace(options.AccessTokenAudience))
        {
            throw new InvalidOperationException("Gateway access-token issuer and audience are required in enforce mode.");
        }
    }

    private static string CreateEndpointSignature(string method, string path)
    {
        var normalizedPath = path.Length > 1 ? path.TrimEnd('/') : path;
        return $"{method.Trim().ToUpperInvariant()} {normalizedPath}";
    }
}

public enum GatewayAccessTokenStatus
{
    Valid,
    Missing,
    Invalid,
    Expired
}

public sealed record GatewayAccessTokenValidationResult(
    GatewayAccessTokenStatus Status,
    GatewayUserContext? UserContext)
{
    public static GatewayAccessTokenValidationResult Valid(GatewayUserContext userContext) =>
        new(GatewayAccessTokenStatus.Valid, userContext);

    public static GatewayAccessTokenValidationResult Missing() =>
        new(GatewayAccessTokenStatus.Missing, null);

    public static GatewayAccessTokenValidationResult Invalid() =>
        new(GatewayAccessTokenStatus.Invalid, null);

    public static GatewayAccessTokenValidationResult Expired() =>
        new(GatewayAccessTokenStatus.Expired, null);
}
