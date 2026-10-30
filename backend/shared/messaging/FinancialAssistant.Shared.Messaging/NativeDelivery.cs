using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeDeliveryRejectedException() : Exception("Native lifecycle delivery is invalid.");

public sealed class NativeDelivery
{
    internal const string AttemptHeader = "fa-retry-attempt";
    internal const string TargetHeader = "fa-retry-target";
    internal const string OriginHeader = "fa-original-publisher";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    private readonly byte[] body;
    private readonly long timestamp;

    private NativeDelivery(NativeSubscription subscription, NativeBinding binding, string eventId,
        string publisherUser, int attempt, long timestamp, byte[] body, long? sourceSequence)
    { Subscription = subscription; Binding = binding; EventId = eventId; PublisherUser = publisherUser; Attempt = attempt; this.timestamp = timestamp; this.body = body; SourceSequence = sourceSequence; }

    public string EventId { get; }
    public string EventType => Binding.EventType;
    public string PublisherUser { get; }
    public int Attempt { get; }
    public long? SourceSequence { get; }
    public bool CanRetry => Attempt < 3;
    internal NativeSubscription Subscription { get; }
    internal NativeBinding Binding { get; }
    internal ReadOnlyMemory<byte> Body => body;
    internal long Timestamp => timestamp;

    public static NativeDelivery Read(NativeSubscription subscription, string exchange, string routingKey,
        IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(subscription); ArgumentNullException.ThrowIfNull(properties);
            if (body.Length is < 1 or > NativePublication.MaximumBodyBytes || properties.ContentType != "application/json" ||
                properties.ContentEncoding != "utf-8" || properties.DeliveryMode != DeliveryModes.Persistent ||
                properties.CorrelationId is not null || properties.ReplyTo is not null)
            { throw new ArgumentException(); }
            BrokerNames.Identifier(properties.MessageId!);
            var binding = subscription.Bindings.SingleOrDefault(b => b.EventType == properties.Type) ?? throw new ArgumentException();
            var headers = properties.Headers;
            var retryKeys = new[] { AttemptHeader, TargetHeader, OriginHeader };
            if (headers is not null && headers.Keys.Any(k => k.StartsWith("fa-", StringComparison.Ordinal) &&
                k != NativePublication.SourceSequenceHeader && !retryKeys.Contains(k, StringComparer.Ordinal)))
            { throw new ArgumentException(); }
            long? sequence = null;
            if (headers is not null && headers.TryGetValue(NativePublication.SourceSequenceHeader, out var ordinal))
            {
                if (ordinal is not long number || number is <= 0 or long.MaxValue) { throw new ArgumentException(); }
                sequence = number;
            }
            var hasRetry = headers is not null && retryKeys.Any(headers.ContainsKey);
            var attempt = 0; var origin = properties.UserId;
            if (hasRetry)
            {
                if (exchange != NativeSubscription.RetryReturnExchange || routingKey != subscription.Queue ||
                    properties.UserId != subscription.BrokerUser || headers![AttemptHeader] is not int count || count is < 1 or > 3 ||
                    HeaderText(headers, TargetHeader) != subscription.Queue)
                { throw new ArgumentException(); }
                attempt = count; origin = HeaderText(headers, OriginHeader);
            }
            else if (exchange != binding.Exchange || routingKey != binding.RoutingKey) { throw new ArgumentException(); }
            if (origin is null || !binding.PublisherUsers.Contains(origin, StringComparer.Ordinal)) { throw new ArgumentException(); }
            _ = DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime);
            var snapshot = body.ToArray();
            using var document = JsonDocument.Parse(snapshot, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("eventId", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != properties.MessageId ||
                !document.RootElement.TryGetProperty("eventType", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != properties.Type)
            { throw new ArgumentException(); }
            UniqueProperties(document.RootElement);
            return new NativeDelivery(subscription, binding, properties.MessageId!, origin, attempt, properties.Timestamp.UnixTime, snapshot, sequence);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new NativeDeliveryRejectedException(); }
    }

    public T Deserialize<T>()
    {
        try { return JsonSerializer.Deserialize<T>(body, Json) ?? throw new JsonException(); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NotSupportedException)
        { throw new NativeDeliveryRejectedException(); }
    }

    public void EnsureWireIdentity(string eventId, string eventType, DateTimeOffset occurredAtUtc)
    {
        if (eventId != EventId || eventType != EventType || occurredAtUtc == default || occurredAtUtc.Offset != TimeSpan.Zero ||
            occurredAtUtc.ToUnixTimeSeconds() != timestamp) { throw new NativeDeliveryRejectedException(); }
    }

    internal IDictionary<string, object?> RetryHeaders()
    {
        if (!CanRetry) { throw new InvalidOperationException("Native retry budget is exhausted."); }
        var headers = new Dictionary<string, object?>
        {
            [AttemptHeader] = Attempt + 1,
            [TargetHeader] = StrictUtf8.GetBytes(Subscription.Queue),
            [OriginHeader] = StrictUtf8.GetBytes(PublisherUser)
        };
        if (SourceSequence is { } sequence) { headers[NativePublication.SourceSequenceHeader] = sequence; }
        return headers;
    }

    private static string HeaderText(IDictionary<string, object?> headers, string key)
    {
        if (!headers.TryGetValue(key, out var value) || value is not byte[] bytes || bytes.Length is < 1 or > 128)
        { throw new ArgumentException(); }
        return StrictUtf8.GetString(bytes);
    }

    private static void UniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) { throw new JsonException(); } UniqueProperties(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { foreach (var item in element.EnumerateArray()) { UniqueProperties(item); } }
    }

    public override string ToString() => "Native delivery (payload omitted).";
}
