using System.Diagnostics;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.Shared.Observability;
using Microsoft.Extensions.Logging;

try
{
    if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
    if (args.Length == 3 && args[0] == "child") { await Fixture.ChildAsync(args[1], args[2]); }
    else if (args.Length == 3 && args[0] == "check-reparse") { Fixture.CheckReparse(args[1], args[2]); }
    else if (args.Length == 0) { await Fixture.RunAsync(); }
    else { throw new ArgumentException("Unsupported fixture mode."); }
    return 0;
}
catch { Console.Error.WriteLine("Native synthetic adapter fixture failed; no installed acceptance claimed."); return 1; }

internal static class Fixture
{
    public static async Task RunAsync()
    {
        var run = Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), "fa-native-fixture-" + run);
        if (Directory.Exists(root) || File.Exists(root)) { throw new IOException("Refusing to reuse a fixture root."); }
        var owned = false;
        Process? child = null;
        DateTime childStarted = default;
        try
        {
            CreateDirectory(root); owned = true; File.WriteAllText(Path.Combine(root, "run.id"), run);
            CreateDirectory(Path.Combine(root, "receipts")); CreateDirectory(Path.Combine(root, "diagnostics")); CreateDirectory(Path.Combine(root, "restore"));
            using (var store = new NativeReceiptObjectStore(ReceiptOptions(Path.Combine(root, "receipts")), TimeProvider.System))
            { await store.StoreAsync("receipt_seed", "synthetic-seed-image"u8.ToArray(), default); }
            var executable = Environment.ProcessPath ?? throw new IOException("Fixture executable identity is unavailable.");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            { start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); }
            start.ArgumentList.Add("child"); start.ArgumentList.Add(root); start.ArgumentList.Add(run);
            child = Process.Start(start) ?? throw new IOException("Owned fixture child did not start.");
            childStarted = child.StartTime.ToUniversalTime();
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(root, "ready")))
            {
                if (child.HasExited || deadline.Elapsed > TimeSpan.FromSeconds(45)) { throw new IOException("Owned fixture child did not become ready."); }
                await Task.Delay(25);
            }
            StopOwned(child, childStarted);
            var expected = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(Path.Combine(root, "proof.json")))
                ?? throw new IOException("Fixture proof missing.");
            var actual = Proof(root);
            if (expected.Count != actual.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value))
            { throw new IOException("Committed ciphertext changed across the process crash."); }
            using (var restarted = new NativeReceiptObjectStore(ReceiptOptions(Path.Combine(root, "receipts")), TimeProvider.System))
            {
                var seed = await restarted.OpenReadAsync("receipt_seed", default) ?? throw new IOException("Seed missing after restart.");
                using (seed) { using var reader = new StreamReader(seed); if (await reader.ReadToEndAsync() != "synthetic-seed-image") { throw new IOException("Seed changed."); } }
                var info = await restarted.GetInfoAsync("receipt_committed") ?? throw new IOException("Committed object missing.");
                if (info.SizeBytes != 10 * 1024 * 1024) { throw new IOException("Maximum-sized receipt changed."); }
                if (await restarted.CleanupBatchAsync() != 1 || Directory.GetFiles(Path.Combine(root, "receipts"), "*.tmp").Length != 0)
                { throw new IOException("Abandoned temporary object did not recover."); }
                var backup = await restarted.ExportEncryptedAsync("receipt_committed");
                using var restored = new NativeReceiptObjectStore(ReceiptOptions(Path.Combine(root, "restore")), TimeProvider.System);
                await restored.RestoreEncryptedAsync("receipt_committed", backup);
                if (await restored.GetInfoAsync("receipt_committed") != info) { throw new IOException("Portable encrypted restore changed metadata."); }
                await restored.DeleteAsync("receipt_committed"); await restored.RestoreEncryptedAsync("receipt_committed", backup);
                if (await restored.OpenReadAsync("receipt_committed", default) is not null) { throw new IOException("Deleted restore target resurrected."); }
            }
            using (var diagnostics = new NativeDiagnosticsProvider(DiagnosticOptions(Path.Combine(root, "diagnostics")), "monitoring", TimeProvider.System))
            {
                var values = await diagnostics.ReadRecentAsync();
                if (values.Count != 1 || values[0].EventId != 27703 || Directory.GetFiles(Path.Combine(root, "diagnostics"), "*.tmp").Length != 0)
                { throw new IOException("Native diagnostic recovery failed."); }
            }
            Console.WriteLine("Native adapter fixture passed: actual owned-process crash/restart, exact committed ciphertext, 10 MiB receipt, abandoned-temp recovery, encrypted portable restore/deletion and diagnostic restart. Not SCM, power-loss, backup-system or installed-OS acceptance.");
        }
        finally
        {
            if (child is not null)
            {
                if (!child.HasExited) { StopOwned(child, childStarted); }
                child.Dispose();
            }
            ValidateRoot(root, run);
            if (owned && Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    public static async Task ChildAsync(string root, string run)
    {
        ValidateRoot(root, run);
        if (File.ReadAllText(Path.Combine(root, "run.id")) != run) { throw new IOException("Wrong fixture owner."); }
        using var store = new NativeReceiptObjectStore(ReceiptOptions(Path.Combine(root, "receipts")), TimeProvider.System);
        var bytes = new byte[10 * 1024 * 1024]; Array.Fill(bytes, (byte)0x51);
        await store.StoreAsync("receipt_committed", bytes, default); CryptographicOperations.ZeroMemory(bytes);
        using var diagnostics = new NativeDiagnosticsProvider(DiagnosticOptions(Path.Combine(root, "diagnostics")), "monitoring", TimeProvider.System);
        diagnostics.CreateLogger("synthetic").LogInformation(new EventId(27703), "Synthetic technical fixture event.");
        await diagnostics.FlushAsync();
        DurableWrite(Path.Combine(root, "proof.json"), JsonSerializer.SerializeToUtf8Bytes(Proof(root)));
        foreach (var directory in new[] { "receipts", "diagnostics" })
        { DurableWrite(Path.Combine(root, directory, Guid.NewGuid().ToString("N") + ".tmp"), new byte[] { 1, 2, 3 }); }
        DurableWrite(Path.Combine(root, "ready"), "ready"u8.ToArray());
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    public static void CheckReparse(string root, string run)
    {
        ValidateRoot(root, run);
        if (File.ReadAllText(Path.Combine(root, "run.id")) != run) { throw new IOException("Wrong reparse fixture owner."); }
        var target = Path.Combine(root, "target"); var link = Path.Combine(root, "junction");
        if (!Directory.Exists(target) || !File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint)) { throw new IOException("Expected owned junction fixture."); }
        using (var valid = new NativeReceiptObjectStore(ReceiptOptions(target), TimeProvider.System)) { }
        var denied = false;
        try { using var invalid = new NativeReceiptObjectStore(ReceiptOptions(link), TimeProvider.System); }
        catch (InvalidOperationException) { denied = true; }
        if (!denied || File.ReadAllText(Path.Combine(target, Guid.Empty.ToString("N") + ".tmp")) != "synthetic-owned-target")
        { throw new IOException("Native reparse boundary failed."); }
        Console.WriteLine("Native reparse fixture passed: actual owned junction rejected and its target preserved.");
    }

    private static Dictionary<string, string> Proof(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in new[] { "receipts", "diagnostics" })
            foreach (var path in Directory.EnumerateFiles(Path.Combine(root, directory), "*.object"))
            { result.Add(directory + "/" + Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))); }
        return result;
    }
    private static void StopOwned(Process process, DateTime started)
    {
        if (process.HasExited) { return; }
        if (started == default || process.StartTime.ToUniversalTime() != started
            || !string.Equals(Path.GetFullPath(process.MainModule!.FileName), Path.GetFullPath(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase))
        { throw new IOException("Fixture child identity changed; refusing termination."); }
        process.Kill(entireProcessTree: false);
        if (!process.WaitForExit(10000)) { throw new IOException("Owned fixture child did not stop."); }
    }
    private static void DurableWrite(string path, byte[] bytes)
    { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
    private static void ValidateRoot(string root, string run)
    {
        if (!Guid.TryParseExact(run, "N", out _) || Path.GetFullPath(root) != Path.Combine(Path.GetFullPath(Path.GetTempPath()), "fa-native-fixture-" + run))
        { throw new IOException("Unsafe fixture path."); }
    }
    private static void CreateDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
        using var identity = WindowsIdentity.GetCurrent(); var sid = identity.User!;
        var security = new DirectorySecurity(); security.SetOwner(sid); security.SetAccessRuleProtection(true, false);
        foreach (var value in new[] { sid.Value, "S-1-5-18", "S-1-5-32-544" }.Distinct())
        {
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(value), FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        }
        new DirectoryInfo(path).Create(security);
    }
    private static NativeReceiptObjectOptions ReceiptOptions(string path) => new()
    {
        RootPath = path,
        ServiceSid = CurrentSid(),
        RetentionHours = 1,
        IndexKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
        ActiveEncryptionKeyId = "synthetic_v1",
        EncryptionKeys = new() { ["synthetic_v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
    };
    private static NativeDiagnosticsOptions DiagnosticOptions(string path) => new() { Enabled = true, RootPath = path, ServiceSid = CurrentSid(), RetentionHours = 1 };
    private static string CurrentSid()
    { if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); } using var identity = WindowsIdentity.GetCurrent(); return identity.User!.Value; }
}
