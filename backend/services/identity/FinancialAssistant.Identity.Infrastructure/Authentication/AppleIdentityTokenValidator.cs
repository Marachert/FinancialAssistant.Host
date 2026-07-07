using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Providers.Apple;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class AppleIdentityTokenValidator : IAppleIdentityTokenValidator
{
    private readonly AppleIdentityProviderOptions options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> configurationManager;
    private readonly JsonWebTokenHandler tokenHandler = new();

    public AppleIdentityTokenValidator(IOptions<IdentityServiceOptions> options)
    {
        this.options = options.Value.Providers.Apple;
        configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            this.options.DiscoveryEndpoint,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<AppleIdentityTokenValidationResult> ValidateAsync(
        string identityToken,
        string nonce,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || options.ClientIds.Count == 0)
        {
            return AppleIdentityTokenValidationResult.Unavailable();
        }

        try
        {
            var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);
            var validation = await ValidateTokenAsync(identityToken, configuration);
            if (!validation.IsValid
                && validation.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                configurationManager.RequestRefresh();
                configuration = await configurationManager.GetConfigurationAsync(cancellationToken);
                validation = await ValidateTokenAsync(identityToken, configuration);
            }

            if (!validation.IsValid
                || validation.SecurityToken is not JsonWebToken token
                || !string.Equals(token.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            {
                return AppleIdentityTokenValidationResult.Invalid();
            }

            var subject = validation.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var tokenNonce = validation.ClaimsIdentity?.FindFirst("nonce")?.Value;
            if (string.IsNullOrWhiteSpace(subject)
                || options.RequireNonce && !AppleNonceVerifier.Matches(nonce, tokenNonce))
            {
                return AppleIdentityTokenValidationResult.Invalid();
            }

            var email = validation.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            var emailVerified = ReadBooleanClaim(validation.ClaimsIdentity?.FindFirst("email_verified")?.Value);
            var isPrivateEmail = ReadBooleanClaim(validation.ClaimsIdentity?.FindFirst("is_private_email")?.Value);
            return AppleIdentityTokenValidationResult.Valid(
                new AppleIdentityPrincipal(subject, email, emailVerified, isPrivateEmail));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or InvalidOperationException)
        {
            return AppleIdentityTokenValidationResult.Unavailable();
        }
    }

    private Task<TokenValidationResult> ValidateTokenAsync(
        string identityToken,
        OpenIdConnectConfiguration configuration)
    {
        var parameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudiences = options.ClientIds,
            RequireAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(Math.Max(0, options.ClockSkewSeconds))
        };

        return tokenHandler.ValidateTokenAsync(identityToken, parameters);
    }

    private static bool ReadBooleanClaim(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
