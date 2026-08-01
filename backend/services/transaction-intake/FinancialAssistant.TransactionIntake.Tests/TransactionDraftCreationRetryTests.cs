using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionDraftCreationRetryTests
{
    [Fact]
    public async Task CreateDraft_WhenFirstPublishFails_RetryPublishesStoredEvent()
    {
        const string userId = "synthetic-draft-event-retry-user";
        var draftStore = new InMemoryTransactionDraftStore();
        var creationStore = new InMemoryTransactionDraftCreationStore();
        var publisher = new FailOncePublisher();
        var service = new TransactionIntakeService(
            new FixedParser(),
            draftStore,
            creationStore,
            publisher,
            new FixedClock(),
            new FixedDraftIdGenerator(),
            new TransactionDraftValidator());
        var request = new TransactionIntakeRequest("Paid 20 USD for taxi today");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftAsync(
                userId,
                "synthetic-draft-event-retry-key",
                request,
                CancellationToken.None));
        var result = await service.CreateDraftAsync(
            userId,
            "synthetic-draft-event-retry-key",
            request,
            CancellationToken.None);
        var stored = await creationStore.GetByReferenceAsync(
            userId,
            "draft-payload-draft_synthetic_event_retry",
            CancellationToken.None);

        Assert.True(result.Replayed);
        Assert.Equal("draft_synthetic_event_retry", result.Draft.Id);
        Assert.Equal(2, publisher.Attempts);
        Assert.Single(publisher.PublishedEvents);
        Assert.NotNull(stored);
        Assert.True(stored.Published);
        Assert.Equal(request.Input, stored.NormalizedInput);
    }

    private sealed class FixedParser : ITransactionInputParser
    {
        public Task<ParsedTransactionCandidate> ParseAsync(
            string normalizedInput,
            DateOnly currentDate,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new ParsedTransactionCandidate(
                    TransactionTypes.Expense,
                    20,
                    "USD",
                    "expense.transport",
                    "Taxi",
                    currentDate,
                    0.95m));
        }
    }

    private sealed class FailOncePublisher : ITransactionDraftCreatedPublisher
    {
        private readonly List<TransactionDraftCreatedIntegrationEvent> publishedEvents = new();

        public int Attempts { get; private set; }

        public IReadOnlyCollection<TransactionDraftCreatedIntegrationEvent> PublishedEvents =>
            publishedEvents;

        public Task PublishAsync(
            TransactionDraftCreatedIntegrationEvent integrationEvent,
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
            new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedDraftIdGenerator : ITransactionDraftIdGenerator
    {
        public string Create() => "draft_synthetic_event_retry";
    }
}
