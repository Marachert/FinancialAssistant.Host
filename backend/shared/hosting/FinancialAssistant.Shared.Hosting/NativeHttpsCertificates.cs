using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

[assembly: InternalsVisibleTo("FinancialAssistant.Shared.Hosting.Tests")]

namespace FinancialAssistant.Shared.Hosting;

public interface INativeHttpsCertificateSource
{
    X509Certificate2 Load(NativeHttpsPlan plan);
}

public static class NativeHttpsCertificatePolicy
{
    private static readonly string[] Signatures =
    ["1.2.840.113549.1.1.11", "1.2.840.113549.1.1.12", "1.2.840.113549.1.1.13",
        "1.2.840.10045.4.3.2", "1.2.840.10045.4.3.3", "1.2.840.10045.4.3.4"];

    public static void Validate(X509Certificate2 certificate, NativeHttpsPlan plan, DateTimeOffset now, bool requireRenewalWindow)
    {
        var minimum = requireRenewalWindow ? now.AddDays(plan.MinimumCertificateDays) : now;
        var basic = certificate.Extensions.OfType<X509BasicConstraintsExtension>().ToArray();
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().ToArray();
        var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().ToArray();
        using var rsa = certificate.GetRSAPublicKey();
        using var ec = certificate.GetECDsaPublicKey();
        if (!certificate.HasPrivateKey || certificate.GetCertHashString(HashAlgorithmName.SHA256) != plan.CertificateSha256 ||
            certificate.NotBefore.ToUniversalTime() > now.UtcDateTime || certificate.NotAfter.ToUniversalTime() <= minimum.UtcDateTime ||
            certificate.SubjectName.RawData.AsSpan().SequenceEqual(certificate.IssuerName.RawData) ||
            !certificate.MatchesHostname(plan.HostName, allowWildcards: false, allowCommonName: false) ||
            !Signatures.Contains(certificate.SignatureAlgorithm.Value, StringComparer.Ordinal) ||
            basic.Length != 1 || basic[0].CertificateAuthority || usage.Length != 1 ||
            !usage[0].KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature) ||
            (usage[0].KeyUsages & (X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign)) != 0 ||
            eku.Length != 1 || !eku[0].EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1") ||
            !(rsa?.KeySize >= 2048 || ec?.KeySize >= 256))
        { throw new InvalidOperationException("Native HTTPS certificate does not meet the approved identity, lifetime or cryptographic policy."); }
    }
}

