namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityEventSubjectHasher
{
    string Hash(string subjectId);
}
