namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IPasswordCredentialHasher
{
    PasswordHashResult Hash(string accountId, string password);
    PasswordVerificationOutcome Verify(string accountId, string passwordHash, string providedPassword);
    void VerifyDummy(string providedPassword);
}

public sealed record PasswordHashResult(string Hash, string Algorithm, string Parameters);

public enum PasswordVerificationOutcome
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}
