using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using FinancialAssistant.Shared.NativeFiles;
using Xunit;

namespace FinancialAssistant.Shared.Recovery.Tests;

public sealed class WindowsNativeRecoveryStoreTests
{
    [Fact]
    public async Task ActualProtectedStorePersistsLargeEncryptedPackageAndRetriesWithoutOverwrite()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture();
        using var package = fixture.Package(large: true);
        using var store = new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid);
        var saved = await store.SaveAsync(package, fixture.Passphrase);
        Assert.False(saved.AlreadyPresent); Assert.True(saved.Bytes > 12 * 1024 * 1024);
        var retry = await store.SaveAsync(package, fixture.Passphrase);
        Assert.True(retry.AlreadyPresent); Assert.Equal(saved.Sha256, retry.Sha256);
        using var restored = await store.OpenAsync(fixture.Metadata.SourceInstallationId, fixture.Metadata.BackupId, fixture.Passphrase);
        var income = restored.Manifest.Artifacts.Single(artifact => artifact.Owner == "income" && artifact.Kind == NativeRecoveryArtifactKind.Database);
        var plaintext = restored.OpenArtifact(income.Id, fixture.Passphrase);
        try { Assert.Equal(fixture.Large.Length, plaintext.Length); Assert.Equal(SHA256.HashData(fixture.Large), SHA256.HashData(plaintext)); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        Assert.DoesNotContain(fixture.Directory, store.ToString());
        Assert.All(System.IO.Directory.EnumerateFiles(fixture.Directory), file => Assert.False(file.EndsWith(".tmp", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ConflictingSameBackupCannotOverwriteVerifiedExistingPackage()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture(); using var first = fixture.Package(); using var conflict = fixture.Package();
        using var store = new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid);
        var saved = await store.SaveAsync(first, fixture.Passphrase);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(conflict, fixture.Passphrase));
        Assert.Equal(saved.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture.Directory, saved.FileName)))));
        using var restored = await store.OpenAsync(fixture.Metadata.SourceInstallationId, fixture.Metadata.BackupId, fixture.Passphrase);
        Assert.Equal(first.Manifest.ManifestId, restored.Manifest.ManifestId);
    }

    [Fact]
    public async Task PersistedCorruptionAndWrongUnlockCannotReturnOrReplaceAPackage()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture(); using var package = fixture.Package();
        using var store = new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid);
        var saved = await store.SaveAsync(package, fixture.Passphrase);
        var wrong = fixture.Passphrase.ToArray(); wrong[0] ^= 1;
        try { await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenAsync(fixture.Metadata.SourceInstallationId, fixture.Metadata.BackupId, wrong)); }
        finally { CryptographicOperations.ZeroMemory(wrong); }
        var path = Path.Combine(fixture.Directory, saved.FileName); var corrupted = File.ReadAllBytes(path); corrupted[^1] ^= 1; File.WriteAllBytes(path, corrupted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenAsync(fixture.Metadata.SourceInstallationId, fixture.Metadata.BackupId, fixture.Passphrase));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(package, fixture.Passphrase));
        Assert.Equal(corrupted, File.ReadAllBytes(path));
    }

    [Fact]
    public void AnotherWriterAndBroadDirectoryAclAreRejectedWithoutRepair()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture();
        using (var store = new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid))
        { Assert.Throws<InvalidOperationException>(() => new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid)); }
        var directory = new DirectoryInfo(fixture.Directory); var acl = directory.GetAccessControl();
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-1-0"), FileSystemRights.Read, AccessControlType.Allow));
        directory.SetAccessControl(acl);
        var before = directory.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);
        Assert.Throws<InvalidOperationException>(() => new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid));
        Assert.Equal(before, directory.GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All));
    }

    [Fact]
    public async Task DefaultNativeFileLimitRemainsTwelveMiBAndExplicitLimitsAreBounded()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProtectedDirectory(fixture.Directory, fixture.Sid, maximumFiles: 10, maximumFileBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProtectedDirectory(fixture.Directory, fixture.Sid, maximumFiles: 10, maximumFileBytes: NativeRecoveryPackage.MaximumPackageBytes + 1));
        using var files = new ProtectedDirectory(fixture.Directory, fixture.Sid);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => files.ReadAsync(new string('A', 64) + ".object", 12 * 1024 * 1024 + 1));
    }

    [Fact]
    public async Task CancellationMissingIdentityAndDisposedStoreNeverWriteAnArchive()
    {
        if (!OperatingSystem.IsWindows()) { PlatformGuard(); return; }
        using var fixture = new Fixture(); using var package = fixture.Package();
        var store = new WindowsNativeRecoveryStore(fixture.Directory, fixture.Sid);
        try
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(package, fixture.Passphrase, cancelled.Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenAsync(Guid.Empty, fixture.Metadata.BackupId, fixture.Passphrase));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenAsync(fixture.Metadata.SourceInstallationId, Guid.NewGuid(), fixture.Passphrase));
            Assert.Empty(System.IO.Directory.EnumerateFiles(fixture.Directory, "*.object"));
            store.Dispose(); store.Dispose();
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(package, fixture.Passphrase));
        }
        finally { store.Dispose(); }
    }

    private static void PlatformGuard() => Assert.Throws<PlatformNotSupportedException>(() => new WindowsNativeRecoveryStore("unused", "unused"));

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; }
        public string Sid { get; }
        public byte[] Passphrase { get; } = Encoding.UTF8.GetBytes("synthetic-only-independent-recovery-unlock-material");
        public byte[] Large { get; } = new byte[13 * 1024 * 1024];
        public NativeRecoveryManifestDocument Metadata { get; } = new()
        {
            SchemaVersion = 1,
            SourceInstallationId = Guid.NewGuid(),
            BackupId = Guid.NewGuid(),
            ManifestId = Guid.NewGuid(),
            ConsistencyBoundaryId = Guid.NewGuid(),
            CapturedAtUtc = DateTimeOffset.Parse("2026-11-03T09:00:00Z"),
            ApplicationVersion = "1.0.0",
            InstallerManifestSha256 = new string('A', 64),
            PostgresMajorVersion = 18,
            BrokerPolicy = NativeRecoveryBrokerPolicy.DrainedThenRecreate,
            SearchPolicy = NativeRecoverySearchPolicy.RebuildFromOwnedState
        };

        public Fixture()
        {
            if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "FinancialAssistant.Backend.sln"))) { root = root.Parent ?? throw new DirectoryNotFoundException(); }
            Directory = Path.Combine(root.FullName, ".codex-runtime", "recovery-store", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            using var identity = WindowsIdentity.GetCurrent(); var owner = identity.User ?? throw new InvalidOperationException(); Sid = owner.Value;
            var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(owner);
            foreach (var sid in new[] { Sid, "S-1-5-18", "S-1-5-32-544" }.Distinct(StringComparer.Ordinal))
            { acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow)); }
            new DirectoryInfo(Directory).SetAccessControl(acl); Large.AsSpan().Fill(0x5A);
        }

        public NativeRecoveryPackage Package(bool large = false) => NativeRecoveryPackage.Create(Metadata,
            NativeRecoveryManifest.RequiredResources.Select(resource => new NativeRecoveryCapture
            {
                Owner = resource.Owner,
                Kind = resource.Kind,
                Plaintext = large && resource.Owner == "income" && resource.Kind == NativeRecoveryArtifactKind.Database ? Large : "synthetic-owned-store-payload"u8.ToArray(),
                MigrationChecksum = resource.Kind == NativeRecoveryArtifactKind.Database ? new string('B', 64) : null
            }).ToArray(), Passphrase);

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(Passphrase); CryptographicOperations.ZeroMemory(Large);
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory))
            {
                var name = Path.GetFileName(file);
                if ((File.GetAttributes(file) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                    name != ".lease" && !(name.Length == 71 && name.EndsWith(".object", StringComparison.Ordinal) && name[..64].All(char.IsAsciiHexDigit)))
                { throw new InvalidOperationException("Unexpected entry in owned synthetic recovery fixture."); }
                File.Delete(file);
            }
            System.IO.Directory.Delete(Directory, recursive: false);
        }
    }
}
