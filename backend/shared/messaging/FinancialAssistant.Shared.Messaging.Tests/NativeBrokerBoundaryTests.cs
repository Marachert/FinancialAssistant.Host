using System.Net.Security;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeBrokerBoundaryTests
{
    private static readonly string SyntheticPassword = new('s', 48);
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static string Connection(string host = "127.0.0.1", string user = "fa-income", string vhost = "fa-test", string scheme = "amqp")
        => $"{scheme}://{user}:{SyntheticPassword}@{host}:5672/{vhost}";

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("[::1]")]
    public void LocalSettingsUseSeparateIdentityAndNeverExposeCredentials(string host)
    {
        var settings = new NativeBrokerSettings("income", Connection(host));
        Assert.Equal("fa-income", settings.BrokerUser); Assert.Equal("fa-test", settings.VirtualHost);
        var factory = settings.CreateFactory();
        Assert.Equal("fa-income", factory.UserName); Assert.Equal(SyntheticPassword, factory.Password);
        Assert.False(factory.AutomaticRecoveryEnabled); Assert.False(factory.TopologyRecoveryEnabled);
        Assert.DoesNotContain(SyntheticPassword, settings.ToString());
        Assert.DoesNotContain(SyntheticPassword, JsonSerializer.Serialize(settings));
    }

    [Fact]
    public void TlsDoesNotDisableCertificateValidation()
    {
        var factory = new NativeBrokerSettings("income", Connection(scheme: "amqps")).CreateFactory();
        Assert.True(factory.Ssl.Enabled); Assert.Equal(SslPolicyErrors.None, factory.Ssl.AcceptablePolicyErrors);
    }

    [Fact]
    public void MissingPortAndWeakCredentialsFailWithoutNetworkActivity()
    {
        Assert.Throws<ArgumentException>(() => new NativeBrokerSettings("income", $"amqp://fa-income:{SyntheticPassword}@localhost/fa-test"));
        Assert.Throws<ArgumentException>(() => new NativeBrokerSettings("income", "amqp://fa-income:synthetic@localhost:5672/fa-test"));
    }

    [Theory]
    [InlineData("remote.invalid", "fa-income", "fa-test", "amqp")]
    [InlineData("127.0.0.1", "guest", "fa-test", "amqp")]
    [InlineData("127.0.0.1", "fa-expense", "fa-test", "amqp")]
    [InlineData("127.0.0.1", "fa-income", "", "amqp")]
    [InlineData("127.0.0.1", "fa-income", "/fa-test", "amqp")]
    [InlineData("127.0.0.1", "fa-income", "fa-test%2Fother", "amqp")]
    [InlineData("127.0.0.1", "fa-income", "fa-test", "https")]
    [InlineData("127.0.0.1", "fa-income", "fa-test?option=unsafe", "amqp")]
    public void ForeignDefaultOrAmbiguousConfigurationFailsWithoutLeakingInput(string host, string user, string vhost, string scheme)
    {
        var exception = Assert.Throws<ArgumentException>(() => new NativeBrokerSettings("income", Connection(host, user, vhost, scheme)));
        Assert.Equal("Native broker configuration is invalid.", exception.Message);
        Assert.Null(exception.InnerException); Assert.DoesNotContain(SyntheticPassword, exception.ToString());
    }

    [Fact]
    public void BodyIsSnapshottedAndPublicDiagnosticsOmitIt()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"synthetic\":true}");
        var publication = new NativePublication("fa.events", "income.created.v1", "income.created.v1", "synthetic-event", At, bytes);
        bytes[0] = 0;
        Assert.Equal((byte)'{', publication.Body.Span[0]);
        Assert.DoesNotContain("synthetic", publication.ToString());
        Assert.DoesNotContain("synthetic\":", JsonSerializer.Serialize(publication));
    }

    [Fact]
    public void RestrictedCommandsCanRetainTheirExistingWireTypeWithoutBroadcastingIt()
    {
        var publication = new NativePublication("fa.commands", "income.transaction-confirmed.v1", "transaction.confirmed.v1",
            "synthetic-confirmation", At, Encoding.UTF8.GetBytes("{}"));
        Assert.Equal("fa.commands", publication.Exchange);
        Assert.Equal("transaction.confirmed.v1", publication.EventType);
        Assert.Throws<ArgumentException>(() => new NativePublication("fa.events", publication.RoutingKey,
            publication.EventType, publication.EventId, At, publication.Body));
    }

    [Theory]
    [InlineData("income.created.v01")]
    [InlineData("income.created.v0")]
    [InlineData("income.created.v2147483648")]
    [InlineData("income.created.v1.more")]
    [InlineData("income.*.v1")]
    [InlineData("income.Created.v1")]
    public void RoutingRejectsWildcardsNoncanonicalOrUnsupportedVersions(string type)
    {
        Assert.Throws<ArgumentException>(() => new NativePublication("fa.events", type, type, "synthetic-event", At, Encoding.UTF8.GetBytes("{}")));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{broken")]
    [InlineData("\"synthetic\"")]
    public void BodyRequiresOneBoundedJsonObject(string json)
    {
        Assert.Throws<ArgumentException>(() => new NativePublication("fa.events", "income.created.v1", "income.created.v1",
            "synthetic-event", At, Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void InvalidSizeTimeIdentifierAndExchangeFailBeforePublishing()
    {
        NativePublication Make(string exchange = "fa.events", string id = "synthetic-event", DateTimeOffset? at = null, byte[]? bytes = null)
            => new(exchange, "income.created.v1", "income.created.v1", id, at ?? At, bytes ?? Encoding.UTF8.GetBytes("{}"));
        Assert.Throws<ArgumentException>(() => Make(exchange: "amq.topic"));
        Assert.Throws<ArgumentException>(() => Make(id: "private@example.invalid"));
        Assert.Throws<ArgumentException>(() => Make(at: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => Make(at: At.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => Make(bytes: []));
        Assert.Throws<ArgumentException>(() => Make(bytes: new byte[NativePublication.MaximumBodyBytes + 1]));
    }
}
