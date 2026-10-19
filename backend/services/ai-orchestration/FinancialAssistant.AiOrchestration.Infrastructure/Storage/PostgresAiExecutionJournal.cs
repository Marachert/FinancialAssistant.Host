using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Storage;

public sealed class PostgresAiExecutionJournal
{
    private readonly OwnedPostgresStore database;
    public PostgresAiExecutionJournal(OwnedPostgresStore database)
    {
        if (database.ServiceId != "ai-orchestration") { throw new ArgumentException("AI requests require their owned storage role."); }
        this.database = database;
    }

    public Task ReserveAsync(AiCapabilityRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        OwnedUsageReservations.Validate(request.UsageSubjectId, "ai-request", DateOnly.FromDateTime(DateTime.UtcNow), 1, request.RequestId);
        if (string.IsNullOrWhiteSpace(request.Input) || request.Input.Length > 100_000 ||
            string.IsNullOrWhiteSpace(request.CapabilityName) || request.CapabilityName.Length > 200 ||
            string.IsNullOrWhiteSpace(request.PromptName) || request.PromptName.Length > 200 || request.PromptVersion is < 1)
        { throw new ArgumentException("Invalid native AI request."); }
        // Only an encrypted digest is retained, never the input, prompt template or response.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { request.CapabilityName, request.PromptName, request.PromptVersion, request.Input });
        string fingerprint;
        try { fingerprint = Convert.ToHexString(SHA256.HashData(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        return database.ExecuteAsync(new[] { "request:" + request.UsageSubjectId }, async (records, _) =>
        {
            var existing = await records.GetAsync("ai_requests", request.UsageSubjectId, request.RequestId);
            if (existing is not null)
            {
                var value = Decode(existing);
                if (value.Fingerprint != fingerprint) { throw new StorageConflictException(); }
                throw new AiRequestAlreadyRecordedException(value.CallId, value.State);
            }
            await records.InsertAsync("ai_requests", request.UsageSubjectId, request.RequestId,
                OwnedRecordCodec.Encode(1, new Entry(fingerprint, "reserved", null)));
            return true;
        }, token);
    }

    public Task CompleteAsync(string owner, string requestId, string? callId, CancellationToken token = default)
    {
        OwnedUsageReservations.Validate(owner, "ai-request", DateOnly.FromDateTime(DateTime.UtcNow), 1, requestId);
        if (callId is not null) { OwnedUsageReservations.Validate(owner, "ai-request", DateOnly.FromDateTime(DateTime.UtcNow), 1, callId); }
        return database.ExecuteAsync(new[] { "request:" + owner }, async (records, _) =>
        {
            var durable = await records.GetAsync("ai_requests", owner, requestId) ?? throw new StorageUnavailableException();
            var value = Decode(durable);
            var state = callId is null ? "failed" : "completed";
            if (value.State != "reserved")
            {
                if (value.State != state || value.CallId != callId) { throw new StorageConflictException(); }
                return true;
            }
            if (callId is not null && await records.GetAsync("ai_calls", owner, callId) is null) { throw new StorageUnavailableException(); }
            await records.ReplaceAsync("ai_requests", owner, requestId, durable.Revision,
                OwnedRecordCodec.Encode(1, value with { State = state, CallId = callId }));
            return true;
        }, token);
    }

    private static Entry Decode(DurableRecord durable)
    {
        var value = OwnedRecordCodec.Decode<Entry>(1, durable);
        if (value.Fingerprint is not { Length: 64 } || !value.Fingerprint.All(char.IsAsciiHexDigit) ||
            value.State is not ("reserved" or "completed" or "failed") || (value.State == "completed") != (value.CallId is not null))
        { throw new StorageUnavailableException(); }
        return value;
    }

    private sealed record Entry([property: JsonRequired] string Fingerprint, [property: JsonRequired] string State,
        [property: JsonRequired] string? CallId);
}

public sealed class DurableAiOrchestrationService(PostgresAiExecutionJournal journal, AiOrchestrationService service) : IAiOrchestrationService
{
    public async Task<AiCapabilityResult> ExecuteAsync(AiCapabilityRequest request, CancellationToken token)
    {
        await journal.ReserveAsync(request, token);
        AiCapabilityResult result;
        try { result = await service.ExecuteAsync(request, token); }
        catch
        {
            // A reserved/uncertain attempt is never automatically charged again after a crash.
            await journal.CompleteAsync(request.UsageSubjectId, request.RequestId, null, CancellationToken.None);
            throw;
        }
        await journal.CompleteAsync(request.UsageSubjectId, request.RequestId, result.CallId, CancellationToken.None);
        return result;
    }
}
