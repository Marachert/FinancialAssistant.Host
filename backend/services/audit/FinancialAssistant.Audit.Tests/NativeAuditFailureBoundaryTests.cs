using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class NativeAuditFailureBoundaryTests
{
    [Fact]
    public async Task NativeUnavailableAppendAndQueryReturnSafeNoStore503WithoutMemoryFallback()
    {
        await using var factory = new NativeFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AuditHeaders.ServiceAuthentication, AuditWebApplicationFactory.ServiceSecret);
        var source = new IntegrationEventEnvelope<AuditEventV1>("synthetic-native-source", "synthetic-native-occurrence",
            AuditEventTypes.Recorded, DateTimeOffset.UtcNow.AddMinutes(-1), "expense-service", 1, "synthetic-correlation", "synthetic-cause", null,
            new AuditEventV1("business", "expense.created", "succeeded", "expense", null, "regulatory"));
        using var append = await client.PostAsJsonAsync(AuditApiRoutes.InternalEvents, source);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, append.StatusCode);
        Assert.True(append.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("Postgres", await append.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Add(AuditHeaders.GatewayAuthentication, AuditWebApplicationFactory.GatewaySecret);
        client.DefaultRequestHeaders.Add(AuditHeaders.GatewayRoles, "admin");
        using var query = await client.GetAsync(AuditApiRoutes.Dashboard + "?correlationId=synthetic-correlation");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, query.StatusCode);
        Assert.True(query.Headers.CacheControl?.NoStore);
        Assert.Equal(2, factory.Unavailable.Calls);
    }

    [Fact]
    public async Task NativeUntrustedOrNonAdminRequestsNeverReachStorage()
    {
        await using var factory = new NativeFactory(); using var client = factory.CreateClient();
        using var untrusted = await client.GetAsync(AuditApiRoutes.Dashboard + "?correlationId=synthetic-correlation");
        Assert.Equal(HttpStatusCode.Unauthorized, untrusted.StatusCode);
        client.DefaultRequestHeaders.Add(AuditHeaders.GatewayAuthentication, AuditWebApplicationFactory.GatewaySecret);
        client.DefaultRequestHeaders.Add(AuditHeaders.GatewayRoles, "user");
        using var forbidden = await client.GetAsync(AuditApiRoutes.Dashboard + "?correlationId=synthetic-correlation");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(0, factory.Unavailable.Calls);
    }

    private sealed class NativeFactory : WebApplicationFactory<Program>
    {
        public UnavailableStore Unavailable { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(FinancialAssistantHost.NativeEnvironment);
            var values = NativeAuditPersistenceTests.Configuration();
            values["Audit:Gateway:SharedSecret"] = AuditWebApplicationFactory.GatewaySecret;
            values["Audit:Services:SharedSecret"] = AuditWebApplicationFactory.ServiceSecret;
            foreach (var pair in values)
            { if (pair.Value is not null) { builder.UseSetting(pair.Key, pair.Value); } }
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IAuditRecordStore>(); services.RemoveAll<IAuditEventTransaction>();
                services.AddSingleton<IAuditRecordStore>(Unavailable);
                services.AddSingleton<IAuditEventTransaction>(Unavailable);
            });
        }
    }
    private sealed class UnavailableStore : IAuditRecordStore, IAuditEventTransaction
    {
        public int Calls { get; private set; }
        public Task<string> ExecuteAsync(IntegrationEventEnvelope<AuditEventV1> source,
            Func<CancellationToken, Task<string>> operation, CancellationToken token)
        { Calls++; throw new StorageUnavailableException(); }
        public Task<AuditAppendResult> AppendAsync(AuditRecord record, CancellationToken token)
        { Calls++; throw new StorageUnavailableException(); }
        public Task<IReadOnlyList<AuditRecord>> FindByCorrelationAsync(string correlationId, DateTimeOffset asOfUtc, CancellationToken token)
        { Calls++; throw new StorageUnavailableException(); }
    }
}
