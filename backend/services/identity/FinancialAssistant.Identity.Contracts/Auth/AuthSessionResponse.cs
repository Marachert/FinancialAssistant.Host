namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record AuthSessionResponse(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    CurrentUserContextResponse User);
