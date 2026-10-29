using System.Text.Json;
using System.Text.Json.Nodes;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class NativeInsightPersistenceTests
{
    private static readonly string Owner = new('a', 64);

    [Fact]
    public void MissingNativeSettingsCannotFallBackToMemory()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRecommendationNotificationInfrastructure(new ConfigurationBuilder().Build(), true));
    }

    [Fact]
    public async Task NativeRegistrationUsesOwnedAdaptersAndNoLegacyConsumer()
    {
        var values = new Dictionary<string, string?>
        {
            ["RecommendationsNotifications:Storage:Postgres:Database"] = "synthetic_insights",
            ["RecommendationsNotifications:Storage:Postgres:Port"] = "5432",
            ["RecommendationsNotifications:Storage:Postgres:Password"] = new string('s', 48),
            ["RecommendationsNotifications:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)51, 32).ToArray()),
            ["RecommendationsNotifications:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["RecommendationsNotifications:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)53, 32).ToArray()),
            ["RecommendationsNotifications:Events:Mode"] = "RabbitMq"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRecommendationNotificationApplication();
        services.AddRecommendationNotificationInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true);
        await using var provider = services.BuildServiceProvider();
        Assert.IsType<PostgresRecommendationNotificationStore>(provider.GetRequiredService<IRecommendationNotificationStore>());
        Assert.IsType<InsightPersistenceContext>(provider.GetRequiredService<IInsightProcessingTransaction>());
        Assert.IsType<PostgresNotificationPreferenceProvider>(provider.GetRequiredService<INotificationPreferenceProvider>());
        Assert.IsType<PostgresRecommendationProfileSettingsProvider>(provider.GetRequiredService<IRecommendationProfileSettingsProvider>());
        Assert.False(provider.GetRequiredService<INotificationEventPublisher>().ConfirmsDelivery);
        Assert.Equal("recommendations-notifications", provider.GetRequiredService<OwnedPostgresStore>().ServiceId);
        Assert.Null(provider.GetService<InMemoryRecommendationNotificationStore>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), s => s is RecommendationNotificationEventConsumer);
    }

    [Fact]
    public void OwnedStateRequiresFieldsVersionAndMatchingOwner()
    {
        var state = InsightState.Empty(Owner);
        var bytes = InsightPersistenceValidation.Encode(state);
        Assert.Equal(Owner, InsightPersistenceValidation.Decode(new DurableRecord(0, bytes), Owner).Owner);
        Assert.Throws<StorageUnavailableException>(() => InsightPersistenceValidation.Decode(new DurableRecord(0, bytes), new string('b', 64)));
        var missing = JsonNode.Parse(bytes)!;
        Assert.True(missing["value"]!.AsObject().Remove("preferences"));
        Assert.Throws<StorageUnavailableException>(() => InsightPersistenceValidation.Decode(new DurableRecord(0, JsonSerializer.SerializeToUtf8Bytes(missing)), Owner));
        var unknown = JsonNode.Parse(bytes)!;
        unknown["value"]!["undeclared"] = true;
        Assert.Throws<StorageUnavailableException>(() => InsightPersistenceValidation.Decode(new DurableRecord(0, JsonSerializer.SerializeToUtf8Bytes(unknown)), Owner));
    }

    [Fact]
    public void SourceCapacityFailsClosedWithoutRemovingReplayEvidence()
    {
        var state = InsightState.Empty(Owner);
        for (var i = 0; i < 1000; i++) { state.Receipts.Add(new InsightReceiptState("analytics", "synthetic-source-" + i, new string('A', 64), [])); }
        Assert.NotEmpty(InsightPersistenceValidation.Encode(state));
        state.Receipts.Add(new InsightReceiptState("analytics", "synthetic-source-1000", new string('B', 64), []));
        Assert.Throws<StorageUnavailableException>(() => InsightPersistenceValidation.Encode(state));
        Assert.Equal(1001, state.Receipts.Count);
    }

    [Fact]
    public void PreferencesAndProfileInputsRemainExplicitAndBounded()
    {
        InsightPersistenceValidation.Preferences(new NotificationPreferences(false, true, new NotificationQuietHours(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC"), []));
        Assert.Throws<ArgumentException>(() => InsightPersistenceValidation.Preferences(new NotificationPreferences(true, true, DisabledNotificationTypes: ["unknown"])));
        Assert.Throws<ArgumentException>(() => InsightPersistenceValidation.Preferences(new NotificationPreferences(true, true, new NotificationQuietHours(new TimeOnly(22, 0), new TimeOnly(22, 0), "UTC"))));
        var at = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var snapshot = new RecommendationProfileSnapshot(Owner, "USD", "synthetic-v1", 0, false, null, at);
        PostgresRecommendationProfileSettingsProvider.Validate(snapshot);
        Assert.Throws<ArgumentException>(() => PostgresRecommendationProfileSettingsProvider.Validate(snapshot with { MonthlyBudget = 0m }));
        Assert.Throws<ArgumentException>(() => PostgresRecommendationProfileSettingsProvider.Validate(snapshot with { SourceRevision = -1 }));
    }

    [Fact]
    public async Task StagingPublicationCannotMarkNotificationDeliveredOrPublished()
    {
        var store = new InMemoryRecommendationNotificationStore();
        var publisher = new StagingPublisher();
        var service = new NotificationPreparationService(store, new NotificationTemplateCatalog(), publisher);
        var at = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var source = new IntegrationEventEnvelope<RecommendationGeneratedV1>("synthetic-source", "synthetic-occurrence",
            RecommendationEventTypes.RecommendationGenerated, at, "synthetic-producer", 1, "synthetic-correlation", "synthetic-cause", Owner,
            new RecommendationGeneratedV1("synthetic-recommendation", "USD", "steady-course", "information", "Synthetic title", "Synthetic body", [], at));
        Assert.Equal(2, (await service.ProcessAsync(source, default)).Count);
        Assert.Equal(2, (await service.ProcessAsync(source, default)).Count);
        Assert.Equal(4, publisher.Count);
        Assert.All(await store.GetNotificationsAsync(Owner, "USD", default), n => Assert.Equal("prepared", n.DeliveryStatus));
    }

    private sealed class StagingPublisher : INotificationEventPublisher
    {
        public bool ConfirmsDelivery => false;
        public int Count { get; private set; }
        public Task PublishAsync(IntegrationEventEnvelope<NotificationPreparedV1> envelope, CancellationToken cancellationToken)
        { Count++; return Task.CompletedTask; }
    }
}
