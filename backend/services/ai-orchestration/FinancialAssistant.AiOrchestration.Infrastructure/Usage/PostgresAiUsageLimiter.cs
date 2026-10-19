using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Usage;

public sealed class PostgresAiUsageLimiter : IAiUsageLimiter
{
    private readonly OwnedPostgresStore database;

    public PostgresAiUsageLimiter(OwnedPostgresStore database)
    {
        if (database.ServiceId != "ai-orchestration") { throw new ArgumentException("AI usage requires its owned storage role."); }
        this.database = database;
    }

    public bool TryAcquire(string usageSubjectId, string providerName, DateOnly utcDate, int dailyLimit) =>
        throw new InvalidOperationException("Durable usage requires asynchronous acquisition and a stable request identity.");

    public Task<bool> TryAcquireAsync(string usageSubjectId, string providerName, DateOnly utcDate, int dailyLimit,
        string requestId, CancellationToken token)
    {
        OwnedUsageReservations.Validate(usageSubjectId, providerName, utcDate, dailyLimit, requestId);
        return database.ExecuteAsync(new[] { "usage:" + usageSubjectId }, (records, _) =>
            OwnedUsageReservations.TryAcquireAsync(records, usageSubjectId, providerName, utcDate, dailyLimit, requestId), token);
    }
}
