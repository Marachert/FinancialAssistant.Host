using System.Net;
using System.Net.Security;
using RabbitMQ.Client;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeBrokerSettings
{
    private readonly Uri uri;

    public NativeBrokerSettings(string serviceId, string connectionString)
    {
        try
        {
            BrokerNames.Segment(serviceId);
            if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var parsed) ||
                parsed.Scheme is not ("amqp" or "amqps") || parsed.Port is < 1 or > 65535 ||
                parsed.Query.Length != 0 || parsed.Fragment.Length != 0 ||
                !(parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                  IPAddress.TryParse(parsed.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address)))
            { throw new ArgumentException(); }
            var credentials = parsed.UserInfo.Split(':', 2);
            if (credentials.Length != 2 || parsed.UserInfo.Count(c => c == ':') != 1 ||
                Uri.UnescapeDataString(credentials[0]) != "fa-" + serviceId ||
                Uri.UnescapeDataString(credentials[1]).Length is < 32 or > 1024)
            { throw new ArgumentException(); }
            var vhost = Uri.UnescapeDataString(parsed.AbsolutePath.Length > 0 ? parsed.AbsolutePath[1..] : string.Empty);
            if (!vhost.StartsWith("fa-", StringComparison.Ordinal)) { throw new ArgumentException(); }
            BrokerNames.Segment(vhost, 96);
            _ = new ConnectionFactory { Uri = parsed };
            uri = parsed; ServiceId = serviceId; VirtualHost = vhost;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { throw new ArgumentException("Native broker configuration is invalid."); }
    }

    public string ServiceId { get; }
    public string BrokerUser => "fa-" + ServiceId;
    public string VirtualHost { get; }
    public string Host => uri.Host;
    public int Port => uri.Port;
    public bool UsesTls => uri.Scheme == "amqps";

    internal ConnectionFactory CreateFactory()
    {
        var factory = new ConnectionFactory
        {
            Uri = uri,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            RequestedHeartbeat = TimeSpan.FromSeconds(10),
            MaxInboundMessageBodySize = NativePublication.MaximumBodyBytes,
            ClientProvidedName = "fa-native:" + ServiceId
        };
        factory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.None;
        return factory;
    }

    public override string ToString() => "Native broker settings (credentials redacted).";
}
