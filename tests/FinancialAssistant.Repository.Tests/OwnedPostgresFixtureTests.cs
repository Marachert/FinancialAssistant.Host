using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class OwnedPostgresFixtureTests
{
    [Theory]
    [InlineData("backend/shared/persistence/FinancialAssistant.Shared.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Shared.Persistence.Integration.dll", "FAIL PostgreSQL integration at fixture validation")]
    [InlineData("backend/services/identity/FinancialAssistant.Identity.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Identity.Persistence.Integration.dll", "FAIL identity PostgreSQL at owned fixture validation")]
    [InlineData("backend/services/profile/FinancialAssistant.ProfileCategory.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.ProfileCategory.Persistence.Integration.dll", "FAIL Profile/Category PostgreSQL at owned fixture validation")]
    [InlineData("backend/services/income/FinancialAssistant.FinancialRecords.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.FinancialRecords.Persistence.Integration.dll", "FAIL Income/Expense PostgreSQL at owned fixture validation")]
    [InlineData("backend/services/transaction-intake/FinancialAssistant.Intake.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Intake.Persistence.Integration.dll", "FAIL Intake PostgreSQL at owned fixture validation")]
    [InlineData("backend/services/receipt-processing/FinancialAssistant.ReceiptAi.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.ReceiptAi.Persistence.Integration.dll", "FAIL Receipt/AI PostgreSQL at isolated fixture validation")]
    [InlineData("backend/services/financial-summary/FinancialAssistant.Projections.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Projections.Persistence.Integration.dll", "FAIL Projections PostgreSQL at isolated fixture validation")]
    [InlineData("backend/services/financial-score/FinancialAssistant.Insights.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Insights.Persistence.Integration.dll", "FAIL Insights PostgreSQL at isolated fixture validation")]
    [InlineData("backend/services/audit/FinancialAssistant.Audit.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Audit.Persistence.Integration.dll", "FAIL Audit PostgreSQL at isolated fixture validation")]
    [InlineData("backend/shared/recovery/FinancialAssistant.NativeRecovery.Integration/bin/Release/net8.0/FinancialAssistant.NativeRecovery.Integration.dll", "FAIL Native Recovery PostgreSQL at owned fixture validation")]
    public async Task MissingFixtureConfigurationFailsWithoutDatabaseMutation(string executable, string expectedFailure)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(Root(), executable));
        foreach (var name in new[] { "FA_PERSISTENCE_TEST_PASSWORD", "FA_PERSISTENCE_TEST_PORT", "FA_PERSISTENCE_TEST_DIRECTORY", "FA_PERSISTENCE_TEST_MODE" })
        {
            start.Environment.Remove(name);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Integration process unavailable.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        Assert.Equal(1, process.ExitCode);
        Assert.Contains(expectedFailure, await output + await error);
    }

    [Fact]
    public async Task UnverifiedArchiveFailsBeforeProcessStartup()
    {
        var root = Root();
        var directory = Path.Combine(root, ".codex-runtime", "fixture-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var archive = Path.Combine(directory, "synthetic-untrusted.zip");
        try
        {
            await File.WriteAllTextAsync(archive, "synthetic unverified archive");
            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", "tools/scripts/test-owned-postgres.ps1", "-ArchivePath", archive })
            {
                start.ArgumentList.Add(argument);
            }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell unavailable.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            var combined = await output + await error;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(OperatingSystem.IsWindows() ? "archive digest mismatch" : "Windows PostgreSQL archive", combined);
            Assert.DoesNotContain("PASS initial PostgreSQL", combined);
            Assert.Equal("synthetic unverified archive", await File.ReadAllTextAsync(archive));
        }
        finally { File.Delete(archive); Directory.Delete(directory, recursive: false); }
    }

    [Fact]
    public void FixtureHasExplicitOwnershipCleanupAndNoInstallation()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "tools/scripts/test-owned-postgres.ps1"));
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("$serverProcess.StartTime", script);
        Assert.Contains("$serverProcess.Path", script);
        Assert.Contains("$saved[$name]", script);
        Assert.Contains("releaseReady=$false", script);
        Assert.Contains("--auth-host=scram-sha-256", script);
        Assert.Contains("[ValidateSet('Foundation','Identity','ProfileCategory','FinancialRecords','Intake','ReceiptAi','Projections','Insights','Audit','NativeLifecycle','Recovery')]", script);
        Assert.Contains("FinancialAssistant.NativeRecovery.Integration.csproj", script);
        Assert.Contains("Scoped owned business recovery evidence is incomplete.", script);
        Assert.Contains("[ValidateSet('IncomeExpense','ProfileCategory','Identity','Intake','Audit')]", script);
        Assert.Contains("$proof.scope -cne $expectedRecoveryScope", script);
        Assert.Contains("FA_RECOVERY_TEST_SCOPE", script);
        Assert.Contains("$result.recoveryScope", script);
        Assert.Contains("FinancialAssistant.NativeLifecycle.Integration.csproj", script);
        Assert.Contains("[IO.FileMode]::CreateNew", script);
        Assert.Contains("FinancialAssistant.Audit.Persistence.Integration.csproj", script);
        Assert.Contains("FinancialAssistant.Insights.Persistence.Integration.csproj", script);
        Assert.Contains("FinancialAssistant.Projections.Persistence.Integration.csproj", script);
        Assert.Contains("FinancialAssistant.ReceiptAi.Persistence.Integration.csproj", script);
        Assert.Contains("FinancialAssistant.Intake.Persistence.Integration.csproj", script);
        Assert.Contains("FinancialAssistant.FinancialRecords.Persistence.Integration.csproj", script);
        Assert.DoesNotContain("New-Service", script);
        Assert.DoesNotContain("sc.exe", script);
        Assert.DoesNotContain("Unblock-File", script);
        Assert.DoesNotContain("Remove-Item -Recurse", script);
    }

    [Fact]
    public void RecoveryFixtureRequiresOwnedClusterAndNewTargetAndNeverOverwritesAStore()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "backend/shared/recovery/FinancialAssistant.NativeRecovery.Integration/Program.cs"));
        Assert.Contains("SHOW data_directory", source);
        Assert.Contains("CREATE DATABASE fa_test_fin209_restore TEMPLATE template0", source);
        Assert.Contains("SELECT count(*) FROM pg_database WHERE datname='fa_test_fin209_restore'", source);
        Assert.Contains("NativeRecoveryKeyWrapping.Unwrap", source);
        Assert.Contains("NativeRecoveryArtifactEncryption.Open", source);
        Assert.Contains("await Child(\"restore\")", source);
        Assert.Contains("REVOKE ALL ON SCHEMA public FROM PUBLIC", source);
        Assert.Contains("\"restore\", null, expectedKeyFailure", source);
        Assert.Contains("new RecoveryScope(\"profile-category\"", source);
        Assert.Contains("\"Profile/Category exact persistence\"", source);
        Assert.Contains("document.Scope == scopeName", source);
        Assert.Contains("scope = scopeName", source);
        Assert.Contains("wrongKeyRejected = true", source);
        Assert.Contains("\"--exit-on-error\", \"--single-transaction\"", source);
        Assert.Contains("FileMode.CreateNew", source);
        Assert.DoesNotContain("--clean", source);
        Assert.DoesNotContain("DROP DATABASE", source);
        Assert.DoesNotContain("DROP SCHEMA", source);
        var records = File.ReadAllText(Path.Combine(Root(), "backend/services/income/FinancialAssistant.FinancialRecords.Persistence.Integration/FinancialRecordsFixture.cs"));
        Assert.Contains("mode != \"restore\" || recovery", records);
        Assert.Contains("recovery.run", records);
        Assert.Contains("recovered-income.keys", records);
        Assert.Contains("recovered-expense.keys", records);
        var profiles = File.ReadAllText(Path.Combine(Root(), "backend/services/profile/FinancialAssistant.ProfileCategory.Persistence.Integration/ProfileCategoryFixture.cs"));
        Assert.Contains("mode != \"restore\" || recovery", profiles);
        Assert.Contains("Setting(\"FA_RECOVERY_TEST_SCOPE\") == \"ProfileCategory\"", profiles);
        Assert.Contains("recovered-profile.keys", profiles);
        Assert.Contains("recovered-category.keys", profiles);
        Assert.Contains("CryptographicOperations.ZeroMemory(keys)", profiles);
        Assert.Contains("synthetic-restored-conflict", profiles);
        Assert.Contains("Hash(replayed) == eventHash", profiles);
        var identity = File.ReadAllText(Path.Combine(Root(), "backend/services/identity/FinancialAssistant.Identity.Persistence.Integration/IdentityPersistenceFixture.cs"));
        Assert.Contains("mode != \"restore\" || recovery", identity);
        Assert.Contains("captured-identity.keys", identity);
        Assert.Contains("recovered-identity.keys", identity);
        Assert.Contains("AuthenticationRollback", identity);
        Assert.Contains("ValidateToken(response.AccessToken", identity);
        var intake = File.ReadAllText(Path.Combine(Root(), "backend/services/transaction-intake/FinancialAssistant.Intake.Persistence.Integration/IntakeFixture.cs"));
        Assert.Contains("mode != \"restore\" || recovery", intake);
        Assert.Contains("recovered-", intake);
        Assert.Contains("proof.OutboxHash == await OutboxHash(database)", intake);
        Assert.Contains("new StorageIdentity(owner).Schema", source);
        Assert.Contains("new(\"audit\", 17, 19)", source);
        var audit = File.ReadAllText(Path.Combine(Root(), "backend/services/audit/FinancialAssistant.Audit.Persistence.Integration/AuditIntegration.cs"));
        Assert.Contains("mode != \"restore\" || recovery", audit);
        Assert.Contains("proof.Digest == await SnapshotHash(auditDatabase)", audit);
        Assert.Contains("Audit recovery snapshot", audit);
    }

    [Theory]
    [InlineData("Foundation", "ProfileCategory")]
    [InlineData("ProfileCategory", "ProfileCategory")]
    [InlineData("FinancialRecords", "IncomeExpense")]
    [InlineData("Identity", "Identity")]
    [InlineData("Intake", "Intake")]
    [InlineData("Audit", "Audit")]
    public async Task RecoveryScopeCannotBeSilentlyIgnoredByAnotherScenario(string scenario, string scope)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", "tools/scripts/test-owned-postgres.ps1",
            "-ArchivePath", "synthetic-nonexistent-untrusted.zip", "-Scenario", scenario, "-RecoveryScope", scope })
        { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell unavailable.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        var diagnostics = await output + await error;
        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("RecoveryScope applies only to the Recovery scenario.", diagnostics);
        Assert.DoesNotContain("Owned local PostgreSQL fixture passed", diagnostics);
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln"))) { return directory.FullName; }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
