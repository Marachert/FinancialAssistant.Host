namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IEmailLookupHasher
{
    string Hash(string normalizedEmail);
}
