using System.Net;
using System.Text;
using FinancialAssistant.Monitoring.Contracts;
using FinancialAssistant.Monitoring.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialAssistant.Monitoring.Tests;

public sealed class HttpMonitoringDependencyProbeTests
{
    [Fact]
    public async Task ProbeAsync_MapsOnlySafeHealthAndAggregateDependencyFields()
    {
        var handler = new RoutingHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath;
            return path switch
            {
                "/health/ready" => Json(HttpStatusCode.OK, "{\"status\":\"ready\",\"raw\":\"ignored\"}"),
                "/api/overview" => Json(
                    HttpStatusCode.OK,
                    "{\"queue_totals\":{\"messages\":7},\"object_totals\":{\"consumers\":3},\"raw\":\"ignored\"}"),
                "/_cluster/health" => Json(
                    HttpStatusCode.OK,
                    "{\"status\":\"yellow\",\"number_of_nodes\":2,\"active_shards\":9,\"cluster_name\":\"ignored\"}"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
        var probe = CreateProbe(
            handler,
            new MonitoringOptions
            {
                Services =
                [
                    new MonitoringServiceTargetOptions
                    {
                        Name = "analytics",
                        BaseAddress = "http://analytics.internal"
                    }
                ],
                RabbitMq = new MonitoringRabbitMqOptions
                {
                    ManagementBaseAddress = "http://rabbitmq.internal",
                    Username = "synthetic-user",
                    Password = "synthetic-password"
                },
                Elasticsearch = new MonitoringElasticsearchOptions
                {
                    BaseAddress = "http://elasticsearch.internal"
                }
            });

        var snapshot = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(MonitoringStatuses.Healthy, Assert.Single(snapshot.Services).Status);
        Assert.Equal(MonitoringStatuses.Healthy, snapshot.RabbitMq.Status);
        Assert.Equal(7, snapshot.RabbitMq.QueueDepth);
        Assert.Equal(3, snapshot.RabbitMq.ConsumerCount);
        Assert.Equal(MonitoringStatuses.Degraded, snapshot.Elasticsearch.Status);
        Assert.Equal("yellow", snapshot.Elasticsearch.ClusterStatus);
        Assert.Equal(2, snapshot.Elasticsearch.NodeCount);
        Assert.Equal(9, snapshot.Elasticsearch.ActiveShardCount);
    }

    [Fact]
    public async Task ProbeAsync_FailsSafelyWithoutReturningTransportOrResponseDetails()
    {
        var handler = new RoutingHandler(request =>
            request.RequestUri?.AbsolutePath == "/_cluster/health"
                ? Json(HttpStatusCode.OK, "not-json")
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var probe = CreateProbe(
            handler,
            new MonitoringOptions
            {
                Services =
                [
                    new MonitoringServiceTargetOptions
                    {
                        Name = "analytics",
                        BaseAddress = "http://analytics.internal"
                    }
                ],
                RabbitMq = new MonitoringRabbitMqOptions
                {
                    ManagementBaseAddress = "http://rabbitmq.internal"
                },
                Elasticsearch = new MonitoringElasticsearchOptions
                {
                    BaseAddress = "http://elasticsearch.internal"
                }
            });

        var snapshot = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal("http_status", Assert.Single(snapshot.Services).ErrorCategory);
        Assert.Equal(MonitoringStatuses.NotConfigured, snapshot.RabbitMq.Status);
        Assert.Equal("not_configured", snapshot.RabbitMq.ErrorCategory);
        Assert.Equal(MonitoringStatuses.Unavailable, snapshot.Elasticsearch.Status);
        Assert.Equal("invalid_response", snapshot.Elasticsearch.ErrorCategory);
    }

    private static HttpMonitoringDependencyProbe CreateProbe(
        HttpMessageHandler handler,
        MonitoringOptions options) =>
        new(new HttpClient(handler), Options.Create(options), TimeProvider.System);

    private static HttpResponseMessage Json(HttpStatusCode status, string content) =>
        new(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private sealed class RoutingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
