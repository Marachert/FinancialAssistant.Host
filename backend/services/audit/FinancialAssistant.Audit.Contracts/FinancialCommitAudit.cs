using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Contracts;

public static class FinancialCommitAudit
{
    public static IntegrationEventEnvelope<AuditEventV1> Create(IntegrationEventEnvelope<FinancialRecordChangedV1> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var (producer, action, resource) = source.EventType switch
        {
            FinancialRecordEventTypes.IncomeCreated => ("income-service", AuditActions.IncomeCreated, AuditResourceTypes.Income),
            FinancialRecordEventTypes.IncomeUpdated => ("income-service", AuditActions.IncomeUpdated, AuditResourceTypes.Income),
            FinancialRecordEventTypes.IncomeArchived => ("income-service", AuditActions.IncomeArchived, AuditResourceTypes.Income),
            FinancialRecordEventTypes.IncomeRestored => ("income-service", AuditActions.IncomeRestored, AuditResourceTypes.Income),
            FinancialRecordEventTypes.ExpenseCreated => ("expense-service", AuditActions.ExpenseCreated, AuditResourceTypes.Expense),
            FinancialRecordEventTypes.ExpenseUpdated => ("expense-service", AuditActions.ExpenseUpdated, AuditResourceTypes.Expense),
            FinancialRecordEventTypes.ExpenseArchived => ("expense-service", AuditActions.ExpenseArchived, AuditResourceTypes.Expense),
            FinancialRecordEventTypes.ExpenseRestored => ("expense-service", AuditActions.ExpenseRestored, AuditResourceTypes.Expense),
            _ => throw new ArgumentException("Unsupported financial commit metadata.")
        };
        if (source.Producer != producer || source.SchemaVersion != 1 || source.Payload is null ||
            source.UserIdHash is null || source.UserIdHash.Length != 64 ||
            source.UserIdHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        { throw new ArgumentException("Invalid financial commit metadata."); }
        var id = Hash(string.Join('|', "native-financial-commit", producer, source.EventType, source.EventId));
        // This actor records the service's durable commit, not an inferred initiating user.
        var payload = new AuditEventV1(AuditDomains.Business, action, AuditOutcomes.Succeeded, resource,
            null, AuditRetentionClasses.Regulatory, AuditActorTypes.System, null);
        return new IntegrationEventEnvelope<AuditEventV1>(id, id, AuditEventTypes.Recorded, source.OccurredAtUtc,
            producer, AuditEventTypes.SchemaVersion, source.CorrelationId, source.EventId, source.UserIdHash, payload);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
