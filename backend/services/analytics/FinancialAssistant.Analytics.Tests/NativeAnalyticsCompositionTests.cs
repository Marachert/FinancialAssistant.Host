using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class NativeAnalyticsCompositionTests
{
    [Fact]
    public void MissingNativeConfigurationNeverSelectsDeveloperAdapters()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAnalyticsInfrastructure(configuration, true));
        using var provider = new ServiceCollection().AddLogging().AddAnalyticsApplication().AddAnalyticsInfrastructure(configuration).BuildServiceProvider();
        Assert.IsType<InMemoryAnalyticsReadModelStore>(provider.GetRequiredService<IAnalyticsReadModelStore>());
        Assert.IsType<InMemoryAnalyticsDailyLimitProvider>(provider.GetRequiredService<IAnalyticsDailyLimitProvider>());
        Assert.IsType<InMemoryAnalyticsEventPublisher>(provider.GetRequiredService<IAnalyticsEventPublisher>());
    }

    [Fact]
    public async Task NativeCompositionUsesOwnedStateAndNoVolatileOrUnqualifiedTransportFallback()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Analytics:Storage:Postgres:Database"] = "synthetic_analytics",
            ["Analytics:Storage:Postgres:Port"] = "5432",
            ["Analytics:Storage:Postgres:Password"] = new string('x', 40),
            ["Analytics:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ["Analytics:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["Analytics:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()),
            ["Analytics:Events:Mode"] = "RabbitMq"
        }).Build();
        await using var provider = new ServiceCollection().AddLogging().AddAnalyticsApplication().AddAnalyticsInfrastructure(configuration, true).BuildServiceProvider();
        Assert.IsType<PostgresAnalyticsReadModelStore>(provider.GetRequiredService<IAnalyticsReadModelStore>());
        Assert.IsType<PostgresAnalyticsEventPublisher>(provider.GetRequiredService<IAnalyticsEventPublisher>());
        Assert.False(provider.GetRequiredService<IAnalyticsEventPublisher>().ConfirmsDelivery);
        Assert.IsType<PostgresAnalyticsLimitProvider>(provider.GetRequiredService<IAnalyticsDailyLimitProvider>());
        Assert.Same(provider.GetRequiredService<IAnalyticsDailyLimitProvider>(), provider.GetRequiredService<IAnalyticsLimitProvider>());
        Assert.Same(provider.GetRequiredService<AnalyticsPersistenceContext>(), provider.GetRequiredService<IAnalyticsProjectionTransaction>());
        Assert.Null(provider.GetService<InMemoryAnalyticsReadModelStore>());
        Assert.Null(provider.GetService<InMemoryAnalyticsDailyLimitProvider>());
        Assert.Null(provider.GetService<InMemoryAnalyticsEventPublisher>());
        Assert.Null(provider.GetService<RabbitMqAnalyticsEventPublisher>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service => service is AnalyticsFinancialEventConsumer);
        Assert.Contains("Analytics:Storage:Postgres:Password", NativeServiceCatalog.Get("analytics").RequiredSecretKeys);
        Assert.Contains("Analytics:Storage:Postgres:IndexKey", NativeServiceCatalog.Get("analytics").RequiredSecretKeys);
    }

    [Fact]
    public void SettingsSnapshotRequiresExplicitOwnerSourceRevisionAndInitializedUtcObservation()
    {
        var value = new AnalyticsLimitSnapshot(new string('a', 64), "USD", "synthetic-source-v1", 0,
            AnalyticsExpenseLimits.Unconfigured, new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        PostgresAnalyticsLimitProvider.Validate(value);
        Assert.Equal(TimeSpan.FromHours(2), PostgresAnalyticsLimitProvider.MaximumSnapshotAge);
        foreach (var invalid in new[]
        {
            value with { OwnerHash = "invalid" }, value with { Currency = "usd" }, value with { SourceSnapshotVersion = " " },
            value with { SourceSnapshotVersion = new string('x', 129) }, value with { SourceSnapshotVersion = "v1\n" },
            value with { SourceRevision = -1 }, value with { SourceRevision = long.MaxValue },
            value with { Limits = new AnalyticsExpenseLimits(0, null, null) },
            value with { Limits = new AnalyticsExpenseLimits(null, -1, null) },
            value with { Limits = new AnalyticsExpenseLimits(null, null, 0) },
            value with { ObservedAtUtc = default }, value with { ObservedAtUtc = value.ObservedAtUtc.ToOffset(TimeSpan.FromHours(1)) }
        })
        { Assert.Throws<ArgumentException>(() => PostgresAnalyticsLimitProvider.Validate(invalid)); }
    }
}
