using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Expense.Infrastructure;

public sealed class InMemoryExpenseRecordEventPublisher : IExpenseRecordEventPublisher
{
    private readonly ConcurrentDictionary<string, IntegrationEventEnvelope<FinancialRecordChangedV1>>
        outbox = new(StringComparer.Ordinal);

    public IReadOnlyCollection<IntegrationEventEnvelope<FinancialRecordChangedV1>> Published =>
        outbox.Values.OrderBy(message => message.OccurredAtUtc).ToArray();

    public Task PublishAsync(
        string eventType,
        ExpenseRecord record,
        string correlationId,
        string causationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var eventId = HashIdentifier($"{eventType}|{record.TransactionId}|{record.Revision}");
        var changedAtUtc = (record.UpdatedAtUtc ?? record.ConfirmedAtUtc).ToUniversalTime();
        var envelope = new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            eventId,
            eventId,
            eventType,
            changedAtUtc,
            "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            correlationId,
            causationId,
            HashIdentifier(record.UserId),
            new FinancialRecordChangedV1(
                record.TransactionId,
                record.Amount,
                record.Currency,
                record.CategoryId,
                record.Date,
                record.Status,
                record.Revision,
                record.Origin,
                changedAtUtc));

        outbox.TryAdd(eventId, envelope);
        return Task.CompletedTask;
    }

    private static string HashIdentifier(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
