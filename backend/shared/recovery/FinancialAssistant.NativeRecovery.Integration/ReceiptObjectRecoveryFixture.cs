using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.NativeFiles;

internal static class ReceiptObjectRecoveryFixture
{
    private const int MaximumFileBytes = 1024 * 1024;
    private const int MaximumBundleBytes = 6 * 1024 * 1024;

    internal static async Task<byte[]> CaptureAsync(string path)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        using var identity = WindowsIdentity.GetCurrent();
        using var directory = new ProtectedDirectory(path, identity.User!.Value, 10);
        var names = directory.ListFiles(); Check(names.Count == 4);
        var files = new List<ObjectFile>();
        try
        {
            foreach (var name in names)
            {
                ValidateName(name);
                files.Add(new ObjectFile(name, await directory.ReadAsync(name, MaximumFileBytes) ?? throw new InvalidOperationException()));
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new ObjectBundle(1, files));
            Check(bytes.Length <= MaximumBundleBytes); return bytes;
        }
        finally { foreach (var file in files) { CryptographicOperations.ZeroMemory(file.Bytes); } }
    }

    internal static async Task RestoreAsync(string path, byte[] bytes)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        var bundle = Decode(bytes);
        try
        {
            Check(!Directory.Exists(path) && !File.Exists(path));
            using var identity = WindowsIdentity.GetCurrent();
            var security = new DirectorySecurity(); security.SetOwner(identity.User!); security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { identity.User!.Value, "S-1-5-18", "S-1-5-32-544" }.Distinct())
            {
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            }
            new DirectoryInfo(path).Create(security);
            using var directory = new ProtectedDirectory(path, identity.User!.Value, 10);
            Check(directory.ListFiles().Count == 0);
            foreach (var file in bundle.Files) { await directory.WriteAsync(file.Name, file.Bytes, replace: false); }
        }
        finally { Clear(bundle); }
        await VerifyAsync(path, bytes);
    }

    internal static async Task VerifyAsync(string path, byte[] bytes)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        var bundle = Decode(bytes);
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            using var directory = new ProtectedDirectory(path, identity.User!.Value, 10);
            Check(directory.ListFiles().SequenceEqual(bundle.Files.Select(file => file.Name).Order(StringComparer.Ordinal)));
            foreach (var file in bundle.Files)
            {
                var actual = await directory.ReadAsync(file.Name, MaximumFileBytes) ?? throw new InvalidOperationException();
                try { Check(CryptographicOperations.FixedTimeEquals(actual, file.Bytes)); }
                finally { CryptographicOperations.ZeroMemory(actual); }
            }
        }
        finally { Clear(bundle); }
    }

    private static ObjectBundle Decode(byte[] bytes)
    {
        Check(bytes.Length is > 0 and <= MaximumBundleBytes);
        var bundle = JsonSerializer.Deserialize<ObjectBundle>(bytes, new JsonSerializerOptions
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 5 }) ?? throw new InvalidOperationException();
        try
        {
            Check(bundle.Schema == 1 && bundle.Files is { Count: 4 }
                && bundle.Files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4);
            foreach (var file in bundle.Files)
            { ValidateName(file.Name); Check(file.Bytes is { Length: > 0 and <= MaximumFileBytes }); }
            return bundle;
        }
        catch { Clear(bundle); throw; }
    }

    private static void ValidateName(string name) => Check(name is { Length: 71 } && name.EndsWith(".object", StringComparison.Ordinal)
        && name.AsSpan(0, 64).ToString().All(character => char.IsAsciiDigit(character) || character is >= 'A' and <= 'F'));
    private static void Clear(ObjectBundle bundle)
    { foreach (var file in bundle.Files ?? []) { if (file?.Bytes is not null) { CryptographicOperations.ZeroMemory(file.Bytes); } } }
    private static void Check(bool value) { if (!value) { throw new InvalidOperationException(); } }
    private sealed record ObjectBundle([property: JsonRequired] int Schema, [property: JsonRequired] List<ObjectFile> Files);
    private sealed record ObjectFile([property: JsonRequired] string Name, [property: JsonRequired] byte[] Bytes);
}
