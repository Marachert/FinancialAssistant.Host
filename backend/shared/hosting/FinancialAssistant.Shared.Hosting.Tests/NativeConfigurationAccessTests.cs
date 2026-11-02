using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeConfigurationAccessTests
{
    private const string Sid = "S-1-5-80-1-2-3-4-5";

    [Theory]
    [InlineData("valid")]
    [InlineData("owner")]
    [InlineData("inheritance")]
    [InlineData("other-reader")]
    [InlineData("service-write")]
    [InlineData("service-delete")]
    [InlineData("service-owner")]
    [InlineData("missing-read")]
    [InlineData("missing-system")]
    [InlineData("missing-admin")]
    [InlineData("inherit-only")]
    [InlineData("deny")]
    public void DescriptorRequiresSystemAdministratorOwnershipAndOnlyIntendedServiceRead(string scenario)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        FileSystemSecurity security = scenario == "inherit-only" ? new DirectorySecurity() : new FileSecurity();
        security.SetOwner(new SecurityIdentifier(scenario == "owner" ? "S-1-5-32-545" : scenario == "service-owner" ? Sid : "S-1-5-18"));
        security.SetAccessRuleProtection(scenario != "inheritance", false);
        foreach (var principal in new[] { "S-1-5-18", "S-1-5-32-544" })
        {
            if (scenario == "missing-system" && principal == "S-1-5-18" || scenario == "missing-admin" && principal == "S-1-5-32-544") { continue; }
            AddRule(security, new FileSystemAccessRule(new SecurityIdentifier(principal), FileSystemRights.FullControl, AccessControlType.Allow));
        }
        if (scenario != "missing-read")
        {
            var rights = FileSystemRights.Read | (scenario == "service-write" ? FileSystemRights.Write : scenario == "service-delete" ? FileSystemRights.Delete : 0);
            AddRule(security, new FileSystemAccessRule(new SecurityIdentifier(Sid), rights,
                scenario == "inherit-only" ? InheritanceFlags.ObjectInherit : InheritanceFlags.None,
                scenario == "inherit-only" ? PropagationFlags.InheritOnly : PropagationFlags.None, AccessControlType.Allow));
        }
        if (scenario is "other-reader" or "deny")
        { AddRule(security, new FileSystemAccessRule(new SecurityIdentifier("S-1-5-32-545"), FileSystemRights.Read, scenario == "deny" ? AccessControlType.Deny : AccessControlType.Allow)); }
        if (scenario == "valid") { NativeConfigurationAccessPolicy.Validate(security, Sid); }
        else { Assert.Throws<InvalidOperationException>(() => { if (OperatingSystem.IsWindows()) { NativeConfigurationAccessPolicy.Validate(security, Sid); } }); }
    }

    [Theory]
    [InlineData(Sid, Sid, Sid, true)]
    [InlineData(Sid, "S-1-5-18", Sid, false)]
    [InlineData(Sid, Sid, "S-1-5-80-9-8-7-6-5", false)]
    [InlineData("S-1-5-18", "S-1-5-18", "S-1-5-18", false)]
    public void ActualIdentityMustMatchBothCanonicalServiceAndDocument(string intended, string actual, string configured, bool valid)
    {
        if (valid) { NativeConfigurationAccessPolicy.ValidateIdentity(intended, actual, configured); }
        else { Assert.Throws<InvalidOperationException>(() => NativeConfigurationAccessPolicy.ValidateIdentity(intended, actual, configured)); }
    }

    [Fact]
    public void InteractiveIdentityCannotOpenInstalledStoreEvenWithMachineDpapiAccess()
    {
        if (!OperatingSystem.IsWindows())
        { Assert.Throws<PlatformNotSupportedException>(() => WindowsNativeConfigurationArtifacts.Open("monitoring")); return; }
        var error = Assert.Throws<InvalidOperationException>(() => WindowsNativeConfigurationArtifacts.Open("monitoring"));
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void OwnedFixtureFileHandlePinsEntryRejectsHardlinksAndReadsAclFromSameHandle()
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var root = Path.Combine(Path.GetTempPath(), "fin208-handles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "synthetic-fixture"); var alias = Path.Combine(root, "synthetic-alias");
        try
        {
            File.WriteAllText(file, "synthetic-not-configuration");
            using (var handle = NativeConfigurationFileHandles.Open(file, directory: false))
            {
                Assert.Equal(file, NativeConfigurationFileHandles.FinalPath(handle), ignoreCase: true);
                Assert.Throws<IOException>(() => File.WriteAllText(file, "must-not-overwrite"));
                Assert.Throws<IOException>(() => File.Move(file, alias));
                Assert.Throws<InvalidOperationException>(() => { if (OperatingSystem.IsWindows()) { NativeConfigurationFileHandles.ValidateAcl(handle, Sid); } });
            }
            using (var handle = NativeConfigurationFileHandles.Open(root, directory: true))
            { Assert.Throws<IOException>(() => Directory.Move(root, root + "-moved")); }
            Assert.True(CreateHardLinkW(alias, file, IntPtr.Zero));
            Assert.Throws<IOException>(() => { if (OperatingSystem.IsWindows()) { NativeConfigurationFileHandles.Open(file, directory: false).Dispose(); } });
        }
        finally
        {
            foreach (var directory in new[] { root, root + "-moved" })
            {
                if (File.Exists(Path.Combine(directory, "synthetic-alias"))) { File.Delete(Path.Combine(directory, "synthetic-alias")); }
                if (File.Exists(Path.Combine(directory, "synthetic-fixture"))) { File.Delete(Path.Combine(directory, "synthetic-fixture")); }
                if (Directory.Exists(directory)) { Directory.Delete(directory); }
            }
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void AddRule(FileSystemSecurity descriptor, FileSystemAccessRule rule)
    {
        if (descriptor is DirectorySecurity directory) { directory.AddAccessRule(rule); }
        else { ((FileSecurity)descriptor).AddAccessRule(rule); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string name, string existing, IntPtr attributes);
}
