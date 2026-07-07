namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IRefreshTokenService
{
    string Create(string sessionId);

    bool TryReadSessionId(string refreshToken, out string sessionId);

    string Hash(string refreshToken);
}
