using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Recovery;
using Npgsql;

var stage = "owned fixture validation";
try
{
    if (!OperatingSystem.IsWindows()) { throw new InvalidOperationException(); }
    var credential = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_PASSWORD") ?? "";
    var operatorSecret = Environment.GetEnvironmentVariable("FA_RECOVERY_TEST_PASSPHRASE") ?? "";
    var mode = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_MODE") ?? "";
    var directory = Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_DIRECTORY") ?? "";
    var scope = Environment.GetEnvironmentVariable("FA_RECOVERY_TEST_SCOPE") ?? "";
    Check(scope is "IncomeExpense" or "ProfileCategory");
    var profileCategory = scope == "ProfileCategory";
    var scopeName = profileCategory ? "profile-category" : "income-expense";
    var owners = profileCategory ? new[] { (Owner: "profile", Seed: (byte)1), (Owner: "category", Seed: (byte)3) }
        : new[] { (Owner: "income", Seed: (byte)1), (Owner: "expense", Seed: (byte)3) };
    var sourceDatabase = profileCategory ? "fa_test_fin283" : "fa_test_fin275";
    var expectedKeyFailure = profileCategory ? "Profile/Category exact persistence" : "Income exact persistence";
    Check(credential.Length == 64 && credential.All(char.IsAsciiHexDigit) && operatorSecret.Length == 64 && operatorSecret.All(char.IsAsciiHexDigit)
        && mode is "initial" or "restart" && Path.IsPathFullyQualified(directory) && Directory.Exists(directory));
    Check(int.TryParse(Environment.GetEnvironmentVariable("FA_PERSISTENCE_TEST_PORT"), out var port) && port is >= 49152 and <= 65535);
    var repository = new DirectoryInfo(AppContext.BaseDirectory);
    while (!File.Exists(Path.Combine(repository.FullName, "FinancialAssistant.Backend.sln")))
    { repository = repository.Parent ?? throw new InvalidOperationException(); }
    var data = new DirectoryInfo(Path.GetFullPath(directory)); var run = data.Parent ?? throw new InvalidOperationException(); var root = run.FullName;
    Check(data.Name == "data" && Guid.TryParseExact(run.Name, "N", out _) && string.Equals(run.Parent?.FullName,
        Path.Combine(repository.FullName, ".codex-runtime", "persistence-test"), StringComparison.OrdinalIgnoreCase));
    for (var entry = data; entry is not null; entry = entry.Parent) { Check((entry.Attributes & FileAttributes.ReparsePoint) == 0); }
    var connection = new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1",
        Port = port,
        Database = "postgres",
        Username = "fa_test_admin",
        Password = credential,
        IncludeErrorDetail = false,
        LogParameters = false,
        Timeout = 3,
        CommandTimeout = 10
    };
    await using var admin = new NpgsqlConnection(connection.ConnectionString); await admin.OpenAsync();
    await using (var location = new NpgsqlCommand("SHOW data_directory", admin))
    { Check(string.Equals(Path.GetFullPath((string)(await location.ExecuteScalarAsync())!), data.FullName, StringComparison.OrdinalIgnoreCase)); }
    var binaries = Path.Combine(root, "pgsql", "bin");
    var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
    var dotnet = Path.Combine(runtime.Parent!.Parent!.Parent!.FullName, "dotnet.exe");
    Check(File.Exists(dotnet));
    var project = profileCategory ? "FinancialAssistant.ProfileCategory.Persistence.Integration" : "FinancialAssistant.FinancialRecords.Persistence.Integration";
    var child = Path.Combine(repository.FullName, "backend", "services", profileCategory ? "profile" : "income", project, "bin", "Release", "net8.0", project + ".dll");
    Check(File.Exists(child));
    var marker = Path.Combine(root, "recovery.run"); var catalog = Path.Combine(root, "recovery.json");
    var passphrase = Encoding.ASCII.GetBytes(operatorSecret); var temporary = new List<string>();
    byte[]? recoveredKey = null;
    try
    {
        RecoveryDocument document;
        if (mode == "initial")
        {
            WriteNew(marker, Encoding.ASCII.GetBytes(run.Name));
            stage = "source business initialization";
            await Child("initial");
            await using (var sessions = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=$1 AND backend_type='client backend'", admin))
            { sessions.Parameters.AddWithValue(sourceDatabase); Check((long)(await sessions.ExecuteScalarAsync())! == 0); }
            document = new RecoveryDocument { InstallationId = Guid.NewGuid(), BackupId = Guid.NewGuid(), Scope = scopeName };
            var originalKey = NativeRecoveryKeyWrapping.CreateRecoveryKey();
            try
            {
                document.WrappedKey = NativeRecoveryKeyWrapping.Wrap(document.InstallationId, document.BackupId, originalKey, passphrase);
                foreach (var (owner, seed) in owners)
                {
                    stage = "trusted quiescent owned schema dump";
                    var raw = Path.Combine(root, Guid.NewGuid().ToString("N") + ".dump");
                    Check(!File.Exists(raw)); temporary.Add(raw);
                    await Run(Path.Combine(binaries, "pg_dump.exe"), ["--host=127.0.0.1", "--port=" + port, "--username=fa_test_admin", "--no-password",
                        "--dbname=" + sourceDatabase, "--format=custom", "--schema=fa_" + owner, "--file=" + raw], null, credential);
                    Check(new FileInfo(raw).Length is > 0 and <= NativeRecoveryArtifactEncryption.MaximumPlaintextBytes && (File.GetAttributes(raw) & FileAttributes.ReparsePoint) == 0);
                    var payload = File.ReadAllBytes(raw);
                    try { Capture(owner, NativeRecoveryArtifactKind.Database, payload, originalKey, document); }
                    finally { CryptographicOperations.ZeroMemory(payload); File.Delete(raw); }
                    var keys = new byte[64]; keys.AsSpan(0, 32).Fill(seed); keys.AsSpan(32, 32).Fill((byte)(seed + 1));
                    try { Capture(owner, NativeRecoveryArtifactKind.Configuration, keys, originalKey, document); }
                    finally { CryptographicOperations.ZeroMemory(keys); }
                }
                WriteNew(catalog, JsonSerializer.SerializeToUtf8Bytes(document));
            }
            finally { CryptographicOperations.ZeroMemory(originalKey); }
        }
        else
        {
            Check(new FileInfo(marker).Length == 32 && (File.GetAttributes(marker) & FileAttributes.ReparsePoint) == 0 && File.ReadAllText(marker) == run.Name);
            await Child("restart");
        }
        stage = "independent portable key and artifact recovery";
        Check(new FileInfo(catalog).Length is > 0 and <= 16384 && (File.GetAttributes(catalog) & FileAttributes.ReparsePoint) == 0);
        document = JsonSerializer.Deserialize<RecoveryDocument>(File.ReadAllBytes(catalog), new JsonSerializerOptions
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 }) ?? throw new InvalidOperationException();
        Check(document.InstallationId != Guid.Empty && document.BackupId != Guid.Empty && document.Scope == scopeName && document.Artifacts.Count == 4
            && document.Artifacts.Select(artifact => artifact.Id).Distinct().Count() == 4
            && document.Artifacts.Select(artifact => (artifact.Owner, artifact.Kind)).Distinct().Count() == 4
            && document.Artifacts.All(artifact => owners.Any(owner => owner.Owner == artifact.Owner) && artifact.Kind is NativeRecoveryArtifactKind.Database or NativeRecoveryArtifactKind.Configuration));
        recoveredKey = NativeRecoveryKeyWrapping.Unwrap(document.InstallationId, document.BackupId, document.WrappedKey, passphrase);
        var restores = new List<string>();
        foreach (var artifact in document.Artifacts)
        {
            var path = Path.Combine(root, artifact.Id.ToString("N") + ".recovery");
            Check(new FileInfo(path).Length is > 36 and <= NativeRecoveryArtifactEncryption.MaximumPlaintextBytes + 36 && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0);
            var ciphertext = File.ReadAllBytes(path); Check(Convert.ToHexString(SHA256.HashData(ciphertext)) == artifact.Digest);
            var plaintext = NativeRecoveryArtifactEncryption.Open(ciphertext, recoveredKey,
                NativeRecoveryContext.Create(document.InstallationId, document.BackupId, artifact.Id, artifact.Owner, artifact.Kind));
            try
            {
                if (artifact.Kind == NativeRecoveryArtifactKind.Configuration)
                {
                    Check(plaintext.Length == 64); var keys = Path.Combine(root, "recovered-" + artifact.Owner + ".keys");
                    WriteNew(keys, plaintext, temporary);
                }
                else if (mode == "initial")
                {
                    var dump = Path.Combine(root, artifact.Id.ToString("N") + ".restore");
                    WriteNew(dump, plaintext, temporary); restores.Add(dump);
                }
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        if (mode == "initial")
        {
            stage = "create empty isolated restore target";
            await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname='fa_test_fin209_restore'", admin))
            { Check((long)(await exists.ExecuteScalarAsync())! == 0); }
            await Sql(admin, "CREATE DATABASE fa_test_fin209_restore TEMPLATE template0");
            var roles = string.Join(",", owners.SelectMany(owner => new[] { "fa_" + owner.Owner, "fa_" + owner.Owner + "_migration" }));
            await Sql(admin, "REVOKE ALL ON DATABASE fa_test_fin209_restore FROM PUBLIC; GRANT CONNECT ON DATABASE fa_test_fin209_restore TO " + roles);
            // A fresh template still grants public schema access; native stores require that grant removed.
            await using (var target = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "fa_test_fin209_restore" }.ConnectionString))
            { await target.OpenAsync(); await Sql(target, "REVOKE ALL ON SCHEMA public FROM PUBLIC"); }
            foreach (var dump in restores)
            {
                stage = "restore trusted synthetic schema without overwrite";
                await Run(Path.Combine(binaries, "pg_restore.exe"), ["--host=127.0.0.1", "--port=" + port, "--username=fa_test_admin", "--no-password",
                    "--dbname=fa_test_fin209_restore", "--exit-on-error", "--single-transaction", dump], null, credential);
            }
        }
        stage = "wrong recovered key denial";
        var recoveredFirst = Path.Combine(root, "recovered-" + owners[0].Owner + ".keys");
        var correctKeys = File.ReadAllBytes(recoveredFirst);
        try
        {
            var wrongKeys = RandomNumberGenerator.GetBytes(64);
            try { File.WriteAllBytes(recoveredFirst, wrongKeys); }
            finally { CryptographicOperations.ZeroMemory(wrongKeys); }
            await Run(dotnet, [child], "restore", null, expectedKeyFailure);
        }
        finally
        {
            try { File.WriteAllBytes(recoveredFirst, correctKeys); }
            finally { CryptographicOperations.ZeroMemory(correctKeys); }
        }
        stage = "restored key access business reconciliation and immutable replay";
        await Child("restore");
        WriteNew(Path.Combine(root, mode == "initial" ? "recovery-initial.json" : "recovery-restart.json"), JsonSerializer.SerializeToUtf8Bytes(new
        { schemaVersion = 1, passed = true, scope = scopeName, portableKeys = true, wrongKeyRejected = true, businessReconciled = true, immutableReplay = true, installed = false, releaseReady = false }));
        Console.WriteLine("PASS owned recovery scope=" + scopeName + ": encrypted per-owner schema artifacts, independent portable key recovery, new empty target, exact business snapshots/owner isolation and idempotent replay. Not complete POC or installed acceptance.");

        async Task Child(string childMode) => await Run(dotnet, [child], childMode, null);
        void Capture(string owner, NativeRecoveryArtifactKind kind, byte[] plaintext, byte[] key, RecoveryDocument target)
        {
            var artifact = new RecoveryArtifact { Id = Guid.NewGuid(), Owner = owner, Kind = kind };
            var ciphertext = NativeRecoveryArtifactEncryption.Seal(plaintext, key,
                NativeRecoveryContext.Create(target.InstallationId, target.BackupId, artifact.Id, owner, kind));
            artifact.Digest = Convert.ToHexString(SHA256.HashData(ciphertext));
            WriteNew(Path.Combine(root, artifact.Id.ToString("N") + ".recovery"), ciphertext); target.Artifacts.Add(artifact);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(passphrase); if (recoveredKey is not null) { CryptographicOperations.ZeroMemory(recoveredKey); }
        foreach (var path in temporary) { if (File.Exists(path)) { File.Delete(path); } }
    }
    return 0;
}
catch { Console.Error.WriteLine("FAIL Native Recovery PostgreSQL at " + stage + ". Sensitive details suppressed."); return 1; }

