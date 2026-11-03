using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.NativeFiles;

namespace FinancialAssistant.Shared.Recovery;

public sealed record NativeRecoveryStoredPackage(Guid SourceInstallationId, Guid BackupId, string FileName, string Sha256, int Bytes, bool AlreadyPresent);

public sealed class WindowsNativeRecoveryStore : IDisposable, IAsyncDisposable
{
    private readonly ProtectedDirectory files;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;

    public WindowsNativeRecoveryStore(string existingProtectedDirectory, string operatorSid) =>
        files = new ProtectedDirectory(existingProtectedDirectory, operatorSid, maximumFiles: 64, maximumFileBytes: NativeRecoveryPackage.MaximumPackageBytes);

    public async Task<NativeRecoveryStoredPackage> SaveAsync(NativeRecoveryPackage package, ReadOnlyMemory<byte> operatorPassphrase, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            using var stream = new MemoryStream();
            try
            {
                package.WriteTo(stream, token); stream.Position = 0;
                using (var verified = NativeRecoveryPackage.ReadFrom(stream, operatorPassphrase.Span, token))
                {
                    if (verified.Manifest.SourceInstallationId != package.Manifest.SourceInstallationId || verified.Manifest.BackupId != package.Manifest.BackupId)
                    { throw Invalid(); }
                }
                if (!stream.TryGetBuffer(out var buffer)) { throw Invalid(); }
                var bytes = buffer.AsMemory(0, checked((int)stream.Length)); var digest = SHA256.HashData(bytes.Span);
                var name = Name(package.Manifest.SourceInstallationId, package.Manifest.BackupId);
                var existing = await files.ReadAsync(name, NativeRecoveryPackage.MaximumPackageBytes, token);
                var alreadyPresent = existing is not null;
                if (existing is not null)
                {
                    try { if (existing.Length != bytes.Length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing), digest)) { throw Invalid(); } }
                    finally { CryptographicOperations.ZeroMemory(existing); }
                }
                else { await files.WriteAsync(name, bytes, replace: false, token); }
                var persisted = await files.ReadAsync(name, NativeRecoveryPackage.MaximumPackageBytes, token) ?? throw Invalid();
                try
                {
                    if (persisted.Length != bytes.Length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(persisted), digest)) { throw Invalid(); }
                }
                finally { CryptographicOperations.ZeroMemory(persisted); }
                return new(package.Manifest.SourceInstallationId, package.Manifest.BackupId, name, Convert.ToHexString(digest), bytes.Length, alreadyPresent);
            }
            finally { if (stream.TryGetBuffer(out var buffer)) { CryptographicOperations.ZeroMemory(buffer.AsSpan()); } }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or CryptographicException or NotSupportedException)
        { throw Invalid(); }
        finally { gate.Release(); }
    }

    public async Task<NativeRecoveryPackage> OpenAsync(Guid sourceInstallationId, Guid backupId, ReadOnlyMemory<byte> operatorPassphrase, CancellationToken token = default)
    {
        var name = Name(sourceInstallationId, backupId);
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var bytes = await files.ReadAsync(name, NativeRecoveryPackage.MaximumPackageBytes, token) ?? throw Invalid();
            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var package = NativeRecoveryPackage.ReadFrom(stream, operatorPassphrase.Span, token);
                if (package.Manifest.SourceInstallationId != sourceInstallationId || package.Manifest.BackupId != backupId)
                { package.Dispose(); throw Invalid(); }
                return package;
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or CryptographicException or NotSupportedException)
        { throw Invalid(); }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        if (!gate.Wait(0)) { throw new InvalidOperationException("Await active recovery operations or use asynchronous disposal."); }
        try { if (!disposed) { disposed = true; files.Dispose(); } }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { if (!disposed) { disposed = true; files.Dispose(); } }
        finally { gate.Release(); }
    }

    public override string ToString() => "Protected native recovery store (paths and unlock material omitted).";

    private static string Name(Guid installation, Guid backup)
    {
        if (installation == Guid.Empty || backup == Guid.Empty) { throw Invalid(); }
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("FinancialAssistant.Recovery.Package.v1:" + installation.ToString("N") + ":" + backup.ToString("N")))) + ".object";
    }

    private static InvalidOperationException Invalid() => new("Protected native recovery package is unavailable or invalid.");
}
