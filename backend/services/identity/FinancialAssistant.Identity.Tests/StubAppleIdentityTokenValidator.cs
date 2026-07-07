using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Providers.Apple;

namespace FinancialAssistant.Identity.Tests;

public sealed class StubAppleIdentityTokenValidator : IAppleIdentityTokenValidator
{
    private readonly ConcurrentDictionary<string, AppleIdentityTokenValidationResult> results = new(StringComparer.Ordinal);

    public void SetResult(string token, string nonce, AppleIdentityTokenValidationResult result)
    {
        results[CreateKey(token, nonce)] = result;
    }

    public Task<AppleIdentityTokenValidationResult> ValidateAsync(
        string identityToken,
        string nonce,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            results.TryGetValue(CreateKey(identityToken, nonce), out var result)
                ? result
                : AppleIdentityTokenValidationResult.Invalid());
    }

    private static string CreateKey(string token, string nonce) => $"{token}:{nonce}";
}
