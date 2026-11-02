using System.Security.AccessControl;
using System.Security.Principal;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeHttpsKeyPolicyTests
{
    [Theory]
    [InlineData("Microsoft Software Key Storage Provider", 0U, uint.MaxValue)]
    [InlineData("Microsoft Enhanced RSA and AES Cryptographic Provider", 24U, 1U)]
    [InlineData("Microsoft Enhanced RSA and AES Cryptographic Provider", 24U, 2U)]
    public void OnlyKnownLocalSoftwareProvidersAreAccepted(string provider, uint type, uint spec) =>
        WindowsNativeHttpsCertificateSource.ValidateProviderMetadata(provider, "synthetic-key", type, spec, 0x20, 0, false);

    [Theory]
    [InlineData("Synthetic Remote Provider", 0U, uint.MaxValue, 0x20U, 0U, false)]
    [InlineData("Microsoft Platform Crypto Provider", 0U, uint.MaxValue, 0x20U, 0U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 24U, uint.MaxValue, 0x20U, 0U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 0U, 1U, 0x20U, 0U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 0U, uint.MaxValue, 0U, 0U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 0U, uint.MaxValue, 0x28U, 0U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 0U, uint.MaxValue, 0x20U, 1U, false)]
    [InlineData("Microsoft Software Key Storage Provider", 0U, uint.MaxValue, 0x20U, 0U, true)]
    public void UnsupportedMetadataIsRejectedBeforeAnyProviderCanBeOpened(string provider, uint type,
        uint spec, uint flags, uint count, bool parameters) =>
        Assert.Throws<InvalidOperationException>(() => WindowsNativeHttpsCertificateSource.ValidateProviderMetadata(
            provider, "synthetic-key", type, spec, flags, count, parameters));

    [Theory]
    [InlineData("valid")]
    [InlineData("owner")]
    [InlineData("inheritance")]
    [InlineData("other-reader")]
    [InlineData("service-write")]
    [InlineData("missing-read")]
    public void ProtectedDescriptorRequiresSystemOwnershipAndOnlyIntendedServiceRead(string scenario)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var service = new SecurityIdentifier("S-1-5-80-1-2-3-4-5");
        var security = new FileSecurity();
        security.SetOwner(new SecurityIdentifier(scenario == "owner" ? "S-1-5-32-545" : "S-1-5-18"));
        security.SetAccessRuleProtection(scenario != "inheritance", false);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-18"), FileSystemRights.FullControl, AccessControlType.Allow));
        if (scenario != "missing-read")
        {
            security.AddAccessRule(new FileSystemAccessRule(service,
                scenario == "service-write" ? FileSystemRights.Read | FileSystemRights.Write : FileSystemRights.Read,
                AccessControlType.Allow));
        }
        if (scenario == "other-reader")
        {
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-32-545"), FileSystemRights.Read, AccessControlType.Allow));
        }
        if (scenario == "valid") { NativeHttpsPrivateKeyPolicy.ValidateDescriptor(security, service); }
        else
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (OperatingSystem.IsWindows()) { NativeHttpsPrivateKeyPolicy.ValidateDescriptor(security, service); }
            });
        }
    }

    [Fact]
    public void CurrentUserOwnedTemporaryFileIsNotAnApprovedServiceKey()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var path = Path.Combine(Path.GetTempPath(), "fin207-key-policy-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "synthetic-not-a-key");
            Assert.Throws<InvalidOperationException>(() =>
            {
                if (OperatingSystem.IsWindows())
                { NativeHttpsPrivateKeyPolicy.Validate(path, new SecurityIdentifier("S-1-5-80-1-2-3-4-5")); }
            });
        }
        finally { File.Delete(path); }
    }
}
