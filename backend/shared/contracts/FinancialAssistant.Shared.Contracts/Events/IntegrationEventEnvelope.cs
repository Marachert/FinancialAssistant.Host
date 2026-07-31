using System.Globalization;

namespace FinancialAssistant.Shared.Contracts.Events;

/// <summary>
/// Carries service-owned event data with consistent delivery and version metadata.
/// </summary>
/// <typeparam name="TPayload">The immutable, event-specific payload contract.</typeparam>
public sealed record IntegrationEventEnvelope<TPayload>
{
    public IntegrationEventEnvelope(
        string eventId,
        string occurrenceId,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string producer,
        int schemaVersion,
        string correlationId,
        string causationId,
        string? userIdHash,
        TPayload payload)
    {
        EventId = RequireValue(eventId, nameof(eventId));
        OccurrenceId = RequireValue(occurrenceId, nameof(occurrenceId));
        EventType = RequireValue(eventType, nameof(eventType));
        Producer = RequireValue(producer, nameof(producer));
        CorrelationId = RequireValue(correlationId, nameof(correlationId));
        CausationId = RequireValue(causationId, nameof(causationId));

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "Schema version must be a positive integer.");
        }

        if (!TryReadSchemaVersion(EventType, out var eventTypeVersion))
        {
            throw new ArgumentException(
                "Event type must use the canonical {domain}.{action}.v{schemaVersion} format.",
                nameof(eventType));
        }

        if (eventTypeVersion != schemaVersion)
        {
            throw new ArgumentException(
                "Event type version must match the envelope schema version.",
                nameof(schemaVersion));
        }

        if (userIdHash is not null && string.IsNullOrWhiteSpace(userIdHash))
        {
            throw new ArgumentException(
                "User ID hash must be null or a non-empty opaque value.",
                nameof(userIdHash));
        }

        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        SchemaVersion = schemaVersion;
        UserIdHash = userIdHash;
        Payload = payload is null
            ? throw new ArgumentNullException(nameof(payload))
            : payload;
    }

    public string EventId { get; }

    public string OccurrenceId { get; }

    public string EventType { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public string Producer { get; }

    public int SchemaVersion { get; }

    public string CorrelationId { get; }

    public string CausationId { get; }

    public string? UserIdHash { get; }

    public TPayload Payload { get; }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value;
    }

    private static bool TryReadSchemaVersion(string eventType, out int schemaVersion)
    {
        schemaVersion = default;
        var segments = eventType.Split('.');

        if (segments.Length != 3 ||
            !IsValidNameSegment(segments[0]) ||
            !IsValidNameSegment(segments[1]))
        {
            return false;
        }

        var versionSegment = segments[2];
        if (versionSegment.Length < 2 ||
            versionSegment[0] != 'v' ||
            versionSegment[1] == '0' ||
            !int.TryParse(
                versionSegment.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out schemaVersion))
        {
            return false;
        }

        return schemaVersion > 0;
    }

    private static bool IsValidNameSegment(string segment)
    {
        if (segment.Length == 0 ||
            segment[0] == '-' ||
            segment[^1] == '-')
        {
            return false;
        }

        var previousWasHyphen = false;
        foreach (var character in segment)
        {
            var isHyphen = character == '-';
            var isLowerAsciiLetter = character is >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';

            if ((!isLowerAsciiLetter && !isDigit && !isHyphen) ||
                (isHyphen && previousWasHyphen))
            {
                return false;
            }

            previousWasHyphen = isHyphen;
        }

        return true;
    }
}
