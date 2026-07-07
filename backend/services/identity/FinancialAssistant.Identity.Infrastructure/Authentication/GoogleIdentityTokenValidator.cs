using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class GoogleIdentityTokenValidator : IGoogleIdentityTokenValidator
{
    private readonly GoogleIdentityProviderOptions options;

    public GoogleIdentityTokenValidator(IOptions<IdentityServiceOptions> options)
    {
        this.options = options.Value.Providers.Google;
    }

    public async Task<GoogleIdentityTokenValidationResult> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || options.ClientIds.Count == 0)
        {
            return GoogleIdentityTokenValidationResult.Unavailable();
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = options.ClientIds,
            HostedDomain = string.IsNullOrWhiteSpace(options.HostedDomain)
                ? null
                : options.HostedDomain,
            IssuedAtClockTolerance = TimeSpan.FromSeconds(
                Math.Max(0, options.IssuedAtClockToleranceSeconds)),
            ExpirationTimeClockTolerance = TimeSpan.FromSeconds(
                Math.Max(0, options.ExpirationClockToleranceSeconds))
        };

        try
        {
            var payload = await GoogleJsonWebSignature
                .ValidateAsync(idToken, settings)
                .WaitAsync(cancellationToken);
            return GoogleIdentityTokenValidationResult.Valid(
                new GoogleIdentityPrincipal(
                    payload.Subject,
                    payload.Email,
                    payload.EmailVerified,
                    payload.HostedDomain));
        }
        catch (InvalidJwtException)
        {
            return GoogleIdentityTokenValidationResult.Invalid();
        }
        catch (HttpRequestException)
        {
            return GoogleIdentityTokenValidationResult.Unavailable();
        }
        catch (IOException)
        {
            return GoogleIdentityTokenValidationResult.Unavailable();
        }
    }
}
