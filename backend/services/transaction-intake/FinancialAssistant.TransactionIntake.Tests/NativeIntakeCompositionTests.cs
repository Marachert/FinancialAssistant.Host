using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class NativeIntakeCompositionTests
{
    [Fact]
    public void MissingNativeConfigurationDoesNotFallBack()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTransactionIntakeInfrastructure(windowsNative: true));
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddTransactionIntakeInfrastructure(new ConfigurationBuilder().Build(), true));
        using var development = new ServiceCollection().AddTransactionIntakeInfrastructure().BuildServiceProvider();
        Assert.IsType<InMemoryTransactionDraftStore>(development.GetRequiredService<ITransactionDraftStore>());
        Assert.IsType<InMemoryTransactionConfirmedPublisher>(development.GetRequiredService<ITransactionConfirmedPublisher>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("25")]
    public void SourceRetentionMustBeExplicitAndBounded(string? hours)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddTransactionIntakeInfrastructure(Configuration(hours), true));
    }

    [Fact]
    public async Task NativeOwnsWorkflowAndPendingOutboxWithoutFinancialConsumers()
    {
        await using var provider = Provider();
        Assert.IsType<PostgresTransactionDraftStore>(provider.GetRequiredService<ITransactionDraftStore>());
        Assert.IsType<PostgresTransactionDraftCreationStore>(provider.GetRequiredService<ITransactionDraftCreationStore>());
        Assert.IsType<PostgresTransactionConfirmationStore>(provider.GetRequiredService<ITransactionConfirmationStore>());
        Assert.IsType<PostgresIntakeOutbox>(provider.GetRequiredService<ITransactionDraftCreatedPublisher>());
        Assert.IsType<PostgresIntakeOutbox>(provider.GetRequiredService<ITransactionConfirmedPublisher>());
        Assert.IsType<IntakePersistenceContext>(provider.GetRequiredService<ITransactionIntakeMutationScope>());
        Assert.Empty(provider.GetServices<ITransactionConfirmedConsumer>());
        Assert.Null(provider.GetService<InMemoryTransactionConfirmedPublisher>());
        Assert.Null(provider.GetService<InMemoryTransactionDraftCreatedPublisher>());
        Assert.EndsWith("TransactionalConfirmationService", provider.GetRequiredService<ITransactionConfirmationService>().GetType().FullName);
        Assert.Contains("TransactionIntake:Storage:Postgres:Password", NativeServiceCatalog.Get("transaction-intake").RequiredSecretKeys);
        Assert.Contains("TransactionIntake:Storage:Postgres:IndexKey", NativeServiceCatalog.Get("transaction-intake").RequiredSecretKeys);
    }

    [Fact]
    public async Task InvalidRequestsAreRejectedBeforeDatabaseAccess()
    {
        await using var provider = Provider();
        var intake = provider.GetRequiredService<ITransactionIntakeService>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => intake.CreateDraftAsync("owner", "synthetic-key", null!, default));
        await Assert.ThrowsAsync<ArgumentException>(() => intake.CreateDraftAsync("owner", "synthetic-key",
            new TransactionIntakeRequest("synthetic\0input"), default));
        var review = provider.GetRequiredService<ITransactionDraftReviewService>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => review.UpdateAsync("owner", "draft", null!, default));
        await Assert.ThrowsAsync<ArgumentException>(() => review.UpdateAsync("owner", "draft",
            new TransactionDraftUpdateRequest(0, "income", 1m, "USD", "income.salary", null,
                DateOnly.FromDateTime(DateTime.UtcNow), "unsafe\0note"), default));
        await Assert.ThrowsAsync<ArgumentException>(() => review.RejectAsync("owner", "", default));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetRequiredService<ITransactionConfirmationService>()
            .ConfirmAsync("owner", "draft", "unsafe\0correlation", default));
    }

    internal static ServiceProvider Provider() => new ServiceCollection().AddLogging().AddTransactionIntakeApplication()
        .AddTransactionIntakeInfrastructure(Configuration("12"), true).BuildServiceProvider();

    [Fact]
    public async Task LongDraftIdentityKeepsDefaultCorrelationWithinSharedEventBound()
    {
        var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        var draftId = new string('d', 200);
        var drafts = new InMemoryTransactionDraftStore();
        var publisher = new InMemoryTransactionConfirmedPublisher(Array.Empty<ITransactionConfirmedConsumer>());
        var draft = new TransactionDraft(draftId, "synthetic-owner", "SYNTHETICFINGERPRINT", "income", 1m, "USD",
            "income.salary", null, new DateOnly(2026, 10, 12), 1m, Array.Empty<string>(), false,
            new TransactionDraftSuggestionMetadata(TransactionDraftSuggestionSources.AiNaturalLanguage, null, 1m,
                Array.Empty<string>(), Array.Empty<string>(), "Ready."), now);
        await drafts.StoreIfMissingAsync(draft.UserId, "synthetic-key", draft.InputFingerprint, draft, default);
        var service = new TransactionConfirmationService(drafts, new InMemoryTransactionConfirmationStore(), publisher,
            new FixedClock(now), new GuidTransactionConfirmationIdGenerator());
        await service.ConfirmAsync(draft.UserId, draft.Id, null, default);
        Assert.InRange(Assert.Single(publisher.PublishedEvents).CorrelationId.Length, 1, 128);
    }

    private sealed class FixedClock(DateTimeOffset now) : ITransactionIntakeClock
    { public DateTimeOffset UtcNow => now; }

    private static IConfiguration Configuration(string? hours) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["TransactionIntake:Storage:SourcePayloadRetentionHours"] = hours,
        ["TransactionIntake:Storage:Postgres:Database"] = "synthetic",
        ["TransactionIntake:Storage:Postgres:Port"] = "5432",
        ["TransactionIntake:Storage:Postgres:Password"] = new string('x', 40),
        ["TransactionIntake:Storage:Postgres:IndexKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
        ["TransactionIntake:Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
        ["TransactionIntake:Storage:Postgres:EncryptionKeys:v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
    }).Build();
}
