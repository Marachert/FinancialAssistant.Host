using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeConfigurationRotationTests
{
    [Theory]
    [InlineData("data-index")]
    [InlineData("encryption-reference")]
    [InlineData("drop-retained-key")]
    [InlineData("database")]
    [InlineData("same-revision")]
    [InlineData("installation")]
    public void RotationCannotDiscardDataIdentityOrRetainedKeys(string mutation)
    {
        var document = NativeServiceConfigurationTests.Document("income");
        document.SecretReferences.Add(new() { Key = "Income:Storage:Postgres:EncryptionKeys:v2", Id = Guid.NewGuid() });
        var previous = NativeServiceConfigurationPlan.Create(document);
        var references = document.SecretReferences.Select(reference => new NativeConfigurationReference
        { Key = reference.Key, Id = mutation == "data-index" && reference.Key.EndsWith(":IndexKey") || mutation == "encryption-reference" && reference.Key.EndsWith(":v1") ? Guid.NewGuid() : reference.Id }).ToList();
        if (mutation == "drop-retained-key") { references.RemoveAll(reference => reference.Key.EndsWith(":v2")); }
        var settings = document.Settings.ToList();
        if (mutation == "database")
        { settings.RemoveAll(setting => setting.Key.EndsWith(":Database")); settings.Add(NativeConfigurationSetting.Text("Income:Storage:Postgres:Database", "fa_other")); }
        var next = NativeServiceConfigurationPlan.Create(new NativeConfigurationDocument
        {
            SchemaVersion = 1,
            InstallationId = mutation == "installation" ? Guid.NewGuid() : document.InstallationId,
            ServiceId = document.ServiceId,
            ServiceSid = document.ServiceSid,
            Revision = mutation == "same-revision" ? document.Revision : Guid.NewGuid(),
            Settings = settings,
            SecretReferences = references
        });
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationRotationPlan.Create(previous, next));
    }

    [Fact]
    public void AppendingEncryptionKeyRetainsOldReferencesAndRequiresControlledRestart()
    {
        var document = NativeServiceConfigurationTests.Document("income");
        var previous = NativeServiceConfigurationPlan.Create(document);
        document.SecretReferences.Add(new() { Key = "Income:Storage:Postgres:EncryptionKeys:v2", Id = Guid.NewGuid() });
        document.Settings.RemoveAll(setting => setting.Key.EndsWith(":ActiveEncryptionKeyId"));
        document.Settings.Add(NativeConfigurationSetting.Text("Income:Storage:Postgres:ActiveEncryptionKeyId", "v2"));
        var next = NativeServiceConfigurationPlan.Create(new NativeConfigurationDocument
        {
            SchemaVersion = 1,
            InstallationId = document.InstallationId,
            ServiceId = document.ServiceId,
            ServiceSid = document.ServiceSid,
            Revision = Guid.NewGuid(),
            Settings = document.Settings,
            SecretReferences = document.SecretReferences
        });
        var rotation = NativeConfigurationRotationPlan.Create(previous, next);
        Assert.True(rotation.RequiresControlledRestart); Assert.False(rotation.RequiresCredentialCoordination);
        Assert.Empty(rotation.ChangedCredentialKeys);
    }
}
