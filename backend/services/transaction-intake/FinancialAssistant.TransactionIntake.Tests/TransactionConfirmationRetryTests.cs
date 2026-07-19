using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionConfirmationRetryTests
{
    [Fact]
    public async Task Confirm_WhenFirstPublishFails_RetryPublishesStoredEvent()
    {
        const string userId = "synthetic-confirm-retry-user";
        const string draftId = "draft_synthetic_confirm_retry";
        var draftStore = new InMemoryTransactionDraftStore();
        var confirmationStore = new InMemoryTransactionConfirmationStore();
        var publisher = new FailOncePublisher();
        var clock = new FixedClock();
        var service = new TransactionConfirmationService(
            draftStore,
            confirmationStore,
            publisher,
            clock,
            new FixedIdGenerator());
        var draft = new TransactionDraft(
            draftId,
            userId,
            "SYNTHETICFINGERPRINT",
            TransactionTypes.Income,
            100,
            "USD",
            "income.salary",
            null,
            new DateOnly(2026, 7, 19),
            0.95m,
            Array.Empty<string>(),
            RequiresReview: false,
            clock.UtcNow);
        await draftStore.StoreIfMissingAsync(
            userId,
            "synthetic-confirm-retry-key",
            draft.InputFingerprint,
            draft,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmAsync(userId, draftId, null, CancellationToken.None));
        var result = await service.ConfirmAsync(userId, draftId, null, CancellationToken.None);
        var stored = await confirmationStore.GetAsync(userId, draftId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Replayed);
        Assert.Equal("txn_synthetic_confirm_retry", result.Transaction.TransactionId);
        Assert.Equal(2, publisher.Attempts);
        Assert.Single(publisher.PublishedEvents);
        Assert.NotNull(stored);
        Assert.True(stored.Published);
    }

    [Fact]
    public async Task Confirm_WhenRequestCancelsAfterPublish_MarksEventPublishedWithoutRedelivery()
    {
        const string userId = "synthetic-confirm-cancel-user";
        const string draftId = "draft_synthetic_confirm_cancel";
        var cancellation = new CancellationTokenSource();
        var draftStore = new InMemoryTransactionDraftStore();
        var confirmationStore = new InMemoryTransactionConfirmationStore();
        var publisher = new CancelAfterPublishPublisher(cancellation);
        var clock = new FixedClock();
        var service = new TransactionConfirmationService(
            draftStore,
            confirmationStore,
            publisher,
            clock,
            new FixedIdGenerator());
        var draft = CreateConfirmableDraft(draftId, userId, clock.UtcNow);
        await draftStore.StoreIfMissingAsync(
            userId,
            "synthetic-confirm-cancel-key",
            draft.InputFingerprint,
            draft,
            CancellationToken.None);

        var first = await service.ConfirmAsync(userId, draftId, null, cancellation.Token);
        var replay = await service.ConfirmAsync(userId, draftId, null, CancellationToken.None);
        var stored = await confirmationStore.GetAsync(userId, draftId, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(1, publisher.Attempts);
        Assert.NotNull(stored);
        Assert.True(stored.Published);
    }

    private sealed class FailOncePublisher : ITransactionConfirmedPublisher
    {
        private readonly List<TransactionConfirmedIntegrationEvent> publishedEvents = new();

        public int Attempts { get; private set; }

        public IReadOnlyCollection<TransactionConfirmedIntegrationEvent> PublishedEvents => publishedEvents;

        public Task PublishAsync(
            TransactionConfirmedIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            if (Attempts == 1)
            {
                throw new InvalidOperationException("Synthetic transient publisher failure.");
            }

            publishedEvents.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class CancelAfterPublishPublisher : ITransactionConfirmedPublisher
    {
        private readonly CancellationTokenSource cancellation;

        public CancelAfterPublishPublisher(CancellationTokenSource cancellation)
        {
            this.cancellation = cancellation;
        }

        public int Attempts { get; private set; }

        public Task PublishAsync(
            TransactionConfirmedIntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            cancellation.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : ITransactionIntakeClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedIdGenerator : ITransactionConfirmationIdGenerator
    {
        public string CreateTransactionId() => "txn_synthetic_confirm_retry";

        public string CreateEventId() => "event_synthetic_confirm_retry";
    }

    private static TransactionDraft CreateConfirmableDraft(
        string draftId,
        string userId,
        DateTimeOffset createdAtUtc) =>
        new(
            draftId,
            userId,
            "SYNTHETICFINGERPRINT",
            TransactionTypes.Income,
            100,
            "USD",
            "income.salary",
            null,
            new DateOnly(2026, 7, 19),
            0.95m,
            Array.Empty<string>(),
            RequiresReview: false,
            createdAtUtc);
}
