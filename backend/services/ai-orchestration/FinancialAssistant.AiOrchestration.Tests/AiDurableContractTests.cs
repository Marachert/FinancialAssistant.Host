using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;
using FinancialAssistant.AiOrchestration.Infrastructure.Usage;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiDurableContractTests
{
    [Fact]
    public async Task AiAdaptersCannotUseReceiptStorageRole()
    {
        using var keys = new RecordProtection(new byte[32], "v1", new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
        await using var database = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("receipt-processing"), "synthetic", 65432, new string('x', 32)), keys);
        Assert.Throws<ArgumentException>(() => new PostgresAiCallMetadataStore(database));
        Assert.Throws<ArgumentException>(() => new PostgresAiUsageLimiter(database));
        Assert.Throws<ArgumentException>(() => new PostgresAiExecutionJournal(database));
    }

    [Fact]
    public void MissingPersistenceCannotSilentlyChooseMemory()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAiOrchestrationInfrastructure(windowsNative: true));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["AiOrchestration:Provider:Enabled"] = "true" }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddAiOrchestrationInfrastructure(config, true));
        Assert.Contains("Live AI", error.Message);
    }

    [Fact]
    public void NativeCompositionUsesOwnedJournalQuotaAndMetadata()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiOrchestration:Storage:Postgres:Database"] = "synthetic",
            ["AiOrchestration:Storage:Postgres:Port"] = "65432",
            ["AiOrchestration:Storage:Postgres:Password"] = new string('x', 32),
            ["AiOrchestration:Storage:Postgres:IndexKey"] = Convert.ToBase64String(new byte[32]),
            ["AiOrchestration:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            ["AiOrchestration:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        }).Build();
        var services = new ServiceCollection().AddAiOrchestrationInfrastructure(config, true);
        Assert.Equal(typeof(PostgresAiUsageLimiter), Assert.Single(services, s => s.ServiceType == typeof(IAiUsageLimiter)).ImplementationType);
        Assert.Equal(typeof(DurableAiOrchestrationService), Assert.Single(services, s => s.ServiceType == typeof(IAiOrchestrationService)).ImplementationType);
        Assert.DoesNotContain(services, s => s.ImplementationType == typeof(InMemoryAiCallMetadataStore));
    }

    [Fact]
    public void UnsafeProviderDetailsAndInvalidTokenCountsCannotEnterMetadata()
    {
        var now = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        var value = new AiCallMetadata("call_synthetic", "transaction-parse", "transaction-parse", 1, "synthetic", "model", AiCallStatus.Succeeded,
            new AiTokenUsage(10, 2), null, null, now, now, new AiProviderUsageMetadata(100, 1, "2026-10"));
        PostgresAiCallMetadataStore.Validate(value);
        Assert.Throws<ArgumentException>(() => PostgresAiCallMetadataStore.Validate(value with { FailureCategory = "secret provider detail" }));
        Assert.Throws<ArgumentException>(() => PostgresAiCallMetadataStore.Validate(value with { TokenUsage = new AiTokenUsage(-1, 0) }));
        Assert.Throws<ArgumentException>(() => PostgresAiCallMetadataStore.Validate(value with { Provider = "https://provider?secret" }));
    }
}
