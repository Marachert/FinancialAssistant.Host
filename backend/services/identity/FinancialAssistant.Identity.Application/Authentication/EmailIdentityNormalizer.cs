using System.Text;

namespace FinancialAssistant.Identity.Application.Authentication;

public static class EmailIdentityNormalizer
{
    public static string Normalize(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
    }
}
