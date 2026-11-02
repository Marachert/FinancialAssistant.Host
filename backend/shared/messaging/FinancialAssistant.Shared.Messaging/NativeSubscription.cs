namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeBinding
{
    private NativeBinding(string exchange, string routingKey, string eventType, string? recipient,
        IReadOnlyList<string> producers)
    {
        BrokerNames.EventType(routingKey); BrokerNames.EventType(eventType);
        ArgumentNullException.ThrowIfNull(producers);
        if (producers.Count is < 1 or > 16 || producers.Distinct(StringComparer.Ordinal).Count() != producers.Count)
        { throw new ArgumentException("A native binding requires explicit bounded publisher roles."); }
        foreach (var producer in producers) { BrokerNames.Segment(producer); }
        Exchange = exchange; RoutingKey = routingKey; EventType = eventType; Recipient = recipient;
        PublisherUsers = Array.AsReadOnly(producers.Select(p => "fa-" + p).ToArray());
    }

    public static NativeBinding Event(string eventType, params string[] producers) =>
        new(NativePublication.EventsExchange, eventType, eventType, null, producers);

    public static NativeBinding Command(string recipient, string action, string wireType, params string[] producers)
    {
        BrokerNames.Segment(recipient); BrokerNames.Segment(action, 48);
        return new NativeBinding(NativePublication.CommandsExchange, recipient + "." + action + ".v1", wireType, recipient, producers);
    }

    public string Exchange { get; }
    public string RoutingKey { get; }
    public string EventType { get; }
    public string? Recipient { get; }
    public IReadOnlyList<string> PublisherUsers { get; }
}

public sealed class NativeSubscription
{
    public const string RetryExchange = "fa.retry";
    public const string RetryReturnExchange = "fa.retry-return";
    public const string DeadLetterExchange = "fa.dead-letter";
    public static IReadOnlyList<TimeSpan> RetryDelays { get; } = Array.AsReadOnly(new[]
        { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5) });

    public NativeSubscription(string owner, string purpose, IReadOnlyList<NativeBinding> bindings)
    {
        BrokerNames.Segment(owner); BrokerNames.Segment(purpose, 40); ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count is < 1 or > 32 || bindings.Any(b => b is null || b.Recipient is not null && b.Recipient != owner) ||
            bindings.Select(b => b.EventType).Distinct(StringComparer.Ordinal).Count() != bindings.Count ||
            bindings.Select(b => (b.Exchange, b.RoutingKey)).Distinct().Count() != bindings.Count)
        { throw new ArgumentException("Invalid or ambiguous native subscription bindings."); }
        Owner = owner; Queue = "fa." + owner + "." + purpose + ".v1";
        if (Queue.Length > 96) { throw new ArgumentException("Native consumer queue name is too long."); }
        Bindings = Array.AsReadOnly(bindings.ToArray());
    }

    public string Owner { get; }
    public string BrokerUser => "fa-" + Owner;
    public string Queue { get; }
    public string DeadLetterQueue => Queue + ".dead-letter";
    public IReadOnlyList<NativeBinding> Bindings { get; }

    public string RetryQueue(NativeBinding binding, int attempt)
    {
        if (!Bindings.Contains(binding) || attempt is < 1 or > 3) { throw new ArgumentException("Invalid owned retry route."); }
        var label = attempt switch { 1 => "5s", 2 => "30s", _ => "5m" };
        return Queue + ".retry." + label + "." + binding.RoutingKey;
    }
}
