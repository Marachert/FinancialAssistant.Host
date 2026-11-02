using System.Text.Json;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class FinancialCommitAuditTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Source(string type, string producer) => new(
        "synthetic-financial-source", "synthetic-financial-source", type, At, producer, 1, "synthetic-correlation", "synthetic-command",
        new string('a', 64), new FinancialRecordChangedV1("synthetic-private-record", 1234.56m, "USD", "synthetic-private-category",
            new DateOnly(2026, 10, 29), "active", 1, "manual", At));

    [Theory]
    [InlineData(FinancialRecordEventTypes.IncomeCreated, "income-service", AuditActions.IncomeCreated)]
    [InlineData(FinancialRecordEventTypes.IncomeUpdated, "income-service", AuditActions.IncomeUpdated)]
    [InlineData(FinancialRecordEventTypes.IncomeArchived, "income-service", AuditActions.IncomeArchived)]
    [InlineData(FinancialRecordEventTypes.IncomeRestored, "income-service", AuditActions.IncomeRestored)]
    [InlineData(FinancialRecordEventTypes.ExpenseCreated, "expense-service", AuditActions.ExpenseCreated)]
    [InlineData(FinancialRecordEventTypes.ExpenseUpdated, "expense-service", AuditActions.ExpenseUpdated)]
    [InlineData(FinancialRecordEventTypes.ExpenseArchived, "expense-service", AuditActions.ExpenseArchived)]
    [InlineData(FinancialRecordEventTypes.ExpenseRestored, "expense-service", AuditActions.ExpenseRestored)]
    public void ExistingCatalogAllowsMinimizedSystemCommitWithoutInventingAnOriginalUser(string type, string producer, string action)
    {
        var source = Source(type, producer); var audit = FinancialCommitAudit.Create(source);
        new AuditPolicy(new[] { producer }, new Dictionary<string, int> { [AuditRetentionClasses.Regulatory] = 10 }).Validate(audit.Payload, producer);
        Assert.Equal(FinancialCommitAudit.Create(source), audit);
        Assert.Equal(AuditActorTypes.System, audit.Payload.ActorType); Assert.Null(audit.Payload.ActorIdHash);
        Assert.Equal(action, audit.Payload.Action);
        Assert.Equal(AuditEventTypes.Recorded, audit.EventType); Assert.Equal(source.EventId, audit.CausationId);
        Assert.Equal(source.UserIdHash, audit.UserIdHash); Assert.Equal(source.CorrelationId, audit.CorrelationId);
        Assert.NotEqual(source.EventId, audit.EventId); Assert.Equal(source.OccurredAtUtc, audit.OccurredAtUtc);
        var json = JsonSerializer.Serialize(audit, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var forbidden in new[] { "1234.56", "synthetic-private-record", "synthetic-private-category", "USD", "\"amount\"", "\"currency\"", "\"categoryId\"", "\"merchant\"" })
        { Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase); }
    }

    [Fact]
    public void ForeignProducerAndUnsupportedActionsCannotMasqueradeAsFinancialCommits()
    {
        Assert.Throws<ArgumentException>(() => FinancialCommitAudit.Create(Source(FinancialRecordEventTypes.IncomeCreated, "expense-service")));
        Assert.Throws<ArgumentException>(() => FinancialCommitAudit.Create(Source("transaction.confirmed.v1", "transaction-intake-service")));
    }
}
