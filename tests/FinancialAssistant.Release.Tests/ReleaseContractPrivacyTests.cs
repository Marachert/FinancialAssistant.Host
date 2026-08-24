using System.Net;
using System.Reflection;
using System.Text.Json;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Tests;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.FinancialScore.Contracts;
using FinancialAssistant.FinancialScore.Tests;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Infrastructure.Storage;
using FinancialAssistant.Identity.Tests;
using FinancialAssistant.Mcp.Contracts;
using FinancialAssistant.Monitoring.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Tests;
using Xunit;

namespace FinancialAssistant.Release.Tests;

public sealed class ReleaseContractPrivacyTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CriticalServices_PublishExpectedOpenApiRoutes()
    {
        using var identity = new IdentityContractWebApplicationFactory();
        using var intake = new TransactionIntakeWebApplicationFactory();
        using var analytics = new AnalyticsWebApplicationFactory();
        using var score = new FinancialScoreWebApplicationFactory();

        await AssertOpenApiAsync(
            identity.CreateClient(),
            IdentityApiRoutes.Register,
            IdentityApiRoutes.SignIn);
        await AssertOpenApiAsync(
            intake.CreateClient(),
            TransactionIntakeApiRoutes.Intake,
            TransactionIntakeApiRoutes.ConfirmDraft);
        await AssertOpenApiAsync(
            analytics.CreateClient(),
            AnalyticsApiRoutes.Dashboard,
            AnalyticsApiRoutes.CategoryBreakdown);
        await AssertOpenApiAsync(
            score.CreateClient(),
            FinancialScoreApiRoutes.Current,
            FinancialScoreApiRoutes.History);
    }

    [Fact]
    public void FinancialEventSchema_IsVersionedAndRoundTripsRequiredEnvelopeFields()
    {
        var occurredAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var envelope = new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            "release-event-1",
            "release-occurrence-1",
            FinancialRecordEventTypes.ExpenseCreated,
            occurredAt,
            "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "release-correlation",
            "release-causation",
            "synthetic-owner-hash",
            new FinancialRecordChangedV1(
                "release-record-1",
                10m,
                "USD",
                "expense.food",
                new DateOnly(2026, 8, 24),
                "active",
                0,
                "confirmed_transaction",
                occurredAt));

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        foreach (var property in new[]
                 {
                     "eventId",
                     "occurrenceId",
                     "eventType",
                     "occurredAtUtc",
                     "producer",
                     "schemaVersion",
                     "correlationId",
                     "causationId",
                     "userIdHash",
                     "payload"
                 })
        {
            Assert.True(root.TryGetProperty(property, out _), property);
        }

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.EndsWith(".v1", root.GetProperty("eventType").GetString());
        Assert.NotNull(
            JsonSerializer.Deserialize<IntegrationEventEnvelope<FinancialRecordChangedV1>>(
                json,
                JsonOptions));
    }

    [Fact]
    public void ElasticsearchCatalog_UsesVersionedPhysicalIndicesAndDistinctAliases()
    {
        var definitions = IdentityIndexCatalog.Create("release-test");

        Assert.Equal(4, definitions.Count);
        Assert.All(definitions, definition =>
        {
            Assert.Contains("-v1-000001", definition.PhysicalIndex);
            Assert.EndsWith("-read", definition.ReadAlias);
            Assert.EndsWith("-write", definition.WriteAlias);
            Assert.Equal(1, definition.SchemaVersion);
        });
        Assert.Equal(
            definitions.Count,
            definitions.Select(item => item.ReadAlias).Distinct().Count());
        Assert.Equal(
            definitions.Count,
            definitions.Select(item => item.WriteAlias).Distinct().Count());
    }

    [Fact]
    public void OperationalContracts_ExposeNoRawPersonalOrFinancialPayloadFields()
    {
        var prohibited = new HashSet<string>(
            [
                "Email",
                "Phone",
                "Password",
                "Secret",
                "Token",
                "ReceiptText",
                "Prompt",
                "ProviderResponse",
                "Merchant",
                "FinancialNote",
                "RawPayload"
            ],
            StringComparer.OrdinalIgnoreCase);
        var assemblies = new[]
        {
            typeof(MonitoringDashboardResponse).Assembly,
            typeof(AuditEventV1).Assembly,
            typeof(McpSystemHealthResponse).Assembly
        };
        var publicProperties = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.ExportedTypes)
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(publicProperties, prohibited.Contains);
    }

    [Fact]
    public void StructuredOperationalLogs_HaveNoSensitiveMessageTemplateFields()
    {
        var root = FindRepositoryRoot();
        foreach (var relativePath in new[]
                 {
                     "backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/Observability/GatewayOperationalLog.cs",
                     "backend/services/identity/FinancialAssistant.Identity.Infrastructure/Observability/IdentityOperationalLog.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(root, relativePath));
            foreach (var field in new[]
                     {
                         "{Email",
                         "{Phone",
                         "{Password",
                         "{Token",
                         "{Receipt",
                         "{Prompt",
                         "{Amount",
                         "{Merchant",
                         "{Note"
                     })
            {
                Assert.DoesNotContain(field, source, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static async Task AssertOpenApiAsync(HttpClient client, params string[] routes)
    {
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var route in routes)
        {
            Assert.True(paths.TryGetProperty(route, out _), route);
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("FinancialAssistant.Backend.sln was not found.");
    }
}
