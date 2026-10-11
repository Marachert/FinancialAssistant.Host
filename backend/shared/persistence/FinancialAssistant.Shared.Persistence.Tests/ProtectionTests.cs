using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Persistence.Tests;

public sealed class ProtectionTests
{
    private static byte[] Key(byte value) => Enumerable.Repeat(value, 32).ToArray();
    private static RecordProtection Protection() => new(Key(1), "v1", new Dictionary<string, byte[]> { ["v1"] = Key(2) });

    [Fact]
    public void CanonicalIdentitiesHaveSeparateRolesAndSchemas()
    {
        foreach (var service in NativeServiceCatalog.Services.Where(service => service.Id is not ("gateway" or "mcp")))
        {
            var identity = new StorageIdentity(service.Id);
            Assert.True(RecordProtection.ValidName(identity.Schema));
            Assert.Equal(identity.Schema, identity.RuntimeRole);
            Assert.Equal(identity.Schema + "_migration", identity.MigrationRole);
        }
    }

    [Theory]
    [InlineData("gateway")]
    [InlineData("mcp")]
    [InlineData("Identity")]
    [InlineData("identity;DROP")]
    [InlineData("foreign")]
    public void NonOwnersAreRejected(string service) => Assert.Throws<ArgumentException>(() => new StorageIdentity(service));

    [Fact]
    public void CiphertextIsRandomAndBoundToContext()
    {
        using var protection = Protection();
        var payload = Encoding.UTF8.GetBytes("synthetic minimized state");
        var context = Encoding.UTF8.GetBytes("identity|account|owner-hash|id-hash|0");
        var first = protection.Encrypt(payload, context);
        Assert.NotEqual(first, protection.Encrypt(payload, context));
        Assert.Equal(payload, protection.Decrypt(first, context));
        Assert.DoesNotContain("synthetic", Encoding.UTF8.GetString(first));
        Assert.Throws<StorageUnavailableException>(() => protection.Decrypt(first, Encoding.UTF8.GetBytes("other owner or revision")));
        first[^1] ^= 1;
        Assert.Throws<StorageUnavailableException>(() => protection.Decrypt(first, context));
    }

    [Fact]
    public void IndexPartsAreUnambiguousAndServiceScoped()
    {
        using var protection = Protection();
        Assert.NotEqual(protection.Index("ab", "c"), protection.Index("a", "bc"));
        Assert.NotEqual(protection.Index("identity", "owner"), protection.Index("profile", "owner"));
        Assert.Equal(protection.Index("identity", "owner"), protection.Index("identity", "owner"));
        Assert.Throws<ArgumentException>(() => protection.Index(""));
        Assert.Throws<ArgumentException>(() => protection.Index(new string('x', 4097)));
    }

    [Fact]
    public void OldKeyIsReadableAfterRotation()
    {
        using var old = Protection();
        var encrypted = old.Encrypt([1, 2, 3], [4]);
        using var rotated = new RecordProtection(Key(1), "v2", new Dictionary<string, byte[]> { ["v1"] = Key(2), ["v2"] = Key(3) });
        Assert.Equal(new byte[] { 1, 2, 3 }, rotated.Decrypt(encrypted, [4]));
        using var lost = new RecordProtection(Key(1), "v2", new Dictionary<string, byte[]> { ["v2"] = Key(3) });
        Assert.Throws<StorageUnavailableException>(() => lost.Decrypt(encrypted, [4]));
    }

    [Fact]
    public void EnvelopeKeyIdIsAuthenticatedEvenForKeyAliases()
    {
        using var protection = new RecordProtection(Key(1), "v1", new Dictionary<string, byte[]> { ["v1"] = Key(2), ["v2"] = Key(2) });
        var envelope = protection.Encrypt([1], [2]);
        envelope[3] = (byte)'2';
        Assert.Throws<StorageUnavailableException>(() => protection.Decrypt(envelope, [2]));
    }

    [Fact]
    public void KeysAreCopiedAndDisposed()
    {
        var index = Key(1);
        var encryption = Key(2);
        var protection = new RecordProtection(index, "v1", new Dictionary<string, byte[]> { ["v1"] = encryption });
        var hash = protection.Index("owner");
        Array.Fill(index, (byte)9);
        Array.Fill(encryption, (byte)9);
        Assert.Equal(hash, protection.Index("owner"));
        Assert.Equal(new byte[] { 1 }, protection.Decrypt(protection.Encrypt([1], [2]), [2]));
        protection.Dispose();
        protection.Dispose();
        Assert.Throws<ObjectDisposedException>(() => protection.Index("owner"));
    }

    [Fact]
    public void IndexAndEncryptionKeyCannotBeShared()
    {
        Assert.Throws<ArgumentException>(() => new RecordProtection(Key(1), "v1", new Dictionary<string, byte[]> { ["v1"] = Key(1) }));
        Assert.Throws<ArgumentException>(() => new RecordProtection(Key(1), "absent", new Dictionary<string, byte[]> { ["v1"] = Key(2) }));
        Assert.Throws<ArgumentException>(() => new RecordProtection(Key(1), "BAD", new Dictionary<string, byte[]> { ["BAD"] = Key(2) }));
    }

    [Fact]
    public void PayloadAndEnvelopeBoundsAreEnforced()
    {
        using var protection = Protection();
        Assert.Equal(Array.Empty<byte>(), protection.Decrypt(protection.Encrypt([], []), []));
        var maximum = new byte[RecordProtection.MaximumPayloadBytes];
        Assert.Equal(maximum, protection.Decrypt(protection.Encrypt(maximum, []), []));
        Assert.Throws<ArgumentException>(() => protection.Encrypt(new byte[maximum.Length + 1], []));
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[30], new byte[100], new byte[maximum.Length + 63] })
        {
            var error = Assert.Throws<StorageUnavailableException>(() => protection.Decrypt(invalid, []));
            Assert.Null(error.InnerException);
        }
    }

    [Theory]
    [InlineData("postgres", 5432, 32)]
    [InlineData("valid", 80, 32)]
    [InlineData("valid", 5432, 31)]
    [InlineData("valid", 5432, 257)]
    [InlineData("other;sql", 5432, 32)]
    public void ConfigurationFailsWithoutPrintingValues(string database, int port, int length)
    {
        var secret = new string('q', length);
        var error = Assert.Throws<ArgumentException>(() => new OwnedPostgresOptions(new StorageIdentity("identity"), database, port, secret));
        Assert.DoesNotContain(secret, error.ToString());
        Assert.Equal("Owned PostgreSQL configuration is missing or invalid.", error.Message);
    }

    [Fact]
    public void ConfigurationToStringDoesNotRevealPassword()
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new OwnedPostgresOptions(new StorageIdentity("identity"), "financial_assistant", 5432, secret);
        Assert.DoesNotContain(secret, options.ToString());
    }
}
