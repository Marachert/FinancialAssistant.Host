using System.Security.Cryptography;
using FinancialAssistant.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class AspNetCorePasswordCredentialHasher : IPasswordCredentialHasher
{
    private const string DummyAccountId = "identity-dummy-account";
    private readonly PasswordHasher<string> hasher = new();
    private readonly string dummyHash;

    public AspNetCorePasswordCredentialHasher()
    {
        var dummySecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        dummyHash = hasher.HashPassword(DummyAccountId, dummySecret);
    }

    public PasswordHashResult Hash(string accountId, string password)
    {
        var value = hasher.HashPassword(accountId, password);
        return new PasswordHashResult(value, "aspnetcore-identity-v3", "embedded");
    }

    public PasswordVerificationOutcome Verify(string accountId, string passwordHash, string providedPassword)
    {
        return hasher.VerifyHashedPassword(accountId, passwordHash, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed
        };
    }

    public void VerifyDummy(string providedPassword)
    {
        _ = hasher.VerifyHashedPassword(DummyAccountId, dummyHash, providedPassword);
    }
}
