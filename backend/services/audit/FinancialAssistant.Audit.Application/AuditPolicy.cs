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
        ["succeeded", "failed", "denied", "accepted"],
        StringComparer.Ordinal);
    private readonly HashSet<string> allowedProducers = new(
        allowedProducers.Select(Normalize),
        StringComparer.Ordinal);
    private readonly Dictionary<string, int> retentionDays = retentionDays
        .ToDictionary(item => Normalize(item.Key), item => item.Value, StringComparer.Ordinal);

    public void Validate(AuditEventV1 payload, string producer)
    {
        if (!allowedProducers.Contains(Normalize(producer)))
        {
            throw new ArgumentException("Audit producer is not allowlisted.");
        }

        if (string.IsNullOrWhiteSpace(payload.Domain)
            || string.IsNullOrWhiteSpace(payload.Outcome)
            || string.IsNullOrWhiteSpace(payload.RetentionClass)
            || !AllowedDomains.Contains(Normalize(payload.Domain))
            || !AllowedOutcomes.Contains(Normalize(payload.Outcome))
            || !retentionDays.ContainsKey(Normalize(payload.RetentionClass)))
        {
            throw new ArgumentException("Audit domain, outcome, or retention class is not allowlisted.");
        }

        EnsureSafeIdentifier(payload.Action, nameof(payload.Action));
        EnsureSafeIdentifier(payload.ResourceType, nameof(payload.ResourceType));
        if (payload.FailureCategory is not null)
        {
            EnsureSafeIdentifier(payload.FailureCategory, nameof(payload.FailureCategory));
        }
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

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
