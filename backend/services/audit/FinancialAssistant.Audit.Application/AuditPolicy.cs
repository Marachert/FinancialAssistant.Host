using FinancialAssistant.Audit.Contracts;

namespace FinancialAssistant.Audit.Application;

public sealed class AuditPolicy(
    IEnumerable<string> allowedProducers,
    IReadOnlyDictionary<string, int> retentionDays)
{
    private static readonly HashSet<string> AllowedDomains = new(
        [AuditDomains.Security, AuditDomains.Business, AuditDomains.Ai, AuditDomains.Admin, AuditDomains.Mcp],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedOutcomes = new(
        [
            AuditOutcomes.Succeeded,
            AuditOutcomes.Failed,
            AuditOutcomes.Denied,
            AuditOutcomes.Accepted
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedActorTypes = new(
        [
            AuditActorTypes.Anonymous,
            AuditActorTypes.User,
            AuditActorTypes.Admin,
            AuditActorTypes.Service,
            AuditActorTypes.System
        ],
        StringComparer.Ordinal);
    private readonly HashSet<string> allowedProducers = new(
        allowedProducers.Select(Normalize),
        StringComparer.Ordinal);
    private readonly Dictionary<string, int> retentionDays = retentionDays
        .ToDictionary(item => Normalize(item.Key), item => item.Value, StringComparer.Ordinal);

    public void Validate(AuditEventV1 payload, string producer)
    {
        var normalizedProducer = Normalize(producer);
        var normalizedDomain = Normalize(payload.Domain);
        var normalizedOutcome = Normalize(payload.Outcome);
        var normalizedRetentionClass = Normalize(payload.RetentionClass);
        var normalizedAction = Normalize(payload.Action);
        var normalizedResourceType = Normalize(payload.ResourceType);
        var normalizedActorType = NormalizeActorType(payload.ActorType);

        if (!allowedProducers.Contains(normalizedProducer))
        {
            throw new ArgumentException("Audit producer is not allowlisted.");
        }

        if (string.IsNullOrWhiteSpace(payload.Domain)
            || string.IsNullOrWhiteSpace(payload.Outcome)
            || string.IsNullOrWhiteSpace(payload.RetentionClass)
            || !AllowedDomains.Contains(normalizedDomain)
            || !AllowedOutcomes.Contains(normalizedOutcome)
            || !retentionDays.ContainsKey(normalizedRetentionClass))
        {
            throw new ArgumentException("Audit domain, outcome, or retention class is not allowlisted.");
        }

        EnsureSafeIdentifier(payload.Action, nameof(payload.Action));
        EnsureSafeIdentifier(payload.ResourceType, nameof(payload.ResourceType));
        if (payload.FailureCategory is not null)
        {
            EnsureSafeIdentifier(payload.FailureCategory, nameof(payload.FailureCategory));
        }

        if (!AllowedActorTypes.Contains(normalizedActorType))
        {
            throw new ArgumentException("Audit actor type is not allowlisted.", nameof(payload));
        }

        EnsureActorHash(normalizedActorType, payload.ActorIdHash);

        if (AuditEventCatalog.TryGet(normalizedAction, out var definition)
            && definition is not null)
        {
            if (!string.Equals(definition.Domain, normalizedDomain, StringComparison.Ordinal)
                || !string.Equals(definition.ResourceType, normalizedResourceType, StringComparison.Ordinal)
                || !string.Equals(definition.RetentionClass, normalizedRetentionClass, StringComparison.Ordinal)
                || !definition.Producers.Contains(normalizedProducer, StringComparer.Ordinal)
                || !definition.ActorTypes.Contains(normalizedActorType, StringComparer.Ordinal))
            {
                throw new ArgumentException("Audit event does not match its catalog definition.");
            }

            return;
        }

        if (normalizedAction.StartsWith("tool.", StringComparison.Ordinal)
            && normalizedAction.Length > "tool.".Length
            && string.Equals(normalizedProducer, "mcp-service", StringComparison.Ordinal)
            && string.Equals(normalizedDomain, AuditDomains.Mcp, StringComparison.Ordinal)
            && string.Equals(normalizedResourceType, AuditResourceTypes.McpTool, StringComparison.Ordinal)
            && string.Equals(normalizedRetentionClass, AuditRetentionClasses.Standard, StringComparison.Ordinal)
            && string.Equals(normalizedActorType, AuditActorTypes.Service, StringComparison.Ordinal))
        {
            return;
        }

        throw new ArgumentException("Audit action is not in the sensitive-operation catalog.");
    }

    public DateTimeOffset ExpiresAt(DateTimeOffset occurredAtUtc, string retentionClass)
    {
        var days = retentionDays[Normalize(retentionClass)];
        return occurredAtUtc.AddDays(days);
    }

    public static void EnsureSafeIdentifier(string value, string parameterName)
        => EnsureSafeIdentifier(value, parameterName, 64);

    public static void EnsureSafeEnvelopeIdentifier(string value, string parameterName)
        => EnsureSafeIdentifier(value, parameterName, 128);

    public static void EnsureSubjectHash(string? value)
    {
        if (value is not null
            && (value.Length != 64 || value.Any(character =>
                !(character is >= '0' and <= '9' or >= 'a' and <= 'f'))))
        {
            throw new ArgumentException(
                "Audit subject hash must be 64 lowercase hexadecimal characters.",
                nameof(value));
        }
    }

    public static string NormalizeActorType(string? value) =>
        string.IsNullOrWhiteSpace(value) ? AuditActorTypes.Service : Normalize(value);

    private static void EnsureActorHash(string actorType, string? actorIdHash)
    {
        if (actorType is AuditActorTypes.User or AuditActorTypes.Admin)
        {
            if (actorIdHash is null)
            {
                throw new ArgumentException(
                    "User and admin audit actors require a pseudonymous actor hash.",
                    nameof(actorIdHash));
            }

            EnsureSubjectHash(actorIdHash);
            return;
        }

        if (actorIdHash is not null)
        {
            throw new ArgumentException(
                "Anonymous, service, and system audit actors cannot carry an actor hash.",
                nameof(actorIdHash));
        }
    }

    private static void EnsureSafeIdentifier(
        string value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength
            || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            throw new ArgumentException(
                $"Audit identifiers must use 1-{maximumLength} safe characters.",
                parameterName);
        }
    }

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
