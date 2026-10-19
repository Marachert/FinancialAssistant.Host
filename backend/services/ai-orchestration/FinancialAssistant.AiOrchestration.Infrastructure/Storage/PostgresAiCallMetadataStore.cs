using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Storage;

public sealed class PostgresAiCallMetadataStore
    : IAiCallMetadataStore
{
    private readonly OwnedPostgresStore database;

    public PostgresAiCallMetadataStore(OwnedPostgresStore database)
    {
        if (database.ServiceId != "ai-orchestration") { throw new ArgumentException("AI metadata requires its owned storage role."); }
        this.database = database;
    }

    public Task AddAsync(AiCallMetadata value, CancellationToken token)
        => throw new InvalidOperationException("Native AI metadata requires an explicit owned usage subject.");

    public Task AddAsync(string owner, AiCallMetadata value, CancellationToken token)
    {
        Identifier(owner);
        Validate(value);
        return database.ExecuteAsync(new[] { "call:" + owner + ":" + value.CallId }, async (records, _) =>
        {
            var durable = await records.GetAsync("ai_calls", owner, value.CallId);
            var encoded = OwnedRecordCodec.Encode(1, value);
            if (durable is not null)
            {
                Validate(OwnedRecordCodec.Decode<AiCallMetadata>(1, durable));
                if (!durable.Payload.AsSpan().SequenceEqual(encoded)) { throw new StorageConflictException(); }
                return true;
            }
            await records.InsertAsync("ai_calls", owner, value.CallId, encoded); return true;
        }, token);
    }

    public Task<AiCallMetadata?> GetAsync(string owner, string callId, CancellationToken token = default)
    {
        Identifier(owner); Identifier(callId);
        return database.ExecuteAsync<AiCallMetadata?>(new[] { "call:" + owner + ":" + callId }, async (records, _) =>
        {
            var durable = await records.GetAsync("ai_calls", owner, callId);
            if (durable is null) { return null; }
            var value = OwnedRecordCodec.Decode<AiCallMetadata>(1, durable);
            try { Validate(value); } catch (ArgumentException) { throw new StorageUnavailableException(); }
            if (value.CallId != callId) { throw new StorageUnavailableException(); }
            return value;
        }, token);
    }

    public static void Validate(AiCallMetadata value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.CallId); Identifier(value.CapabilityName); Identifier(value.PromptName);
        Identifier(value.Provider); Identifier(value.Model);
        if (!Enum.IsDefined(value.Status) || value.PromptVersion < 1 || value.Confidence is < 0 or > 1 ||
            value.StartedAtUtc == default || value.StartedAtUtc.Offset != TimeSpan.Zero ||
            value.CompletedAtUtc.Offset != TimeSpan.Zero || value.CompletedAtUtc < value.StartedAtUtc ||
            value.TokenUsage is { InputTokens: < 0 } or { OutputTokens: < 0 } || value.ProviderUsage is null ||
            value.ProviderUsage.RequestCharacters is < 1 or > 1_000_000 || value.ProviderUsage.ProviderRequestUnits is < 0 or > 1 ||
            value.ProviderUsage.BillingMonth != value.StartedAtUtc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) ||
            value.FailureCategory is not (null or "provider_request_too_large" or "daily_usage_limit_exceeded" or "cancelled" or
                "provider_failure" or "provider_timeout" or "provider_unavailable" or "invalid_provider_response" or
                "invalid_token_usage" or "structured_output_validation_failed"))
        { throw new ArgumentException("Invalid privacy-safe AI call metadata."); }
    }

    private static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(c =>
            !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid AI metadata identifier."); }
    }
}
