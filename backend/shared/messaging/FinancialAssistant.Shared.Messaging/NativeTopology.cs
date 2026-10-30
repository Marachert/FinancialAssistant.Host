using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeTopology
{
    private readonly NativeSubscription[] subscriptions;
    private readonly NativeBinding[] publications;

    public NativeTopology(string virtualHost, IReadOnlyList<NativeSubscription> subscriptions, IReadOnlyList<NativeBinding> publications)
    {
        BrokerNames.Segment(virtualHost, 96);
        ArgumentNullException.ThrowIfNull(subscriptions); ArgumentNullException.ThrowIfNull(publications);
        if (!virtualHost.StartsWith("fa-", StringComparison.Ordinal) || subscriptions.Count is < 1 or > 32 ||
            publications.Count is < 1 or > 64 || subscriptions.Any(s => s is null) || publications.Any(p => p is null) ||
            subscriptions.Select(s => s.Queue).Distinct(StringComparer.Ordinal).Count() != subscriptions.Count)
        { throw new ArgumentException("Invalid native broker topology."); }
        foreach (var binding in subscriptions.SelectMany(s => s.Bindings))
        {
            var declared = publications.Where(p => p.Exchange == binding.Exchange && p.RoutingKey == binding.RoutingKey && p.EventType == binding.EventType)
                .SelectMany(p => p.PublisherUsers).ToHashSet(StringComparer.Ordinal);
            if (binding.PublisherUsers.Any(p => !declared.Contains(p))) { throw new ArgumentException("Native subscription has no authorized publication route."); }
        }
        VirtualHost = virtualHost; this.subscriptions = subscriptions.ToArray(); this.publications = publications.ToArray();
        BrokerUsers = Array.AsReadOnly(subscriptions.Select(s => s.BrokerUser).Concat(publications.SelectMany(p => p.PublisherUsers))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        if (BrokerUsers.Count > 32 || subscriptions.Sum(s => 2 + 3 * s.Bindings.Count) > 512)
        { throw new ArgumentException("Native broker topology exceeds its bounded role/queue budget."); }
    }

    public string VirtualHost { get; }
    public IReadOnlyList<string> BrokerUsers { get; }

    // The returned definitions contain credential hashes and must be handled as secrets.
    // Normal application roles cannot import them or administer broker topology.
    public byte[] CreateDefinitions(IReadOnlyDictionary<string, string> passwords)
    {
        ArgumentNullException.ThrowIfNull(passwords);
        if (passwords.Count != BrokerUsers.Count || BrokerUsers.Any(u => !passwords.TryGetValue(u, out var password) ||
                password is null || password.Length is < 32 or > 1024 || password.Any(char.IsControl) || string.IsNullOrWhiteSpace(password)) ||
            passwords.Values.Distinct(StringComparer.Ordinal).Count() != passwords.Count)
        { throw new ArgumentException("Native broker roles require distinct bounded credentials."); }
        var users = BrokerUsers.Select(user => new
        { name = user, password_hash = PasswordHash(passwords[user]), hashing_algorithm = "rabbit_password_hashing_sha256", tags = Array.Empty<string>() }).ToArray();
        var permissions = BrokerUsers.Select(user => new
        {
            user,
            vhost = VirtualHost,
            configure = "^$",
            write = ExactNames(publications.Where(p => p.PublisherUsers.Contains(user)).Select(p => p.Exchange)
                .Concat(subscriptions.Any(s => s.BrokerUser == user) ? new[] { NativeSubscription.RetryExchange } : Array.Empty<string>())),
            read = ExactNames(subscriptions.Where(s => s.BrokerUser == user).Select(s => s.Queue))
        }).ToArray();
        var topicPermissions = BrokerUsers.Select(user => new
        {
            user,
            vhost = VirtualHost,
            exchange = NativePublication.EventsExchange,
            write = ExactNames(publications.Where(p => p.Exchange == NativePublication.EventsExchange && p.PublisherUsers.Contains(user)).Select(p => p.RoutingKey)),
            read = "^$"
        }).ToArray();
        var exchanges = new[]
        {
            Exchange(NativePublication.EventsExchange, "topic", false), Exchange(NativePublication.CommandsExchange, "direct", false),
            Exchange(NativeSubscription.RetryExchange, "direct", false), Exchange(NativeSubscription.RetryReturnExchange, "direct", true),
            Exchange(NativeSubscription.DeadLetterExchange, "direct", true)
        };
        var queues = new List<object>(); var bindings = new List<object>(); var policies = new List<object>();
        foreach (var subscription in subscriptions)
        {
            queues.Add(Queue(subscription.Queue)); queues.Add(Queue(subscription.DeadLetterQueue));
            bindings.Add(Binding(NativeSubscription.RetryReturnExchange, subscription.Queue, subscription.Queue));
            bindings.Add(Binding(NativeSubscription.DeadLetterExchange, subscription.DeadLetterQueue, subscription.Queue));
            policies.Add(Policy(subscription.Queue, DeadLetterPolicy(NativeSubscription.DeadLetterExchange, subscription.Queue)));
            policies.Add(Policy(subscription.DeadLetterQueue, CapacityPolicy()));
            foreach (var binding in subscription.Bindings)
            {
                bindings.Add(Binding(binding.Exchange, subscription.Queue, binding.RoutingKey));
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    var name = subscription.RetryQueue(binding, attempt);
                    queues.Add(Queue(name)); bindings.Add(Binding(NativeSubscription.RetryExchange, name, name));
                    var retryPolicy = DeadLetterPolicy(NativeSubscription.RetryReturnExchange, subscription.Queue);
                    retryPolicy["message-ttl"] = (long)NativeSubscription.RetryDelays[attempt - 1].TotalMilliseconds;
                    policies.Add(Policy(name, retryPolicy));
                }
            }
        }
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            vhosts = new[] { new { name = VirtualHost } },
            users,
            permissions,
            topic_permissions = topicPermissions,
            exchanges,
            queues,
            bindings,
            policies
        });
    }

    private object Exchange(string name, string type, bool isInternal) => new
    { name, vhost = VirtualHost, type, durable = true, auto_delete = false, @internal = isInternal, arguments = new Dictionary<string, object>() };
    private object Queue(string name) => new
    { name, vhost = VirtualHost, durable = true, auto_delete = false, arguments = new Dictionary<string, object> { ["x-queue-type"] = "quorum", ["x-quorum-initial-group-size"] = 1 } };
    private object Binding(string source, string destination, string key) => new
    { source, vhost = VirtualHost, destination, destination_type = "queue", routing_key = key, arguments = new Dictionary<string, object>() };
    private object Policy(string queue, IDictionary<string, object> definition) => new Dictionary<string, object>
    { ["name"] = queue, ["vhost"] = VirtualHost, ["pattern"] = ExactNames(new[] { queue }), ["apply-to"] = "queues", ["priority"] = 100, ["definition"] = definition };
    private static Dictionary<string, object> CapacityPolicy() => new()
    { ["overflow"] = "reject-publish", ["max-length"] = 10000, ["max-length-bytes"] = 64 * 1024 * 1024 };
    private static Dictionary<string, object> DeadLetterPolicy(string exchange, string route)
    {
        var policy = CapacityPolicy();
        policy["dead-letter-exchange"] = exchange; policy["dead-letter-routing-key"] = route;
        policy["dead-letter-strategy"] = "at-least-once"; policy["delivery-limit"] = 20;
        return policy;
    }
    private static string ExactNames(IEnumerable<string> names)
    {
        var items = names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(Regex.Escape).ToArray();
        return items.Length == 0 ? "^$" : "^(?:" + string.Join("|", items) + ")$";
    }
    private static string PasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(4); var bytes = Encoding.UTF8.GetBytes(password);
        var input = new byte[salt.Length + bytes.Length]; salt.CopyTo(input, 0); bytes.CopyTo(input, salt.Length);
        try
        {
            var digest = SHA256.HashData(input); var stored = new byte[salt.Length + digest.Length];
            salt.CopyTo(stored, 0); digest.CopyTo(stored, salt.Length);
            try { return Convert.ToBase64String(stored); }
            finally { CryptographicOperations.ZeroMemory(digest); CryptographicOperations.ZeroMemory(stored); }
        }
        finally { CryptographicOperations.ZeroMemory(input); CryptographicOperations.ZeroMemory(bytes); }
    }

    public override string ToString() => "Native broker topology (credentials omitted).";
}
