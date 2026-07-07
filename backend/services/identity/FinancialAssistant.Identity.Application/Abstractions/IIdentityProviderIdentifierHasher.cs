namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityProviderIdentifierHasher
{
    string Hash(string provider, string identifierType, string value);
}
