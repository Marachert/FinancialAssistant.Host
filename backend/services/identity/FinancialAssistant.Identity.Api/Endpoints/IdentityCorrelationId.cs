using System.Diagnostics;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static class IdentityCorrelationId
{
    public static string Resolve(HttpContext context)
    {
        var supplied = context.Request.Headers[IdentityApiHeaders.CorrelationId]
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();

        if (IsCanonical(supplied))
        {
            return supplied!;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("D");
    }

    private static bool IsCanonical(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (Guid.TryParseExact(value, "D", out _)
                || Guid.TryParseExact(value, "N", out _));
    }
}
