using System.Reflection;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using FinancialAssistant.Mcp.Infrastructure;
using Xunit;

namespace FinancialAssistant.Mcp.Tests;

public sealed class McpApplicationTests
{
    [Fact]
    public void Registry_ContainsOnlySixReadOnlyAllowlistedTools()
    {
        var registry = new McpToolRegistry();

        Assert.Equal(6, registry.All.Count);
        Assert.All(registry.All, tool =>
        {
            Assert.True(tool.IsReadOnly);
            Assert.NotEmpty(tool.AllowedRoles);
            Assert.Equal("aggregate-operational-only", tool.DataClassification);
        });
        Assert.DoesNotContain(
            registry.All,
            tool => tool.Name.Contains("elastic", StringComparison.OrdinalIgnoreCase)
                || tool.Name.Contains("sql", StringComparison.OrdinalIgnoreCase)
                || tool.Name.Contains("query", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<ArgumentException>(() => registry.Get("elasticsearch_query"));
    }

    [Fact]
    public async Task RestrictedTool_DeniesDeveloperAndAuditsAttempt()
    {
        var audit = new InMemoryMcpAuditSink();
        var executor = CreateExecutor(audit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            executor.GetAiCostSummaryAsync(
                Context(McpRoles.Developer),
                CancellationToken.None));

        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal(McpToolNames.AiCostSummary, entry.ToolName);
        Assert.Equal("denied", entry.Outcome);
        Assert.Equal("role", entry.FailureCategory);
    }

    [Fact]
    public async Task AllowedTool_ReturnsAggregateDataAndAuditsSuccess()
    {
        var audit = new InMemoryMcpAuditSink();
        var executor = CreateExecutor(audit);

        var response = await executor.GetParsingQualityAsync(
            Context(McpRoles.QualityAssurance),
            CancellationToken.None);

        Assert.Equal(10, response.ProcessedCount);
        Assert.Equal(80m, response.SuccessPercent);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal(McpToolNames.ParsingQuality, entry.ToolName);
        Assert.Equal("succeeded", entry.Outcome);
        Assert.Null(entry.FailureCategory);
    }

    [Fact]
    public async Task AuditFailure_FailsClosedWithoutSecondAuditAttempt()
    {
        var audit = new FailingAuditSink();
        var executor = CreateExecutor(audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.GetParsingQualityAsync(
                Context(McpRoles.QualityAssurance),
                CancellationToken.None));

        Assert.Equal(1, audit.AttemptCount);
    }

    [Fact]
    public async Task JiraTool_CreatesDraftOnlyAndRejectsControlCharacters()
    {
        var audit = new InMemoryMcpAuditSink();
        var executor = CreateExecutor(audit);

        var draft = await executor.CreateJiraIssueDraftAsync(
            Context(McpRoles.Developer),
            "Add safe diagnostic",
            "Expose one aggregate status",
            CancellationToken.None);

        Assert.True(draft.RequiresHumanSubmission);
        Assert.Equal("FIN", draft.ProjectKey);
        Assert.Contains("mcp-draft", draft.Labels);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            executor.CreateJiraIssueDraftAsync(
                Context(McpRoles.Developer),
                "unsafe\nsummary",
                "goal",
                CancellationToken.None));
    }

    [Fact]
    public async Task ArchitectureLookup_AcceptsExactCatalogAndRejectsArbitrarySearch()
    {
        var executor = CreateExecutor(new InMemoryMcpAuditSink());

        var result = await executor.GetArchitectureReferenceAsync(
            Context(McpRoles.Operator),
            "service-catalog",
            CancellationToken.None);

        Assert.Equal("Service Catalog and Ownership", result.Title);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            executor.GetArchitectureReferenceAsync(
                Context(McpRoles.Operator),
                "SELECT * FROM architecture",
                CancellationToken.None));
    }

    [Fact]
    public void PublicContracts_ContainNoRawSensitivePayloadFields()
    {
        var prohibited = new HashSet<string>(
            ["Email", "Phone", "ReceiptText", "Prompt", "ProviderResponse", "Amount", "FinancialNote"],
            StringComparer.OrdinalIgnoreCase);
        var properties = typeof(McpSystemHealthResponse).Assembly.ExportedTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(property => property.Name);

        Assert.DoesNotContain(properties, prohibited.Contains);
    }

    private static McpToolExecutor CreateExecutor(IMcpAuditSink auditSink) =>
        new(
            new McpToolRegistry(),
            new SyntheticOperationalDataProvider(),
            new McpReferenceDataProvider(Microsoft.Extensions.Options.Options.Create(new McpOptions())),
            auditSink,
            TimeProvider.System);

    private static McpCallContext Context(string role) => new("synthetic-correlation", [role]);

    private sealed class SyntheticOperationalDataProvider : IMcpOperationalDataProvider
    {
        public Task<McpSystemHealthResponse> GetSystemHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new McpSystemHealthResponse(
                "healthy",
                [new McpServiceStatus("audit", "healthy", 3, null)],
                "healthy",
                0,
                "healthy",
                "aggregate-operational-only"));

        public Task<McpAiCostSummaryResponse> GetAiCostSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new McpAiCostSummaryResponse(10, 9, 100, 20, 42, "aggregate-operational-only"));

        public Task<McpParsingQualityResponse> GetParsingQualityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new McpParsingQualityResponse(10, 8, 1, 1, 80m, "aggregate-operational-only"));
    }

    private sealed class FailingAuditSink : IMcpAuditSink
    {
        public int AttemptCount { get; private set; }

        public Task RecordAsync(McpAuditEntry entry, CancellationToken cancellationToken)
        {
            AttemptCount++;
            throw new InvalidOperationException("Synthetic audit failure.");
        }
    }
}
