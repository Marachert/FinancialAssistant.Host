using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FinancialAssistant.Shared.Hosting;

public sealed class WindowsNativeConfigurationArtifacts : INativeConfigurationArtifacts
{
    private readonly List<SafeFileHandle> directories = new();
    private readonly string root;
    private readonly string serviceId;
    private readonly string intendedSid;
    private readonly Guid installationId;
    private NativeServiceConfigurationPlan? validatedPlan;
    private bool disposed;

    private WindowsNativeConfigurationArtifacts(string serviceId, string root, string intendedSid)
    { this.serviceId = serviceId; this.root = root; this.intendedSid = intendedSid; }

    [SupportedOSPlatform("windows")]
    private WindowsNativeConfigurationArtifacts(string serviceId) : this(serviceId, Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FinancialAssistant", "Configuration", serviceId),
        ((SecurityIdentifier)new NTAccount(NativeServiceCatalog.Get(serviceId).ServiceAccount).Translate(typeof(SecurityIdentifier))).Value)
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            NativeConfigurationAccessPolicy.ValidateIdentity(intendedSid, identity.User?.Value, intendedSid);
            var drive = new DriveInfo(Path.GetPathRoot(root)!);
            if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS") { throw new IOException(); }
            var chain = new Stack<string>();
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            { chain.Push(directory.FullName); }
            while (chain.Count > 0) { directories.Add(NativeConfigurationFileHandles.Open(chain.Pop(), directory: true)); }
            NativeConfigurationFileHandles.ValidateAcl(directories[^1], intendedSid, directory: true);
            if (!NativeConfigurationFileHandles.FinalPath(directories[^1]).Equals(root, StringComparison.OrdinalIgnoreCase))
            { throw new IOException(); }
            var marker = ReadFile("installation.id", 32);
            if (marker.Length != 32 || !Guid.TryParseExact(Encoding.ASCII.GetString(marker), "N", out installationId) || installationId == Guid.Empty)
            { throw new IOException(); }
        }
        catch { Dispose(); throw; }
    }

    public static WindowsNativeConfigurationArtifacts Open(string serviceId)
    {
        _ = NativeServiceCatalog.Get(serviceId);
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("Native protected configuration requires Windows."); }
        try { return new WindowsNativeConfigurationArtifacts(serviceId); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or System.Security.SecurityException or IdentityNotMappedException)
        { throw new InvalidOperationException("Native protected configuration or its intended service identity is unavailable."); }
    }

    public byte[] ReadDocument()
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        return ReadFile("current.json", NativeServiceConfigurationPlan.MaximumDocumentBytes);
    }

    public void ValidateOwner(NativeServiceConfigurationPlan plan)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        using var identity = WindowsIdentity.GetCurrent();
        NativeConfigurationAccessPolicy.ValidateIdentity(intendedSid, identity.User?.Value, plan.ServiceSid);
        if (plan.ServiceId != serviceId || plan.InstallationId != installationId) { throw new InvalidOperationException(); }
        NativeConfigurationFileHandles.ValidateAcl(directories[^1], intendedSid, directory: true);
        validatedPlan = plan;
    }

    public byte[] ReadSecret(Guid secretId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        if (validatedPlan is null || !validatedPlan.Secrets.Any(binding => binding.SecretId == secretId))
        { throw new InvalidOperationException("Native secret reference has not been authorized."); }
        return ReadFile(secretId.ToString("N") + ".secret", WindowsDpapiSecretProtector.MaximumCiphertextBytes);
    }

    [SupportedOSPlatform("windows")]
    private byte[] ReadFile(string name, int maximumBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var handle = NativeConfigurationFileHandles.Open(Path.Combine(root, name), directory: false);
        NativeConfigurationFileHandles.ValidateAcl(handle, intendedSid);
        using var stream = new FileStream(handle, FileAccess.Read);
        if (stream.Length is < 1 || stream.Length > maximumBytes) { throw new IOException("Native configuration size is invalid."); }
        var bytes = new byte[(int)stream.Length];
        try { stream.ReadExactly(bytes); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        foreach (var directory in directories) { directory.Dispose(); }
        directories.Clear(); validatedPlan = null;
    }

    public override string ToString() => "Windows native read-only protected configuration artifacts (secret values omitted).";
}
