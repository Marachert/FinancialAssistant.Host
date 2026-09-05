using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Contracts;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Monitoring.Infrastructure;

public sealed class HttpMonitoringDependencyProbe(
    HttpClient httpClient,
    IOptions<MonitoringOptions> options,
    TimeProvider timeProvider) : IMonitoringDependencyProbe
{
    private readonly MonitoringOptions options = options.Value;

    public async Task<MonitoringProbeSnapshot> ProbeAsync(CancellationToken cancellationToken)
    {
        var servicesTask = Task.WhenAll(
            options.Services.Select(target => ProbeServiceAsync(target, cancellationToken)));
        var rabbitMqTask = ProbeRabbitMqAsync(cancellationToken);
        var elasticsearchTask = ProbeElasticsearchAsync(cancellationToken);
        await Task.WhenAll(servicesTask, rabbitMqTask, elasticsearchTask);
        var services = await servicesTask;
        var rabbitMq = await rabbitMqTask;
        var elasticsearch = await elasticsearchTask;
        return new MonitoringProbeSnapshot(services, rabbitMq, elasticsearch);
    }

    private async Task<MonitoringServiceProbe> ProbeServiceAsync(
        MonitoringServiceTargetOptions target,
        CancellationToken cancellationToken)
    {
        var checkedAt = timeProvider.GetUtcNow();
        if (!TryCreateUri(target.BaseAddress, "/health/ready", out var uri))
        {
            return new MonitoringServiceProbe(
                target.Name,
                MonitoringStatuses.NotConfigured,
                0,
                checkedAt,
                "not_configured");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return new MonitoringServiceProbe(
                    target.Name,
                    MonitoringStatuses.Unavailable,
                    stopwatch.ElapsedMilliseconds,
                    checkedAt,
                    "http_status");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var status = document.RootElement.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()?.Trim().ToLowerInvariant()
                : null;
            if (status is not (MonitoringStatuses.Healthy
                or MonitoringStatuses.Degraded
                or MonitoringStatuses.Unavailable))
            {
                return new MonitoringServiceProbe(
                    target.Name,
                    MonitoringStatuses.Unavailable,
                    stopwatch.ElapsedMilliseconds,
                    checkedAt,
                    "invalid_response");
            }

            return new MonitoringServiceProbe(
                target.Name,
                status,
                stopwatch.ElapsedMilliseconds,
                checkedAt,
                null);
        }
        catch (JsonException)
        {
            return new MonitoringServiceProbe(
                target.Name,
                MonitoringStatuses.Unavailable,
                stopwatch.ElapsedMilliseconds,
                checkedAt,
                "invalid_response");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new MonitoringServiceProbe(
                target.Name,
                MonitoringStatuses.Unavailable,
                stopwatch.ElapsedMilliseconds,
                checkedAt,
                "timeout");
        }
        catch (HttpRequestException)
        {
            return new MonitoringServiceProbe(
                target.Name,
                MonitoringStatuses.Unavailable,
                stopwatch.ElapsedMilliseconds,
                checkedAt,
                "transport");
        }
    }

    private async Task<MonitoringRabbitMqProbe> ProbeRabbitMqAsync(
        CancellationToken cancellationToken)
    {
        var checkedAt = timeProvider.GetUtcNow();
        var configuration = options.RabbitMq;
        if (!TryCreateUri(configuration.ManagementBaseAddress, "/api/overview", out var uri)
            || string.IsNullOrWhiteSpace(configuration.Username)
            || string.IsNullOrWhiteSpace(configuration.Password))
        {
            return new MonitoringRabbitMqProbe(
                MonitoringStatuses.NotConfigured,
                0,
                0,
                0,
                checkedAt,
                "not_configured");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var credential = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.Username}:{configuration.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return RabbitMqFailure(stopwatch.ElapsedMilliseconds, checkedAt, "http_status");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var queueDepth = ReadInt64(root, "queue_totals", "messages");
            var consumerCount = ReadInt32(root, "object_totals", "consumers");
            return new MonitoringRabbitMqProbe(
                MonitoringStatuses.Healthy,
                stopwatch.ElapsedMilliseconds,
                queueDepth,
                consumerCount,
                checkedAt,
                null);
        }
        catch (JsonException)
        {
            return RabbitMqFailure(stopwatch.ElapsedMilliseconds, checkedAt, "invalid_response");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RabbitMqFailure(stopwatch.ElapsedMilliseconds, checkedAt, "timeout");
        }
        catch (HttpRequestException)
        {
            return RabbitMqFailure(stopwatch.ElapsedMilliseconds, checkedAt, "transport");
        }
    }

    private async Task<MonitoringElasticsearchProbe> ProbeElasticsearchAsync(
        CancellationToken cancellationToken)
    {
        var checkedAt = timeProvider.GetUtcNow();
        if (!TryCreateUri(options.Elasticsearch.BaseAddress, "/_cluster/health", out var uri))
        {
            return ElasticsearchFailure(0, checkedAt, "not_configured", MonitoringStatuses.NotConfigured);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            stopwatch.Stop();
            if (!response.IsSuccessStatusCode)
            {
                return ElasticsearchFailure(stopwatch.ElapsedMilliseconds, checkedAt, "http_status");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var clusterStatus = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()?.Trim().ToLowerInvariant() ?? "unknown"
                : "unknown";
            var status = clusterStatus switch
            {
                "green" => MonitoringStatuses.Healthy,
                "yellow" => MonitoringStatuses.Degraded,
                _ => MonitoringStatuses.Unavailable
            };
            return new MonitoringElasticsearchProbe(
                status,
                stopwatch.ElapsedMilliseconds,
                clusterStatus,
                ReadInt32(root, "number_of_nodes"),
                ReadInt32(root, "active_shards"),
                checkedAt,
                clusterStatus is "green" or "yellow" ? null : "cluster_status");
        }
        catch (JsonException)
        {
            return ElasticsearchFailure(stopwatch.ElapsedMilliseconds, checkedAt, "invalid_response");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ElasticsearchFailure(stopwatch.ElapsedMilliseconds, checkedAt, "timeout");
        }
        catch (HttpRequestException)
        {
            return ElasticsearchFailure(stopwatch.ElapsedMilliseconds, checkedAt, "transport");
        }
    }

    private static MonitoringRabbitMqProbe RabbitMqFailure(
        long latencyMilliseconds,
        DateTimeOffset checkedAt,
        string errorCategory) =>
        new(
            MonitoringStatuses.Unavailable,
            latencyMilliseconds,
            0,
            0,
            checkedAt,
            errorCategory);

    private static MonitoringElasticsearchProbe ElasticsearchFailure(
        long latencyMilliseconds,
        DateTimeOffset checkedAt,
        string errorCategory,
        string status = MonitoringStatuses.Unavailable) =>
        new(status, latencyMilliseconds, "unknown", 0, 0, checkedAt, errorCategory);

    private static bool TryCreateUri(string baseAddress, string path, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var baseUri)
            || !Uri.TryCreate(baseUri, path, out var targetUri))
        {
            return false;
        }

        uri = targetUri;
        return true;
    }

    private static int ReadInt32(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static int ReadInt32(JsonElement root, string parent, string property) =>
        root.TryGetProperty(parent, out var parentElement)
            ? ReadInt32(parentElement, property)
            : 0;

    private static long ReadInt64(JsonElement root, string parent, string property) =>
        root.TryGetProperty(parent, out var parentElement)
        && parentElement.TryGetProperty(property, out var value)
        && value.TryGetInt64(out var result)
            ? result
            : 0;
}
