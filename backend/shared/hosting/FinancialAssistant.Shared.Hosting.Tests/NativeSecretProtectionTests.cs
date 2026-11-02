using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeSecretProtectionTests
{
    private const string ServiceSid = "S-1-5-80-1-2-3-4-5";

    [Theory]
    [InlineData("gateway", "Gateway:Security:AccessTokenSigningKey")]
    [InlineData("identity", "Identity:Storage:Postgres:EncryptionKeys:v1")]
    [InlineData("income", "Income:Messaging:ConnectionString")]
    [InlineData("analytics", "Analytics:Profile:SharedSecret")]
    [InlineData("profile", "Profile:FinancialReaders:financial-score:SharedSecret")]
    [InlineData("receipt-processing", "ReceiptProcessing:Storage:Files:EncryptionKeys:v2")]
    public void BindingAcceptsOnlyAnOwnedRuntimeSecret(string service, string key)
    {
        var binding = NativeSecretBinding.Create(Guid.NewGuid(), service, ServiceSid, key, Guid.NewGuid());
        Assert.Equal(key, binding.ConfigurationKey);
        Assert.Equal(service, binding.ServiceId);
    }

    [Theory]
    [InlineData("gateway", "Income:Storage:Postgres:Password")]
    [InlineData("income", "Income:Storage:Postgres:MigrationPassword")]
    [InlineData("identity", "Identity:Storage:Postgres:EncryptionKeys:../foreign")]
    [InlineData("identity", "Identity:Storage:Postgres:EncryptionKeys:V1")]
    [InlineData("profile", "Profile:FinancialReaders:foreign:SharedSecret")]
    [InlineData("foreign", "Gateway:Security:AccessTokenSigningKey")]
    public void CrossServiceInstallerAndUnknownSecretsCannotBindToRuntime(string service, string key) =>
        Assert.Throws<ArgumentException>(() => NativeSecretBinding.Create(Guid.NewGuid(), service, ServiceSid, key, Guid.NewGuid()));

    [Theory]
    [InlineData("S-1-5-21-1-2-3")]
    [InlineData("S-1-5-80-1")]
    [InlineData("S-1-5-80-1-2-3-4-05")]
    public void BindingRejectsNonCanonicalVirtualServiceIdentity(string sid) =>
        Assert.Throws<ArgumentException>(() => NativeSecretBinding.Create(Guid.NewGuid(), "gateway", sid,
            "Gateway:Security:AccessTokenSigningKey", Guid.NewGuid()));

    [Fact]
    public void EmptyInstallationOrReferenceCannotCreateAReusableUnscopedBinding()
    {
        Assert.Throws<ArgumentException>(() => NativeSecretBinding.Create(Guid.Empty, "gateway", ServiceSid, "Gateway:Security:AccessTokenSigningKey", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => NativeSecretBinding.Create(Guid.NewGuid(), "gateway", ServiceSid, "Gateway:Security:AccessTokenSigningKey", Guid.Empty));
    }

    [Fact]
    public void PayloadSizesAreBoundedWithoutEchoingInput()
    {
        var protector = new WindowsDpapiSecretProtector(); var binding = Binding();
        Assert.Throws<ArgumentException>(() => protector.Protect(Array.Empty<byte>(), binding));
        Assert.Throws<ArgumentException>(() => protector.Protect(new byte[8193], binding));
        Assert.Throws<ArgumentException>(() => protector.Unprotect(new byte[16385], binding));
    }

    [Fact]
    public void ActualWindowsDpapiRoundtripBindsReferenceAndRejectsTampering()
    {
        var protector = new WindowsDpapiSecretProtector(); var binding = Binding();
        var input = Encoding.UTF8.GetBytes("synthetic-local-secret-not-a-real-credential");
        if (!OperatingSystem.IsWindows())
        { Assert.Throws<PlatformNotSupportedException>(() => protector.Protect(input, binding)); return; }
        byte[]? plaintext = null;
        try
        {
            var ciphertext = protector.Protect(input, binding);
            Assert.False(ciphertext.AsSpan().SequenceEqual(input));
            plaintext = protector.Unprotect(ciphertext, binding);
            Assert.Equal(input, plaintext);
            var wrong = NativeSecretBinding.Create(binding.InstallationId, binding.ServiceId, binding.ServiceSid, binding.ConfigurationKey, Guid.NewGuid());
            var error = Assert.Throws<InvalidOperationException>(() => protector.Unprotect(ciphertext, wrong));
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("synthetic-local-secret", error.ToString());
            ciphertext[^1] ^= 1;
            Assert.Throws<InvalidOperationException>(() => protector.Unprotect(ciphertext, binding));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            if (plaintext is not null) { CryptographicOperations.ZeroMemory(plaintext); }
        }
    }

    private static NativeSecretBinding Binding() => NativeSecretBinding.Create(Guid.NewGuid(), "gateway", ServiceSid,
        "Gateway:Security:AccessTokenSigningKey", Guid.NewGuid());

    [Theory]
    [InlineData("installation")]
    [InlineData("service")]
    [InlineData("sid")]
    [InlineData("setting")]
    public void EveryPurposeComponentChangesEntropyAndRejectsDpapiReplay(string changed)
    {
        var original = Binding();
        var altered = NativeSecretBinding.Create(changed == "installation" ? Guid.NewGuid() : original.InstallationId,
            changed == "service" ? "identity" : original.ServiceId,
            changed == "sid" ? "S-1-5-80-9-8-7-6-5" : original.ServiceSid,
            changed == "service" ? "Identity:Authentication:AccessTokenSigningKey" :
                changed == "setting" ? "Gateway:DownstreamAuthentication:SharedSecret" : original.ConfigurationKey, original.SecretId);
        Assert.NotEqual(original.CreateEntropy(), altered.CreateEntropy());
        if (!OperatingSystem.IsWindows()) { return; }
        var protector = new WindowsDpapiSecretProtector();
        var bytes = Encoding.UTF8.GetBytes("synthetic-only-no-real-credential-for-binding");
        try
        {
            var ciphertext = protector.Protect(bytes, original);
            Assert.Throws<InvalidOperationException>(() => protector.Unprotect(ciphertext, altered));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
