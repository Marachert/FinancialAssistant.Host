using System.Text.Json;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativePublication
{
    public const int MaximumBodyBytes = 256 * 1024;
    public const string EventsExchange = "fa.events";
    public const string CommandsExchange = "fa.commands";
    internal const string SourceSequenceHeader = "fa-source-sequence";
    private readonly byte[] body;

    public NativePublication(string exchange, string routingKey, string eventType, string eventId,
        DateTimeOffset occurredAtUtc, ReadOnlyMemory<byte> body, long? sourceSequence = null)
    {
        if (exchange is not (EventsExchange or CommandsExchange) || body.Length is < 1 or > MaximumBodyBytes ||
            occurredAtUtc == default || occurredAtUtc.Offset != TimeSpan.Zero || sourceSequence is <= 0 or long.MaxValue)
        { throw new ArgumentException("Invalid native publication."); }
        BrokerNames.EventType(routingKey); BrokerNames.EventType(eventType); BrokerNames.Identifier(eventId);
        if (exchange == EventsExchange && routingKey != eventType)
        { throw new ArgumentException("Integration-event routing must match its versioned event type."); }
        this.body = body.ToArray();
        try
        {
            using var document = JsonDocument.Parse(this.body, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) { throw new JsonException(); }
        }
        catch (JsonException) { throw new ArgumentException("Native publication requires a bounded JSON object."); }
        Exchange = exchange; RoutingKey = routingKey; EventType = eventType; EventId = eventId; OccurredAtUtc = occurredAtUtc;
        SourceSequence = sourceSequence;
    }

    public string Exchange { get; }
    public string RoutingKey { get; }
    public string EventType { get; }
    public string EventId { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public long? SourceSequence { get; }
    public int BodyBytes => body.Length;
    internal ReadOnlyMemory<byte> Body => body;
    public override string ToString() => "Native publication (payload omitted).";
}
