using System.Net.Http.Json;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Mcp.Infrastructure;

public sealed class InMemoryMcpAuditSink : IMcpAuditSink
{
    private readonly object sync = new();
    private readonly List<McpAuditEntry> entries = [];

    public Task RecordAsync(McpAuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<McpAuditEntry> Snapshot()
    {
        lock (sync)
        {
            return entries.ToArray();
        }
    }
}

public sealed class HttpMcpAuditSink(
    IHttpClientFactory httpClientFactory,
    IOptions<McpOptions> options) : IMcpAuditSink
{
    public async Task RecordAsync(McpAuditEntry entry, CancellationToken cancellationToken)
    {
        var value = options.Value.Audit;
        if (!Uri.TryCreate(value.BaseAddress, UriKind.Absolute, out var baseAddress)
            || string.IsNullOrWhiteSpace(value.SharedSecret))
        {
            throw new InvalidOperationException("The central Audit Service adapter is not configured.");
        }

        var eventId = $"mcp-{Guid.NewGuid():N}";
        var envelope = new IntegrationEventEnvelope<AuditEventV1>(
            eventId,
            $"occurrence-{eventId}",
            AuditEventTypes.Recorded,
            entry.OccurredAtUtc,
            "mcp-service",
            AuditEventTypes.SchemaVersion,
            entry.CorrelationId,
            eventId,
            null,
            new AuditEventV1(
                AuditDomains.Mcp,
                $"tool.{entry.ToolName}",
                entry.Outcome,
                "mcp-tool",
                entry.FailureCategory,
                AuditRetentionClasses.Standard,
                AuditActorTypes.Service));
        using var request = new HttpRequestMessage(HttpMethod.Post, AuditApiRoutes.InternalEvents)
        {
            Content = JsonContent.Create(envelope)
        };
        request.Headers.Add(AuditHeaders.ServiceAuthentication, value.SharedSecret);
        var client = httpClientFactory.CreateClient(DependencyInjection.AuditClientName);
        client.BaseAddress = baseAddress;
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
