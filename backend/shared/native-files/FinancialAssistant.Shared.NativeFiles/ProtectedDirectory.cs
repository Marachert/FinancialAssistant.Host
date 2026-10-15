using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace FinancialAssistant.Shared.NativeFiles;

public sealed partial class ProtectedDirectory : IDisposable
{
    private readonly string root;
    private readonly string serviceSid;
    private readonly HashSet<string> allowedSids;
    private readonly List<SafeFileHandle> directories = new();
    private readonly FileStream lease;
    private readonly int maximumFiles;
    private bool disposed;

    public ProtectedDirectory(string rootPath, string serviceSid, int maximumFiles = 10000)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("Protected native files require Windows."); }
        if (maximumFiles is < 10 or > 10000) { throw new ArgumentOutOfRangeException(nameof(maximumFiles)); }
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath) || rootPath.StartsWith(@"\\", StringComparison.Ordinal)
            || rootPath.IndexOf(':', 2) >= 0 || rootPath.Any(char.IsControl)
            || rootPath.Split(['\\', '/']).Any(part => part.EndsWith(' ') || part.EndsWith('.')))
        { throw new ArgumentException("A local absolute protected data directory is required."); }
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        if (root.Equals(install, StringComparison.OrdinalIgnoreCase) || root.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || install.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase))
        { throw new ArgumentException("Native data must be separate from application files and volume roots."); }
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is null || identity.User.Value != serviceSid) { throw new InvalidOperationException("Native file identity does not match its configured service owner."); }
        allowedSids = new HashSet<string>(StringComparer.Ordinal) { serviceSid, "S-1-5-18", "S-1-5-32-544" };
        this.serviceSid = serviceSid;
        this.maximumFiles = maximumFiles;
        FileStream? acquired = null;
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS")
            { throw new InvalidOperationException("Native protected storage requires a local NTFS volume."); }
            var chain = new Stack<string>();
            for (var path = new DirectoryInfo(root); path is not null; path = path.Parent) { chain.Push(path.FullName); }
            while (chain.Count > 0)
            {
                var handle = Open(chain.Pop(), directory: true);
                directories.Add(handle);
                Inspect(handle, directory: true);
            }
            using (var installHandle = Open(install, directory: true))
            {
                var actualRoot = FinalPath(directories[^1]);
                var actualInstall = FinalPath(installHandle);
                if (actualRoot.Equals(actualInstall, StringComparison.OrdinalIgnoreCase)
                    || actualRoot.StartsWith(actualInstall + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || actualInstall.StartsWith(actualRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                { throw new IOException("Native data and application files must be separate."); }
            }
            ValidateAcl(new DirectoryInfo(root).GetAccessControl(), requireProtected: true);
            _ = ListFiles();
            var leaseHandle = CreateFileW(Path.Combine(root, ".lease"), 0xC0000000, 0, IntPtr.Zero, 4, 0x00200000, IntPtr.Zero);
            if (leaseHandle.IsInvalid) { leaseHandle.Dispose(); throw new IOException("Native storage writer is unavailable."); }
            acquired = new FileStream(leaseHandle, FileAccess.ReadWrite);
            Inspect(acquired.SafeFileHandle, directory: false);
            ValidateAcl(acquired.GetAccessControl(), requireProtected: false);
            lease = acquired;
            _ = ListFiles();
        }
        catch
        {
            acquired?.Dispose();
            foreach (var handle in directories) { handle.Dispose(); }
            throw new InvalidOperationException("Native protected file storage is unavailable or its security policy is invalid.");
        }
    }

    public IReadOnlyList<string> ListFiles()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var names = new List<string>();
        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            var name = Path.GetFileName(path);
            if (name == ".lease") { continue; }
            ValidateName(name);
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            { throw new IOException("Unexpected native storage entry."); }
            names.Add(name);
            if (names.Count > maximumFiles) { throw new IOException("Native storage capacity exceeded."); }
        }
        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    public async Task<byte[]?> ReadAsync(string name, int maximumBytes, CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateName(name);
        if (maximumBytes is < 1 or > 12 * 1024 * 1024) { throw new ArgumentOutOfRangeException(nameof(maximumBytes)); }
        SafeFileHandle handle;
        try { handle = Open(Path.Combine(root, name), directory: false); }
        catch (FileNotFoundException) { return null; }
        using (handle)
        using (var stream = new FileStream(handle, FileAccess.Read))
        {
            Inspect(handle, directory: false);
            ValidateAcl(stream.GetAccessControl(), requireProtected: false);
            if (stream.Length is < 1 || stream.Length > maximumBytes) { throw new IOException("Invalid native storage file size."); }
            var bytes = new byte[checked((int)stream.Length)];
            try { await stream.ReadExactlyAsync(bytes, token); return bytes; }
            catch { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); throw; }
        }
    }

    public async Task WriteAsync(string name, ReadOnlyMemory<byte> bytes, bool replace, CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateName(name);
        if (bytes.Length is < 1 or > 12 * 1024 * 1024) { throw new ArgumentException("Invalid native storage payload size."); }
        var entries = ListFiles();
        if (!entries.Contains(name) && entries.Count >= maximumFiles - 1) { throw new IOException("Native storage capacity exceeded."); }
        if (entries.Contains(name))
        {
            using var existing = Open(Path.Combine(root, name), directory: false);
            Inspect(existing, directory: false);
            using var existingStream = new FileStream(existing, FileAccess.Read);
            ValidateAcl(existingStream.GetAccessControl(), requireProtected: false);
        }
        var temporary = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                Inspect(stream.SafeFileHandle, directory: false);
                ValidateAcl(stream.GetAccessControl(), requireProtected: false);
                await stream.WriteAsync(bytes, token);
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, Path.Combine(root, name), overwrite: replace);
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }

    public void DeleteTemporary(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateName(name);
        if (!name.EndsWith(".tmp", StringComparison.Ordinal)) { throw new ArgumentException("Only abandoned temporary files may be removed."); }
        using (var handle = Open(Path.Combine(root, name), directory: false)) { Inspect(handle, directory: false); }
        File.Delete(Path.Combine(root, name));
    }

    public void DeleteFile(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ValidateName(name);
        using (var handle = Open(Path.Combine(root, name), directory: false)) { Inspect(handle, directory: false); }
        File.Delete(Path.Combine(root, name));
    }

    private void ValidateAcl(FileSystemSecurity security, bool requireProtected)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        if (requireProtected && !security.AreAccessRulesProtected || security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner
            || !allowedSids.Contains(owner.Value)) { throw new IOException("Invalid native storage ownership."); }
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        var serviceAccess = false;
        if (rules.Length == 0) { throw new IOException("Invalid native storage ACL."); }
        foreach (var rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow || !allowedSids.Contains(rule.IdentityReference.Value)
                || rule.PropagationFlags != PropagationFlags.None) { throw new IOException("Invalid native storage ACL."); }
            if (rule.IdentityReference.Value == serviceSid && (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify
                && (rule.InheritanceFlags & (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)) == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit))
            { serviceAccess = true; }
        }
        if (requireProtected && !serviceAccess)
        { throw new IOException("Native storage service access is missing."); }
    }

    private static void ValidateName(string name)
    {
        if (name is null || !FileNamePattern().IsMatch(name)) { throw new ArgumentException("Invalid opaque native storage name."); }
    }

    private static SafeFileHandle Open(string path, bool directory)
    {
        var handle = CreateFileW(path, directory ? 0x00020080u : 0x80000000u, directory ? 3u : 1u, IntPtr.Zero, 3,
            0x00200000u | (directory ? 0x02000000u : 0u), IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (!directory && error == 2) { throw new FileNotFoundException("Native object is unavailable."); }
            throw new IOException("Native storage handle could not be opened.");
        }
        return handle;
    }

    private static void Inspect(SafeFileHandle handle, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & 0x400) != 0
            || ((info.Attributes & 0x10) != 0) != directory || !directory && info.NumberOfLinks != 1)
        { throw new IOException("Native storage links and unexpected entry types are prohibited."); }
    }

    private static string FinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity || !buffer.ToString().StartsWith(@"\\?\", StringComparison.Ordinal))
        { throw new IOException("Native storage canonical path is unavailable."); }
        return Path.TrimEndingDirectorySeparator(buffer.ToString()[4..]);
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        lease.Dispose();
        foreach (var handle in directories) { handle.Dispose(); }
    }

    [GeneratedRegex(@"^(?:[A-F0-9]{64}\.object|[a-f0-9]{32}\.tmp)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint sharing, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }
}
