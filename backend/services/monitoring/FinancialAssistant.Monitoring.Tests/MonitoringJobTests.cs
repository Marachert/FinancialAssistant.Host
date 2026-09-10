using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Contracts;
using Xunit;

namespace FinancialAssistant.Monitoring.Tests;

public sealed class MonitoringJobTests
{
    [Theory]
    [InlineData("unknown", "running", null)]
    [InlineData("ocr", "failed", "raw-provider-response")]
    [InlineData("ocr", "running", "timeout")]
    [InlineData("ocr", "failed", null)]
    [InlineData("ai", "running", null)]
    public void Store_RejectsUnsafeOrMismatchedSignal(string kind, string state, string? error)
    {
        var store = CreateStore(new Clock());
        Assert.Throws<ArgumentException>(() => store.Record(Signal() with
        {
            Kind = kind,
            State = state,
            ErrorCategory = error
        }));
        Assert.Empty(store.GetSnapshot().Jobs);
    }

    [Fact]
    public void Store_IsBoundedExpiresAndPreservesRevisionIdempotency()
    {
        var clock = new Clock();
        var store = CreateStore(clock);
        var signal = Signal();
        store.Record(signal);
        clock.Now = clock.Now.AddMinutes(1);
        store.Record(signal);
        Assert.Equal(clock.Now.AddMinutes(-1), Assert.Single(store.GetSnapshot().Jobs).ObservedAtUtc);
        Assert.Throws<InvalidOperationException>(() => store.Record(signal with { State = "succeeded" }));
        store.Record(signal with { Revision = 2, State = "succeeded" });
        store.Record(signal);
        Assert.Equal("succeeded", Assert.Single(store.GetSnapshot().Jobs).State);
        for (var i = 0; i < 210; i++)
        {
            clock.Now = clock.Now.AddSeconds(1);
            store.Record(Signal());
        }

        Assert.Equal(MonitoringJobStore.Capacity, store.GetSnapshot().Jobs.Count);
        clock.Now = clock.Now.AddHours(24);
        Assert.Empty(store.GetSnapshot().Jobs);
    }

    [Fact]
    public async Task Jobs_RequireAdminAndSignalsRequireServiceAuthentication()
    {
        using var factory = new MonitoringWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(MonitoringApiRoutes.Jobs)).StatusCode);
        client.DefaultRequestHeaders.Add(MonitoringHeaders.GatewayAuthentication,
            MonitoringWebApplicationFactory.GatewaySecret);
        client.DefaultRequestHeaders.Add(MonitoringHeaders.GatewayRoles, "user");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MonitoringApiRoutes.Jobs)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync(MonitoringApiRoutes.JobSignals, Signal())).StatusCode);
        client.DefaultRequestHeaders.Add(MonitoringHeaders.SignalAuthentication,
            MonitoringWebApplicationFactory.SignalSecret);
        var signal = Signal();
        Assert.Equal(HttpStatusCode.Accepted,
            (await client.PostAsJsonAsync(MonitoringApiRoutes.JobSignals, signal)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(MonitoringApiRoutes.JobSignals,
                signal with { State = "succeeded" })).StatusCode);
        client.DefaultRequestHeaders.Remove(MonitoringHeaders.GatewayRoles);
        client.DefaultRequestHeaders.Add(MonitoringHeaders.GatewayRoles, "admin");
        using var response = await client.GetAsync(MonitoringApiRoutes.Jobs);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<MonitoringJobsResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Jobs);
        Assert.Equal("bounded-operational-metadata-only", body.DataClassification);
    }

    private static MonitoringJobSignalRequest Signal() =>
        new(Guid.NewGuid(), "receipt-processing", "ocr", "running", 1, null);

    private static MonitoringJobStore CreateStore(TimeProvider clock) =>
        new(new MonitoringSignalPolicy(["receipt-processing"], ["dashboard"]), clock);

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
