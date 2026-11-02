using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FinancialAssistant.Shared.Hosting;

[SupportedOSPlatform("windows")]
internal static class NativeConfigurationFileHandles
{
    internal static SafeFileHandle Open(string path, bool directory)
    {
        // No delete sharing pins the selected entry; file reads also prohibit concurrent writers.
        var handle = CreateFileW(path, directory ? 0x00020081u : 0x80020000u, directory ? 3u : 1u,
            IntPtr.Zero, 3, 0x00200000u | (directory ? 0x02000000u : 0u), IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Native configuration entry is unavailable."); }
        try { Inspect(handle, directory); return handle; }
        catch { handle.Dispose(); throw; }
    }

    internal static void Inspect(SafeFileHandle handle, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var info) || (info.Attributes & 0x400) != 0 ||
            ((info.Attributes & 0x10) != 0) != directory || !directory && info.NumberOfLinks != 1)
        { throw new IOException("Native configuration links or unexpected entry types are forbidden."); }
    }

    internal static void ValidateAcl(SafeFileHandle handle, string serviceSid, bool directory = false)
    {
        var status = GetSecurityInfo(handle, 1, 5, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var security);
        try
        {
            if (status != 0 || security == IntPtr.Zero) { throw new IOException("Native configuration security is unavailable."); }
            var length = GetSecurityDescriptorLength(security);
            if (length is < 20 or > 65536) { throw new IOException("Native configuration security is invalid."); }
            var bytes = new byte[(int)length]; Marshal.Copy(security, bytes, 0, bytes.Length);
            FileSystemSecurity descriptor = directory ? new DirectorySecurity() : new FileSecurity();
            descriptor.SetSecurityDescriptorBinaryForm(bytes);
            NativeConfigurationAccessPolicy.Validate(descriptor, serviceSid);
        }
        finally { if (security != IntPtr.Zero) { _ = LocalFree(security); } }
    }

    internal static string FinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(32768);
        var count = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        if (count == 0 || count >= buffer.Capacity || !buffer.ToString().StartsWith(@"\\?\", StringComparison.Ordinal))
        { throw new IOException("Native configuration path is unavailable."); }
        return Path.TrimEndingDirectorySeparator(buffer.ToString()[4..]);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint sharing, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, uint objectType, uint information, IntPtr owner,
        IntPtr group, IntPtr dacl, IntPtr sacl, out IntPtr descriptor);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }
}
