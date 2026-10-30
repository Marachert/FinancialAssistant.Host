using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativeBrokerFixtureTests
{
    [Fact]
    public void RepeatableRunnerUsesExplicitPackagesAndOwnedProcessCleanup()
    {
        var source = Read("tools/scripts/test-native-broker.ps1");
        Assert.Contains("[Parameter(Mandatory)][string] $PreparedPackageDirectory", source);
        Assert.Contains("[string] $PostgresArchivePath", source);
        Assert.Contains("SetAccessRuleProtection($true, $false)", source);
        Assert.Contains("process.StartTime.ToUniversalTime() -ne $Entry.StartedAt", source);
        Assert.Contains("process.MainModule.FileName -ne $Entry.Exe", source);
        Assert.Contains("financial-postgres-location.json", source);
        Assert.Contains("PostgreSQL recovery ownership mismatch", source);
        Assert.Contains("temporaryCredentialFilesRemoved", source);
        Assert.DoesNotContain("p35", source);
        Assert.DoesNotContain("Invoke-WebRequest", source);
        Assert.DoesNotContain("Start-Service", source);
    }

    [Fact]
    public void CombinedFixtureObservesActualWorkersAndOutboxRatherThanCallingProjectors()
    {
        var source = Read("backend/shared/messaging/FinancialAssistant.NativeLifecycle.Integration/FinancialDelivery.cs");
        Assert.Contains("GetServices<IHostedService>()", source);
        Assert.Contains("AcknowledgedDeliveries > acknowledgements", source);
        Assert.Contains("message.Confirmation?.DraftId == queued", source);
        Assert.Contains("message.Confirmation?.DraftId == state.QueuedIncomeDraft", source);
        Assert.Contains("GetSnapshotAsync(Owner", source);
        Assert.Contains("NotificationDeliveryStatuses.Prepared", source);
        Assert.Contains("providerInvocations = 0", source);
        Assert.DoesNotContain(".ProjectAsync(", source);
        Assert.DoesNotContain(".ProcessAnalyticsUpdatedAsync(", source);
        Assert.DoesNotContain(".ProcessScoreCalculatedAsync(", source);
        Assert.DoesNotContain("NotificationDeliveryService>", source);
    }

    [Fact]
    public void PostgresHarnessUsesAtomicEvidenceAndChecksOwnershipBeforeStopping()
    {
        var source = Read("tools/scripts/test-owned-postgres.ps1");
        Assert.Contains("[IO.FileMode]::CreateNew", source);
        Assert.DoesNotContain("-NoClobber", source);
        Assert.Contains("$serverProcess.Path -ne", source);
        Assert.Contains("$serverProcess.StartTime.ToUniversalTime() -lt $runStarted", source);
        Assert.Contains("'-m','immediate'", source);
        Assert.Contains("-Scenario", Read("docs/engineering/native-lifecycle-delivery.md"));
    }

    private static string Read(string path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
            { return File.ReadAllText(Path.Combine(directory.FullName, path)); }
        }
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }
}
