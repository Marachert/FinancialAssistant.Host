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
}
