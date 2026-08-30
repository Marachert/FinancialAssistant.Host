using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class BackendObservabilityStrategyTests
{
    [Fact]
    public void Strategy_DefinesEveryRequiredSignalAndOwnershipBoundary()
    {
        var strategy = ReadStrategy();

        foreach (var section in new[]
                 {
                     "Structured logs",
                     "Correlation and distributed traces",
                     "Metrics",
                     "Health checks",
                     "Service requirements",
                     "Alert priorities",
                     "Dashboard requirements",
                     "Privacy, security, retention, and cost",
                     "Adoption and verification"
                 })
        {
            Assert.Contains(section, strategy, StringComparison.Ordinal);
        }

        foreach (var rule in new[] { "OBS-001", "OBS-002", "OBS-003", "OBS-004", "OBS-005" })
        {
            Assert.Contains(rule, strategy, StringComparison.Ordinal);
        }

        Assert.Contains("Monitoring Service aggregates privacy-safe operational state", strategy, StringComparison.Ordinal);
        Assert.Contains("cannot create, repair, infer, or override a transaction", strategy, StringComparison.Ordinal);
        Assert.Contains("Each service owns its signals", strategy, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_DefinesSafeLogsTracePropagationAndBoundedMetrics()
    {
        var strategy = ReadStrategy();

        Assert.Contains("source-generated `LoggerMessage`", strategy, StringComparison.Ordinal);
        Assert.Contains("stable numeric `EventId`", strategy, StringComparison.Ordinal);
        Assert.Contains("W3C Trace Context", strategy, StringComparison.Ordinal);
        Assert.Contains("RabbitMQ producers copy `CorrelationId`, `CausationId`, and trace context", strategy, StringComparison.Ordinal);
        Assert.Contains("financial_assistant.<service>.<signal>", strategy, StringComparison.Ordinal);
        Assert.Contains("Every request-serving service implements the RED baseline", strategy, StringComparison.Ordinal);
        Assert.Contains("prohibited metric labels", strategy, StringComparison.Ordinal);
        Assert.Contains("CorrelationId", strategy, StringComparison.Ordinal);
        Assert.Contains("TraceId", strategy, StringComparison.Ordinal);
        Assert.Contains("FailureType", strategy, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_DefinesHealthAlertAndDashboardContracts()
    {
        var strategy = ReadStrategy();

        Assert.Contains("GET /health/live", strategy, StringComparison.Ordinal);
        Assert.Contains("GET /health/ready", strategy, StringComparison.Ordinal);
        Assert.Contains("must not call a database, broker, object store, provider, or another service", strategy, StringComparison.Ordinal);
        Assert.Contains("an unready endpoint returns HTTP 503", strategy, StringComparison.Ordinal);

        foreach (var priority in new[] { "P1 Critical", "P2 High", "P3 Medium", "P4 Low" })
        {
            Assert.Contains(priority, strategy, StringComparison.Ordinal);
        }

        Assert.Contains("p50/p95/p99 latency", strategy, StringComparison.Ordinal);
        Assert.Contains("show no-data separately from zero", strategy, StringComparison.Ordinal);
        Assert.Contains("Every panel declares owner", strategy, StringComparison.Ordinal);
        Assert.Contains("runbook link", strategy, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_CoversEveryBackendComponentAndSensitiveDataBoundary()
    {
        var strategy = ReadStrategy();

        foreach (var owner in new[]
                 {
                     "Public API Gateway",
                     "Identity",
                     "Profile and Category",
                     "Transaction Intake",
                     "Income and Expense",
                     "Receipt Processing",
                     "AI Orchestration",
                     "Financial Summary and Analytics",
                     "Financial Score",
                     "Recommendations and Notifications",
                     "Monitoring",
                     "Audit",
                     "MCP"
                 })
        {
            Assert.Contains(owner, strategy, StringComparison.Ordinal);
        }

        foreach (var forbidden in new[]
                 {
                     "credentials",
                     "personal identities",
                     "financial values",
                     "receipts",
                     "OCR text",
                     "prompts",
                     "completions",
                     "provider payloads"
                 })
        {
            Assert.Contains(forbidden, strategy, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Paid exporters and notification destinations remain disabled", strategy, StringComparison.Ordinal);
        Assert.Contains("Use synthetic identifiers and values", strategy, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_IsDiscoverableFromPlatformAndServiceIndexes()
    {
        var root = FindRepositoryRoot();
        var docs = ReadRequiredFile(root, "docs/README.md");
        var architecture = ReadRequiredFile(root, "docs/architecture/README.md");
        var template = ReadRequiredFile(root, "backend/templates/service-template/README.md");
        var monitoring = ReadRequiredFile(root, "backend/services/monitoring/README.md");
        var logPolicy = ReadRequiredFile(root, "docs/engineering/safe-operational-log-policy.md");

        Assert.Contains("docs/architecture/backend-observability-strategy.md", docs, StringComparison.Ordinal);
        Assert.Contains("docs/architecture/backend-observability-strategy.md", architecture, StringComparison.Ordinal);
        Assert.Contains("backend observability strategy", template, StringComparison.Ordinal);
        Assert.Contains("backend observability strategy", monitoring, StringComparison.Ordinal);
        Assert.Contains("backend observability strategy", logPolicy, StringComparison.Ordinal);
    }

    private static string ReadStrategy()
    {
        var root = FindRepositoryRoot();
        var content = ReadRequiredFile(root, "docs/architecture/backend-observability-strategy.md");
        return string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ReadRequiredFile(string root, string path)
    {
        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-190 file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
