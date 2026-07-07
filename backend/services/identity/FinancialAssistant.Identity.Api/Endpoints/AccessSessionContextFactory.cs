using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinancialAssistant.Identity.Application.Sessions;

namespace FinancialAssistant.Identity.Api.Endpoints;

internal static class AccessSessionContextFactory
{
    public static bool TryCreate(ClaimsPrincipal principal, out AccessSessionContext context)
    {
        context = null!;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var accountId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var sessionId = principal.FindFirstValue("sid");
        var method = principal.FindFirstValue("amr");
        var issued = principal.FindFirstValue(JwtRegisteredClaimNames.Iat);
        var expires = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (string.IsNullOrWhiteSpace(accountId)
            || string.IsNullOrWhiteSpace(sessionId)
            || string.IsNullOrWhiteSpace(method)
            || !long.TryParse(issued, out var issuedSeconds)
            || !long.TryParse(expires, out var expiresSeconds))
        {
            return false;
        }

        context = new AccessSessionContext(
            accountId,
            sessionId,
            method,
            DateTimeOffset.FromUnixTimeSeconds(issuedSeconds),
            DateTimeOffset.FromUnixTimeSeconds(expiresSeconds));
        return true;
    }
}
