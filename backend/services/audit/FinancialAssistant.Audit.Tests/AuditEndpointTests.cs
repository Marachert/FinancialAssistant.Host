using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class AuditEndpointTests(AuditWebApplicationFactory factory) :
    IClassFixture<AuditWebApplicationFactory>
{
    [Fact]
    public async Task HealthEndpoints_AreAvailable()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task AuditQuery_RequiresTrustedGatewayAndAdminRole()
    {
        using var client = factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync($"{AuditApiRoutes.Dashboard}?correlationId=trace-safe")).StatusCode);

        using var nonAdmin = QueryRequest("trace-safe", "user");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(nonAdmin)).StatusCode);

        using var admin = QueryRequest("trace-safe", "user,admin");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(admin)).StatusCode);
    }

    [Fact]
    public async Task TrustedEvent_IsStoredAndReturnedAsSafeMetadataOnly()
    {
        using var client = factory.CreateClient();
        var envelope = CreateEnvelope("event-audit-endpoint", "trace-audit-endpoint");
        using var ingest = new HttpRequestMessage(HttpMethod.Post, AuditApiRoutes.InternalEvents)
        {
            Content = JsonContent.Create(envelope)
        };
        ingest.Headers.Add(AuditHeaders.ServiceAuthentication, AuditWebApplicationFactory.ServiceSecret);
        var accepted = await client.SendAsync(ingest);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        using var query = QueryRequest(envelope.CorrelationId, "admin");
        var response = await client.SendAsync(query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<AuditQueryResponse>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var record = Assert.Single(Assert.IsType<AuditQueryResponse>(result).Records);
        Assert.Equal(envelope.EventId, record.SourceEventId);
        Assert.Equal(envelope.CorrelationId, record.CorrelationId);
        Assert.Equal("security", record.Domain);
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receiptText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prompt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("financialNote", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicContracts_ContainNoRawPersonalOrFinancialPayloadFields()
    {
        var prohibited = new HashSet<string>(
            ["Email", "Phone", "ReceiptText", "Prompt", "Response", "FinancialNote", "Amount", "Description"],
            StringComparer.OrdinalIgnoreCase);
        var properties = new[] { typeof(AuditEventV1), typeof(AuditRecordResponse) }
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name);
        Assert.DoesNotContain(properties, prohibited.Contains);
    }

    private static HttpRequestMessage QueryRequest(string correlationId, string roles)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AuditApiRoutes.Dashboard}?correlationId={Uri.EscapeDataString(correlationId)}");
        request.Headers.Add(AuditHeaders.GatewayAuthentication, AuditWebApplicationFactory.GatewaySecret);
        request.Headers.Add(AuditHeaders.GatewayRoles, roles);
        return request;
    }

    internal static IntegrationEventEnvelope<AuditEventV1> CreateEnvelope(
        string eventId,
        string correlationId,
        string retentionClass = "security") =>
        new(
            eventId,
            $"occurrence-{eventId}",
            AuditEventTypes.Recorded,
            new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
            "identity-service",
            AuditEventTypes.SchemaVersion,
            correlationId,
            "causation-safe",
            new string('a', 64),
            new AuditEventV1(
                AuditDomains.Security,
                "session.revoked",
                "succeeded",
                "session",
                null,
                retentionClass));
}
