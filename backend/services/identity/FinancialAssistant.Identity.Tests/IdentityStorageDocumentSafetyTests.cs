using FinancialAssistant.Identity.Infrastructure.Storage.Documents;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityStorageDocumentSafetyTests
{
    private static readonly HashSet<string> ForbiddenPropertyNames = new(
        [
            "Password",
            "RawPassword",
            "AccessToken",
            "RefreshToken",
            "Email",
            "Phone",
            "ProviderSubject",
            "VerificationCode",
            "ResetToken"
        ],
        StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void StorageDocuments_DoNotExposePlaintextSecretOrIdentityFields()
    {
        var documentTypes = new[]
        {
            typeof(AccountDocument),
            typeof(CredentialMetadataDocument),
            typeof(SessionDocument),
            typeof(ProviderLinkDocument)
        };

        foreach (var documentType in documentTypes)
        {
            var unsafeProperty = documentType
                .GetProperties()
                .FirstOrDefault(property => ForbiddenPropertyNames.Contains(property.Name));

            Assert.Null(unsafeProperty);
        }
    }

    [Fact]
    public void SensitiveLookupAndSecretFields_AreExplicitlyHashOnly()
    {
        Assert.NotNull(typeof(CredentialMetadataDocument).GetProperty("LookupKeyHash"));
        Assert.NotNull(typeof(CredentialMetadataDocument).GetProperty("SecretHash"));
        Assert.NotNull(typeof(SessionDocument).GetProperty("RefreshTokenHash"));
        Assert.NotNull(typeof(ProviderLinkDocument).GetProperty("ProviderSubjectHash"));
    }
}