public sealed class WindowsNativeHttpsCertificateSource : INativeHttpsCertificateSource
{
    public X509Certificate2 Load(NativeHttpsPlan plan)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("Native HTTPS certificate provisioning requires Windows."); }
        try { return LoadWindows(plan); }
        catch (Exception exception) when (exception is CryptographicException or UnauthorizedAccessException or
            IOException or System.Security.SecurityException or IdentityNotMappedException or ArgumentException or InvalidOperationException)
        { throw new InvalidOperationException("Native HTTPS machine certificate, private-key policy or intended service identity is unavailable."); }
    }

    [SupportedOSPlatform("windows")]
    private static X509Certificate2 LoadWindows(NativeHttpsPlan plan)
    {
        var intended = (SecurityIdentifier)new NTAccount(NativeServiceCatalog.Get("gateway").ServiceAccount).Translate(typeof(SecurityIdentifier));
        using var identity = WindowsIdentity.GetCurrent();
        if (intended.Value != plan.ServiceSid || identity.User?.Value != intended.Value) { throw new InvalidOperationException(); }
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var certificates = store.Certificates.Cast<X509Certificate2>().ToArray();
        X509Certificate2? result = null;
        try
        {
            var matches = certificates.Where(certificate => certificate.GetCertHashString(HashAlgorithmName.SHA256) == plan.CertificateSha256).ToArray();
            if (matches.Length != 1) { throw new InvalidOperationException(); }
            result = new X509Certificate2(matches[0]);
            RequireLocalProvider(result);
            NativeHttpsCertificatePolicy.Validate(result, plan, DateTimeOffset.UtcNow, requireRenewalWindow: true);
            using var chain = new X509Chain(useMachineContext: true);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(2);
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            if (!chain.Build(result)) { throw new InvalidOperationException(); }
            NativeHttpsPrivateKeyPolicy.Validate(KeyPath(result), intended);
            return result;
        }
        catch { result?.Dispose(); throw; }
        finally { foreach (var certificate in certificates) { certificate.Dispose(); } }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyProviderInfo
    {
        public IntPtr ContainerName;
        public IntPtr ProviderName;
        public uint ProviderType;
        public uint Flags;
        public uint ParameterCount;
        public IntPtr Parameters;
        public uint KeySpec;
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertGetCertificateContextProperty(IntPtr context, uint property, IntPtr data, ref uint size);

    [SupportedOSPlatform("windows")]
    private static void RequireLocalProvider(X509Certificate2 certificate)
    {
        // Inspect metadata first: acquiring an unknown KSP key could invoke a remote provider.
        uint size = 0;
        if (!CertGetCertificateContextProperty(certificate.Handle, 2, IntPtr.Zero, ref size) ||
            size < Marshal.SizeOf<KeyProviderInfo>() || size > 65536) { throw new InvalidOperationException(); }
        var allocatedSize = size;
        var data = Marshal.AllocHGlobal((int)size);
        try
        {
            if (!CertGetCertificateContextProperty(certificate.Handle, 2, data, ref size) ||
                size < Marshal.SizeOf<KeyProviderInfo>() || size > allocatedSize) { throw new InvalidOperationException(); }
            var info = Marshal.PtrToStructure<KeyProviderInfo>(data);
            var provider = ReadMetadataName(info.ProviderName, data, size);
            var container = ReadMetadataName(info.ContainerName, data, size);
            ValidateProviderMetadata(provider, container, info.ProviderType, info.KeySpec, info.Flags,
                info.ParameterCount, info.Parameters != IntPtr.Zero);
        }
        finally { Marshal.FreeHGlobal(data); GC.KeepAlive(certificate); }
    }

    internal static void ValidateProviderMetadata(string provider, string container, uint providerType,
        uint keySpec, uint flags, uint parameterCount, bool hasParameters)
    {
        var cng = providerType == 0 && keySpec == uint.MaxValue && provider == "Microsoft Software Key Storage Provider";
        var csp = providerType == 24 && keySpec is 1 or 2 && provider == "Microsoft Enhanced RSA and AES Cryptographic Provider";
        if (container.Length == 0 || !(cng || csp) || (flags & 0x20) == 0 || (flags & ~0x60U) != 0 ||
            parameterCount != 0 || hasParameters) { throw new InvalidOperationException("Native HTTPS requires an approved local machine key provider."); }
    }

    private static string ReadMetadataName(IntPtr value, IntPtr buffer, uint size)
    {
        var offset = value.ToInt64() - buffer.ToInt64();
        if (offset < 0 || offset >= size || offset % 2 != 0) { throw new InvalidOperationException(); }
        var limit = Math.Min(256, (int)((size - offset) / 2));
        var chars = new List<char>();
        for (var index = 0; index < limit; index++)
        {
            var character = (char)Marshal.ReadInt16(value, index * 2);
            if (character == '\0') { return new string(chars.ToArray()); }
            if (character is < ' ' or > '~') { throw new InvalidOperationException(); }
            chars.Add(character);
        }
        throw new InvalidOperationException();
    }

    [SupportedOSPlatform("windows")]
    private static string KeyPath(X509Certificate2 certificate)
    {
        string name; string directory;
        using var rsa = certificate.GetRSAPrivateKey();
        using var ec = certificate.GetECDsaPrivateKey();
        var cng = rsa is RSACng rsaCng ? rsaCng.Key : ec is ECDsaCng ecCng ? ecCng.Key : null;
        if (cng is not null)
        {
            if (!cng.IsMachineKey || !CngProvider.MicrosoftSoftwareKeyStorageProvider.Equals(cng.Provider) || cng.ExportPolicy != CngExportPolicies.None ||
                cng.UIPolicy.ProtectionLevel != CngUIProtectionLevels.None)
            { throw new InvalidOperationException(); }
            name = cng.UniqueName ?? throw new InvalidOperationException(); directory = "Keys";
        }
        else if (rsa is RSACryptoServiceProvider csp)
        {
            var info = csp.CspKeyContainerInfo;
            if (!info.MachineKeyStore || info.HardwareDevice || info.Removable || info.Exportable || info.Protected) { throw new InvalidOperationException(); }
            name = info.UniqueKeyContainerName; directory = "RSA/MachineKeys";
        }
        else { throw new InvalidOperationException(); }
        if (string.IsNullOrEmpty(name) || name.Length > 200 || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        { throw new InvalidOperationException(); }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Crypto", directory, name);
    }
}

public static class NativeHttpsPrivateKeyPolicy
{
    [SupportedOSPlatform("windows")]
    public static void Validate(string path, SecurityIdentifier serviceSid)
    {
        var full = Path.GetFullPath(path);
        for (FileSystemInfo? entry = new FileInfo(full); entry is not null; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) { throw new InvalidOperationException("Linked certificate key storage is forbidden."); }
        }
        using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        var security = stream.GetAccessControl();
        ValidateDescriptor(security, serviceSid);
    }

    [SupportedOSPlatform("windows")]
    internal static void ValidateDescriptor(FileSecurity security, SecurityIdentifier serviceSid)
    {
        if (!security.AreAccessRulesProtected || security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner ||
            owner.Value is not ("S-1-5-18" or "S-1-5-32-544"))
        { throw new InvalidOperationException("Certificate private-key ownership or inheritance is invalid."); }
        var canRead = false;
        const FileSystemRights forbidden = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership | FileSystemRights.DeleteSubdirectoriesAndFiles;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow) { continue; }
            var sid = ((SecurityIdentifier)rule.IdentityReference).Value;
            if (sid is "S-1-5-18" or "S-1-5-32-544") { continue; }
            if (sid != serviceSid.Value || (rule.FileSystemRights & forbidden) != 0) { throw new InvalidOperationException("Certificate key grants exceed the intended service boundary."); }
            canRead |= (rule.FileSystemRights & FileSystemRights.ReadData) != 0;
        }
        if (!canRead) { throw new InvalidOperationException("Certificate key lacks explicit service read access."); }
    }
}