static void Check(bool value) { if (!value) { throw new InvalidOperationException(); } }
static async Task Sql(NpgsqlConnection connection, string sql)
{ await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
static void WriteNew(string path, byte[] bytes, List<string>? temporary = null)
{ using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); temporary?.Add(path); stream.Write(bytes); stream.Flush(true); }
static async Task Run(string executable, string[] arguments, string? mode, string? credential, string? expectedFailureStage = null)
{
    var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) { start.ArgumentList.Add(argument); }
    start.Environment.Remove("FA_RECOVERY_TEST_PASSPHRASE");
    if (mode is not null) { start.Environment["FA_PERSISTENCE_TEST_MODE"] = mode; }
    if (credential is not null)
    {
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("PG", StringComparison.OrdinalIgnoreCase)).ToArray()) { start.Environment.Remove(name); }
        start.Environment["PGPASSWORD"] = credential; start.Environment["PGSSLMODE"] = "disable";
    }
    using var process = Process.Start(start) ?? throw new InvalidOperationException(); var began = process.StartTime.ToUniversalTime();
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch (OperationCanceledException)
    {
        if (process.StartTime.ToUniversalTime() != began || !string.Equals(process.MainModule?.FileName, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException(); }
        process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw;
    }
    _ = await output; var diagnostics = await error;
    if (expectedFailureStage is not null)
    {
        Check(process.ExitCode == 1 && diagnostics.Contains("at " + expectedFailureStage, StringComparison.Ordinal));
        return;
    }
    if (mode is not null && process.ExitCode != 0)
    {
        foreach (var name in new[] { "owned fixture validation", "Income exact persistence", "Expense exact persistence", "Profile/Category exact persistence", "restored exact profile and category state", "cross-service role denial" })
        { if (diagnostics.Contains("at " + name, StringComparison.Ordinal)) { Console.Error.WriteLine("Owned financial child failed at " + name + "."); } }
    }
    if (process.ExitCode != 0 || credential is not null && !string.IsNullOrWhiteSpace(diagnostics)) { throw new InvalidOperationException(); }
}

sealed class RecoveryDocument
{
    public string Scope { get; set; } = "";
    public Guid InstallationId { get; set; }
    public Guid BackupId { get; set; }
    public byte[] WrappedKey { get; set; } = Array.Empty<byte>();
    public List<RecoveryArtifact> Artifacts { get; set; } = new();
}
sealed class RecoveryArtifact
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = "";
    public NativeRecoveryArtifactKind Kind { get; set; }
    public string Digest { get; set; } = "";
}
