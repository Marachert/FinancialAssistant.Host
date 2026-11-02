using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeTopologyTests
{
    private static readonly NativeBinding Income = NativeBinding.Event("income.created.v1", "income");
    private static NativeTopology Topology() => new("fa-synthetic", new[]
    {
        new NativeSubscription("analytics", "financial", new[] { Income }),
        new NativeSubscription("financial-summary", "financial", new[] { Income })
    }, new[] { Income });
    private static Dictionary<string, string> Passwords() => new()
    { ["fa-income"] = new('i', 48), ["fa-analytics"] = new('a', 48), ["fa-financial-summary"] = new('s', 48) };
    private static JsonElement[] Items(JsonElement root, string name) => root.GetProperty(name).EnumerateArray().ToArray();
    private static JsonElement Find(JsonElement root, string collection, string key, string value) =>
        Items(root, collection).Single(item => item.GetProperty(key).GetString() == value);

    [Fact]
    public void DefinitionsContainSaltedHashesWithoutPlaintextPasswordsOrAdministrativeTags()
    {
        var passwords = Passwords(); var bytes = Topology().CreateDefinitions(passwords);
        var text = Encoding.UTF8.GetString(bytes);
        foreach (var password in passwords.Values) { Assert.DoesNotContain(password, text); }
        using var document = JsonDocument.Parse(bytes);
        foreach (var user in Items(document.RootElement, "users"))
        {
            Assert.Empty(user.GetProperty("tags").EnumerateArray());
            Assert.Equal("rabbit_password_hashing_sha256", user.GetProperty("hashing_algorithm").GetString());
            var stored = Convert.FromBase64String(user.GetProperty("password_hash").GetString()!);
            Assert.Equal(36, stored.Length);
            var input = stored[..4].Concat(Encoding.UTF8.GetBytes(passwords[user.GetProperty("name").GetString()!])).ToArray();
            Assert.Equal(SHA256.HashData(input), stored[4..]);
        }
        Assert.Equal("fa-synthetic", Assert.Single(Items(document.RootElement, "vhosts")).GetProperty("name").GetString());
        Assert.DoesNotContain("password", Topology().ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationRolesCannotConfigureTopologyConsumePeersOrPublishUnknownTopics()
    {
        using var document = JsonDocument.Parse(Topology().CreateDefinitions(Passwords())); var root = document.RootElement;
        foreach (var permission in Items(root, "permissions"))
        {
            Assert.Equal("^$", permission.GetProperty("configure").GetString());
            Assert.DoesNotMatch(permission.GetProperty("write").GetString()!, "fa.retry-return");
            Assert.DoesNotMatch(permission.GetProperty("write").GetString()!, "fa.dead-letter");
            Assert.DoesNotMatch(permission.GetProperty("write").GetString()!, "amq.default");
        }
        var analytics = Find(root, "permissions", "user", "fa-analytics");
        Assert.Matches(analytics.GetProperty("read").GetString()!, "fa.analytics.financial.v1");
        Assert.DoesNotMatch(analytics.GetProperty("read").GetString()!, "fa.financial-summary.financial.v1");
        Assert.DoesNotMatch(analytics.GetProperty("read").GetString()!, "fa.analytics.financial.v1.dead-letter");
        Assert.DoesNotMatch(analytics.GetProperty("write").GetString()!, "fa.events");
        var topic = Find(root, "topic_permissions", "user", "fa-income");
        Assert.Matches(topic.GetProperty("write").GetString()!, "income.created.v1");
        Assert.DoesNotMatch(topic.GetProperty("write").GetString()!, "expense.created.v1");
        Assert.Equal("^$", topic.GetProperty("read").GetString());
    }

    [Fact]
    public void RetryReturnAndDeadLettersAreInternalAndEveryQueueIsDurableQuorum()
    {
        using var document = JsonDocument.Parse(Topology().CreateDefinitions(Passwords())); var root = document.RootElement;
        Assert.True(Find(root, "exchanges", "name", "fa.retry-return").GetProperty("internal").GetBoolean());
        Assert.True(Find(root, "exchanges", "name", "fa.dead-letter").GetProperty("internal").GetBoolean());
        Assert.Equal(10, Items(root, "queues").Length);
        foreach (var queue in Items(root, "queues"))
        {
            Assert.True(queue.GetProperty("durable").GetBoolean()); Assert.False(queue.GetProperty("auto_delete").GetBoolean());
            Assert.Equal("quorum", queue.GetProperty("arguments").GetProperty("x-queue-type").GetString());
        }
        var retry = Find(root, "policies", "name", "fa.analytics.financial.v1.retry.30s.income.created.v1").GetProperty("definition");
        Assert.Equal(30000, retry.GetProperty("message-ttl").GetInt32());
        Assert.Equal("fa.retry-return", retry.GetProperty("dead-letter-exchange").GetString());
        Assert.Equal("fa.analytics.financial.v1", retry.GetProperty("dead-letter-routing-key").GetString());
        Assert.Equal("at-least-once", retry.GetProperty("dead-letter-strategy").GetString());
        Assert.Equal("reject-publish", retry.GetProperty("overflow").GetString());
        Assert.DoesNotContain(Items(root, "bindings"), binding => binding.GetProperty("source").GetString() == "fa.retry-return" &&
            binding.GetProperty("destination").GetString()!.Contains("retry", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingSharedWeakOrAdditionalCredentialsAreRejectedWithoutDisclosure()
    {
        foreach (var passwords in new[]
        {
            new Dictionary<string, string>(),
            new Dictionary<string, string>(Passwords()) { ["fa-income"] = new('a', 48) },
            new Dictionary<string, string>(Passwords()) { ["fa-income"] = "synthetic-too-short" },
            new Dictionary<string, string>(Passwords()) { ["guest"] = new('g', 48) }
        })
        {
            var error = Assert.Throws<ArgumentException>(() => Topology().CreateDefinitions(passwords));
            Assert.Equal("Native broker roles require distinct bounded credentials.", error.Message); Assert.Null(error.InnerException);
        }
    }

    [Fact]
    public void UndeclaredPublisherDuplicateQueuesAndDefaultVhostAreInvalid()
    {
        var subscription = new NativeSubscription("analytics", "financial", new[] { Income });
        Assert.Throws<ArgumentException>(() => new NativeTopology("fa-synthetic", new[] { subscription }, new[] { NativeBinding.Event("income.created.v1", "expense") }));
        Assert.Throws<ArgumentException>(() => new NativeTopology("fa-synthetic", new[] { subscription, subscription }, new[] { Income }));
        Assert.Throws<ArgumentException>(() => new NativeTopology("/", new[] { subscription }, new[] { Income }));
    }
}
