using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Providers.Google;

namespace FinancialAssistant.Identity.Tests;

public sealed class StubGoogleIdentityTokenValidator : IGoogleIdentityTokenValidator
{
    private readonly ConcurrentDictionary<string, GoogleIdentityTokenValidationResult> results = new(StringComparer.Ordinal);

    public void SetResult(string token, GoogleIdentityTokenValidationResult result)
    {
        results[token] = result;
    }

    public Task<GoogleIdentityTokenValidationResult> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            results.TryGetValue(idToken, out var result)
                ? result
                : GoogleIdentityTokenValidationResult.Invalid());
    }
}
