using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class OperationalIndexDashboardTests
{
    private const string AssetRoot = "infra/elasticsearch/operations/";

    [Theory]
    [InlineData("application-logs", "public-api-gateway", "application-logs.json")]
    [InlineData("event-summaries", "audit", "audit-events.json")]
    [InlineData("ai-ocr-job-observations", "monitoring", "ai-ocr-jobs.json")]
    [InlineData("service-health", "monitoring", "service-health.json")]
    public void QueryContract_IsOwnedBoundedAndUsesOnlyDeclaredFields(string family, string owner, string file)
    {
        using var catalog = JsonDocument.Parse(Read(AssetRoot + "catalog.json"));
        var entries = catalog.RootElement.GetProperty("families").EnumerateArray().ToArray();
        Assert.Equal(4, entries.Length);
        Assert.Equal("design-only", catalog.RootElement.GetProperty("status").GetString());
        Assert.Equal(30, catalog.RootElement.GetProperty("refreshMinimumSeconds").GetInt32());
        var entry = Assert.Single(entries, x => x.GetProperty("name").GetString() == family);
        Assert.Equal(owner, entry.GetProperty("owner").GetString());
        Assert.Equal($"fa-local-{owner}-{family}-read", entry.GetProperty("readAlias").GetString());
        Assert.Equal(file, entry.GetProperty("query").GetString());
        var fields = entry.GetProperty("fields").EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var field in entry.GetProperty("fields").EnumerateObject())
        {
            Assert.Contains(field.Value.GetString(), new[] { "date", "keyword", "integer", "long", "double" });
        }

        using var query = JsonDocument.Parse(Read(AssetRoot + file));
        var root = query.RootElement;
        Assert.Equal(0, root.GetProperty("size").GetInt32());
        Assert.False(root.GetProperty("_source").GetBoolean());
        Assert.False(root.GetProperty("track_total_hits").GetBoolean());
        Assert.Equal("2s", root.GetProperty("timeout").GetString());
        Assert.Contains("\"lte\": \"now\"", root.GetProperty("query").GetRawText(), StringComparison.Ordinal);
        Assert.Contains("\"gte\": \"now-", root.GetProperty("query").GetRawText(), StringComparison.Ordinal);
        if (family != "service-health")
        {
            Assert.DoesNotContain("\"top_hits\"", root.GetRawText(), StringComparison.Ordinal);
        }

        VerifyFieldsAndOperators(root, fields);
    }

    [Fact]
    public void AuditProjection_ExcludesExpiredAndIdentifyingRecords()
    {
        using var query = JsonDocument.Parse(Read(AssetRoot + "audit-events.json"));
        var filters = query.RootElement.GetProperty("query").GetProperty("bool").GetProperty("filter");
        Assert.Equal("now", filters[1].GetProperty("range").GetProperty("expiresAtUtc").GetProperty("gt").GetString());
        var catalog = Read(AssetRoot + "catalog.json");
        foreach (var forbidden in new[] { "actorIdHash", "subjectIdHash", "correlationId", "receiptId", "operationId", "amount", "payload", "message", "prompt" })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", catalog, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void JobQuery_CountsOnlyFailedAiOcrObservations()
    {
        using var query = JsonDocument.Parse(Read(AssetRoot + "ai-ocr-jobs.json"));
        var root = query.RootElement;
        var filters = root.GetProperty("query").GetProperty("bool").GetProperty("filter");
        Assert.Equal("failed", filters[1].GetProperty("term").GetProperty("state").GetString());
        Assert.Equal(new[] { "ai", "ocr" }, filters[2].GetProperty("terms").GetProperty("kind").EnumerateArray().Select(x => x.GetString()));
        var categories = root.GetProperty("aggs").GetProperty("kinds").GetProperty("aggs")
            .GetProperty("categories").GetProperty("filters").GetProperty("filters");
        Assert.Equal(new[] { "timeout", "transport", "provider_unavailable", "invalid_result", "policy_rejected" }, categories.EnumerateObject().Select(x => x.Name));
    }

    [Fact]
    public void HealthQuery_SelectsOneLatestSampleWithAnExactSourceAllowlist()
    {
        using var query = JsonDocument.Parse(Read(AssetRoot + "service-health.json"));
        var root = query.RootElement;
        var filters = root.GetProperty("query").GetProperty("bool").GetProperty("filter");
        Assert.Equal("identity-service", filters[1].GetProperty("term").GetProperty("component").GetString());
        Assert.Equal("now-5m", filters[0].GetProperty("range").GetProperty("observedAtUtc").GetProperty("gte").GetString());
        var latest = root.GetProperty("aggs").GetProperty("selectedComponent").GetProperty("aggs").GetProperty("latest").GetProperty("top_hits");
        Assert.Equal(1, latest.GetProperty("size").GetInt32());
        Assert.Equal("desc", latest.GetProperty("sort")[0].GetProperty("observedAtUtc").GetProperty("order").GetString());
        Assert.Equal(new[] { "observedAtUtc", "component", "status", "latencyMilliseconds" }, latest.GetProperty("_source").GetProperty("includes").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Documentation_SeparatesDesignFromDeploymentAndExplainsFailureSemantics()
    {
        var doc = Read("docs/engineering/operational-indices-and-dashboards.md");
        foreach (var phrase in new[] { "not deployed", "dynamic: strict", "PostgreSQL", "allow_partial_search_results=false", "timed_out", "unique job totals", "not distinct", "physical erasure", "FIN-205" })
        {
            Assert.Contains(phrase, doc, StringComparison.Ordinal);
        }

        Assert.Contains("operational-indices-and-dashboards.md", Read("docs/README.md"), StringComparison.Ordinal);
        Assert.Contains("operational-indices-and-dashboards.md", Read("infra/README.md"), StringComparison.Ordinal);
    }

    private static void VerifyFieldsAndOperators(JsonElement node, HashSet<string> fields)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                VerifyFieldsAndOperators(child, fields);
            }
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, new[] { "script", "script_fields", "runtime_mappings", "query_string", "wildcard" });
                if (property.Name == "field")
                {
                    Assert.Contains(property.Value.GetString()!, fields);
                }
                else if (property.Name is "term" or "terms" or "range")
                {
                    foreach (var field in property.Value.EnumerateObject())
                    {
                        Assert.Contains(field.Name, fields);
                    }
                }

                VerifyFieldsAndOperators(property.Value, fields);
            }
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
