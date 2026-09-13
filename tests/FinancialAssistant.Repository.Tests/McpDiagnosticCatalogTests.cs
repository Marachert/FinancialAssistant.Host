using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class McpDiagnosticCatalogTests
{
    private const string CatalogPath = "docs/api/mcp-diagnostics-catalog.json";

    [Fact]
    public void Catalog_IsOfflineBoundedAndDoesNotEnableStorageOrProduction()
    {
        using var catalog = JsonDocument.Parse(Read(CatalogPath));
        var root = catalog.RootElement;
        Assert.Equal("FIN-196", root.GetProperty("jira").GetString());
        Assert.Equal("design-only", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("registered").GetBoolean());
        Assert.False(root.GetProperty("directStorageAccess").GetBoolean());
        Assert.Equal(5, root.GetProperty("timeoutSeconds").GetInt32());
        Assert.Equal(16384, root.GetProperty("maximumResponseBytes").GetInt32());
        Assert.Equal(30, root.GetProperty("minimumRepeatSeconds").GetInt32());
        Assert.Equal(1, root.GetProperty("maximumConcurrentCallsPerPrincipal").GetInt32());
        Assert.Equal(0, root.GetProperty("automaticRetries").GetInt32());
        Assert.Equal(new[] { "local", "approved-poc" }, Strings(root.GetProperty("environments")));
        Assert.Equal(new[] { "ok", "no_data", "unavailable", "stale", "denied", "invalid_request", "rate_limited" }, Strings(root.GetProperty("outcomes")));
        Assert.Equal(new[] { "operational_query", "operational_index_inspect", "failed_job_lookup", "audit_lookup" },
            root.GetProperty("actions").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
    }

    [Theory]
    [InlineData("operational_query", "owner-api-required", 20, "catalogKey", "catalogKey,bucketKey,count,component,status,latencyMilliseconds")]
    [InlineData("operational_index_inspect", "owner-api-required", 1, "catalogKey", "catalogKey,owner,configured,aliasResolved,schemaMatches,retentionPolicyMatches,projectionStatus")]
    [InlineData("failed_job_lookup", "monitoring-api-adapter-required", 1, "operationId,kind", "sourceService,kind,state,revision,observedAtUtc,errorCategory")]
    [InlineData("audit_lookup", "audit-api-adapter-required", 20, "correlationId", "occurredAtUtc,producer,action,domain,outcome")]
    public void Actions_AreAdminOnlyReadOnlyWithExactInputAndOutputFields(
        string name, string route, int maximum, string inputs, string outputs)
    {
        using var catalog = JsonDocument.Parse(Read(CatalogPath));
        var action = Action(catalog, name);
        Assert.Equal(new[] { "admin" }, Strings(action.GetProperty("roles")));
        Assert.True(action.GetProperty("readOnly").GetBoolean());
        Assert.Equal(route, action.GetProperty("route").GetString());
        Assert.Equal(maximum, action.GetProperty("maximumItems").GetInt32());
        var schema = action.GetProperty("inputSchema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(inputs.Split(','), Strings(schema.GetProperty("required")));
        Assert.Equal(inputs.Split(','), schema.GetProperty("properties").EnumerateObject().Select(x => x.Name));
        Assert.All(schema.GetProperty("properties").EnumerateObject(),
            x => Assert.Equal("string", x.Value.GetProperty("type").GetString()));
        Assert.Equal(outputs.Split(','), Strings(action.GetProperty("resultFields")));
        Assert.DoesNotContain($"\"{name}\"", Read("backend/services/mcp/FinancialAssistant.Mcp.Contracts/McpContracts.cs"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("operational_query")]
    [InlineData("operational_index_inspect")]
    public void QueryKeys_MatchTheOwnedProjectionCatalog(string name)
    {
        using var catalog = JsonDocument.Parse(Read(CatalogPath));
        using var projections = JsonDocument.Parse(Read("infra/elasticsearch/operations/catalog.json"));
        var action = Action(catalog, name);
        Assert.Equal("../../infra/elasticsearch/operations/catalog.json", action.GetProperty("projectionCatalog").GetString());
        Assert.Equal(projections.RootElement.GetProperty("families").EnumerateArray().Select(x => x.GetProperty("name").GetString()),
            Strings(action.GetProperty("inputSchema").GetProperty("properties").GetProperty("catalogKey").GetProperty("enum")));
    }

    [Fact]
    public void LookupSelectors_AreBoundedAndNeverReturned()
    {
        using var catalog = JsonDocument.Parse(Read(CatalogPath));
        var job = Action(catalog, "failed_job_lookup");
        var properties = job.GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal("uuid", properties.GetProperty("operationId").GetProperty("format").GetString());
        Assert.Equal(36, properties.GetProperty("operationId").GetProperty("maxLength").GetInt32());
        Assert.Equal(new[] { "ai", "ocr", "notification" }, Strings(properties.GetProperty("kind").GetProperty("enum")));
        Assert.DoesNotContain("operationId", Strings(job.GetProperty("resultFields")));
        var audit = Action(catalog, "audit_lookup");
        var correlation = audit.GetProperty("inputSchema").GetProperty("properties").GetProperty("correlationId");
        Assert.Equal(1, correlation.GetProperty("minLength").GetInt32());
        Assert.Equal(128, correlation.GetProperty("maxLength").GetInt32());
        Assert.Equal("^[A-Za-z0-9._:-]+$", correlation.GetProperty("pattern").GetString());
        Assert.DoesNotContain("correlationId", Strings(audit.GetProperty("resultFields")));
    }

    [Fact]
    public void Documentation_PreservesRuntimeGapsAndDiscoverability()
    {
        var doc = Read("docs/engineering/mcp-operational-diagnostics.md");
        foreach (var phrase in new[] { "design-only", "not registered", "zeros do not prove", "never authorization", "process-local", "FIN-205", "16 KiB", "Audit failure", "first-user readiness", "extra spending" })
        {
            Assert.Contains(phrase, doc, StringComparison.Ordinal);
        }

        foreach (var path in new[] { "docs/README.md", "docs/api/README.md", "docs/api/mcp-server-v1.md", "backend/services/mcp/README.md" })
        {
            Assert.Contains("mcp-operational-diagnostics.md", Read(path), StringComparison.Ordinal);
        }
    }

    private static JsonElement Action(JsonDocument catalog, string name) =>
        Assert.Single(catalog.RootElement.GetProperty("actions").EnumerateArray(), x => x.GetProperty("name").GetString() == name);

    private static IEnumerable<string?> Strings(JsonElement array) => array.EnumerateArray().Select(x => x.GetString());

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
