using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class AlertRuleDocumentationTests
{
    private const string Guide = "docs/engineering/alerting-and-incident-priorities.md";

    [Fact]
    public void Inventory_IsOfflineAndCannotAuthorizeSendingOrMutation()
    {
        using var document = JsonDocument.Parse(Read("docs/delivery/alert-rules.json"));
        var root = document.RootElement;
        Assert.Equal(new[] { "classification", "enabled", "destination", "automaticMutationAllowed", "runbook", "rules" },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("offline-alert-definition-only", root.GetProperty("classification").GetString());
        Assert.False(root.GetProperty("enabled").GetBoolean());
        Assert.False(root.GetProperty("automaticMutationAllowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("destination").ValueKind);
        Assert.Equal(Guide, root.GetProperty("runbook").GetString());
        Assert.Contains("not executable configuration", Read(Guide), StringComparison.Ordinal);
    }

    [Fact]
    public void Inventory_CoversTicketScopeAndRequiresOwnedSources()
    {
        using var document = JsonDocument.Parse(Read("docs/delivery/alert-rules.json"));
        var rules = document.RootElement.GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal(new[]
        {
            "service_down", "provider_failure", "queue_backlog", "http_error_rate",
            "storage_failure", "release_blocker", "visibility_gap", "integrity_security"
        }, rules.Select(rule => rule.GetProperty("id").GetString()));
        foreach (var rule in rules)
        {
            Assert.Equal(new[] { "id", "priority", "owner", "sourceGate" }, rule.EnumerateObject().Select(property => property.Name));
            Assert.Matches("^[a-z][a-z-]+$", rule.GetProperty("owner").GetString()!);
            Assert.Matches("^[a-z][a-z-]+$", rule.GetProperty("sourceGate").GetString()!);
            Assert.Contains(rule.GetProperty("priority").GetString(), new[] { "P1", "P2", "P3", "P4" });
            Assert.Contains($"| `{rule.GetProperty("id").GetString()}` |", Read(Guide), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Guide_PreservesMissingDataAndUnimplementedSignalBoundaries()
    {
        var guide = Read(Guide);
        foreach (var phrase in new[]
        {
            "broker-wide", "per-queue adapter", "absent numeric fields to zero",
            "process-local", "not request error rates", "never zero or healthy",
            "Counter resets", "consent suppression", "unchanged candidate",
            "no automatic clear", "durable incident", "owner reconciliation",
            "unknown", "90 seconds", "30 seconds", "maximum 60-minute expiry",
            "never authorize replay", "maximum-spend approval (zero by default)",
            "not an SLA", "not restoration-time", "FIN-199", "FIN-200/P9", "FIN-216"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }

        var probe = Read("backend/services/monitoring/FinancialAssistant.Monitoring.Infrastructure/HttpMonitoringDependencyProbe.cs");
        Assert.Contains("\"/api/overview\"", probe, StringComparison.Ordinal);
        Assert.Contains("ReadInt64(root, \"queue_totals\", \"messages\")", probe, StringComparison.Ordinal);
        Assert.Contains("ReadInt32(root, \"object_totals\", \"consumers\")", probe, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpRate_UsesFullWindowMinimumIncludingLowVolumeFailure()
    {
        var guide = Read(Guide);
        Assert.Contains("at least 20 completed requests across the full rolling five-minute window", guide, StringComparison.Ordinal);
        Assert.Contains("20/20 failures over five minutes and triggers P2", guide, StringComparison.Ordinal);
        Assert.Contains("Nineteen total requests remain", guide, StringComparison.Ordinal);
        Assert.Contains("one failure among 20 total requests is 5%", guide, StringComparison.Ordinal);
        Assert.Contains("not 20 per minute", guide, StringComparison.Ordinal);
        var row = guide.Split('\n').Single(line => line.StartsWith("| `http_error_rate` |", StringComparison.Ordinal));
        Assert.DoesNotContain("each of five", row, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseGate_DoesNotPageRoutinePendingChecksOrWeakenMergeGate()
    {
        var guide = Read(Guide);
        Assert.Contains("Ordinary pending CI prevents merge but does not page", guide, StringComparison.Ordinal);
        Assert.Contains("30-minute build", guide, StringComparison.Ordinal);
        Assert.Contains("before release approval is requested", guide, StringComparison.Ordinal);
        Assert.Contains("more than 30 minutes after candidate registration", guide, StringComparison.Ordinal);
        Assert.Contains("every mandatory check must succeed on the exact head", guide, StringComparison.Ordinal);
        Assert.Contains("P1 security/integrity immediately", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void Guide_IsDiscoverableFromExistingOperationalPolicy()
    {
        foreach (var path in new[]
        {
            "docs/README.md", "docs/architecture/backend-observability-strategy.md",
            "docs/engineering/service-health-and-readiness.md", "docs/engineering/failed-job-support-workflow.md"
        })
        {
            Assert.Contains("alerting-and-incident-priorities.md", Read(path), StringComparison.Ordinal);
        }
    }

    private static string Read(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return File.ReadAllText(Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
