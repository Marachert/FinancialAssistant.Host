using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Shared.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeConfigurationSourceTests
{
    [Theory]
    [MemberData(nameof(NativeServiceConfigurationTests.Hosts), MemberType = typeof(NativeServiceConfigurationTests))]
    public void FullEncryptedPackageResolvesExistingSettingsWithoutSerializingPlaintext(string serviceId)
    {
        var plan = NativeServiceConfigurationPlan.Create(NativeServiceConfigurationTests.Document(serviceId));
        var inputs = Inputs(plan); var protector = new SyntheticProtector();
        try
        {
            using var package = NativeConfigurationPackage.Create(plan, inputs, protector);
            var source = new NativeConfigurationSource(serviceId, () => package, protector);
            foreach (var input in inputs.Values)
            {
                var text = Encoding.UTF8.GetString(input);
                Assert.DoesNotContain(text, JsonSerializer.Serialize(package));
                Assert.DoesNotContain(text, source.ToString());
            }
            var configuration = new ConfigurationBuilder().Add(source).Build();
            using var configurationLifetime = (IDisposable)configuration;
            foreach (var pair in inputs) { Assert.Equal(Encoding.UTF8.GetString(pair.Value), configuration[pair.Key]); }
            Assert.Equal("false", configuration["AiOrchestration:Provider:Enabled"]);
            Assert.All(protector.ReturnedPlaintext, bytes => Assert.All(bytes, value => Assert.Equal((byte)0, value)));
        }
        finally { foreach (var input in inputs.Values) { CryptographicOperations.ZeroMemory(input); } }
    }

    [Fact]
    public void OwnershipIsCheckedBeforeAnySecretIsDecryptedAndNoFallbackIsPublished()
    {
        var plan = NativeServiceConfigurationPlan.Create(NativeServiceConfigurationTests.Document("gateway"));
        var inputs = Inputs(plan); var protector = new SyntheticProtector();
        try
        {
            using var package = NativeConfigurationPackage.Create(plan, inputs, protector);
            var artifacts = new FaultArtifacts(package, rejectOwner: true);
            var source = new NativeConfigurationSource("gateway", () => artifacts, protector);
            var error = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder().Add(source).Build());
            Assert.Equal(0, protector.UnprotectCalls);
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("synthetic-private-error", error.ToString());
        }
        finally { foreach (var input in inputs.Values) { CryptographicOperations.ZeroMemory(input); } }
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("missing-input")]
    [InlineData("extra-input")]
    [InlineData("invalid-key")]
    public void PartialOrInvalidSecretMaterialCannotBecomeConfiguration(string mutation)
    {
        var plan = NativeServiceConfigurationPlan.Create(NativeServiceConfigurationTests.Document("income"));
        var inputs = Inputs(plan); var protector = new SyntheticProtector();
        try
        {
            if (mutation == "missing-input") { inputs.Remove(plan.Secrets[0].ConfigurationKey); }
            if (mutation == "extra-input") { inputs.Add("Foreign:Password", Encoding.UTF8.GetBytes(new string('x', 64))); }
            if (mutation == "invalid-key") { inputs["Income:Storage:Postgres:IndexKey"] = Encoding.UTF8.GetBytes(new string('x', 64)); }
            if (mutation != "tamper")
            { Assert.Throws<InvalidOperationException>(() => NativeConfigurationPackage.Create(plan, inputs, protector)); return; }
            using var package = NativeConfigurationPackage.Create(plan, inputs, protector);
            var source = new NativeConfigurationSource("income", () => new FaultArtifacts(package, rejectOwner: false), protector);
            var error = Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder().Add(source).Build());
            Assert.Null(error.InnerException);
            Assert.DoesNotContain("synthetic-private-error", error.ToString());
        }
        finally { foreach (var input in inputs.Values) { CryptographicOperations.ZeroMemory(input); } }
    }

    internal static Dictionary<string, byte[]> Inputs(NativeServiceConfigurationPlan plan) => plan.Secrets.ToDictionary(binding => binding.ConfigurationKey, binding =>
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("synthetic-only:" + binding.ConfigurationKey));
        var text = NativeSecretMaterial.IsDataKey(binding.ConfigurationKey) ? Convert.ToBase64String(hash) :
            binding.ConfigurationKey.EndsWith(":Messaging:ConnectionString", StringComparison.Ordinal)
                ? "amqp://fa-" + plan.ServiceId + ":" + Convert.ToHexString(hash) + "@127.0.0.1:5672/fa-synthetic"
                : Convert.ToHexString(hash);
        CryptographicOperations.ZeroMemory(hash);
        return Encoding.UTF8.GetBytes(text);
    }, StringComparer.Ordinal);

    internal sealed class SyntheticProtector : INativeSecretProtector
    {
        private readonly byte[] key = Enumerable.Repeat((byte)7, 32).ToArray();
        internal int UnprotectCalls { get; private set; }
        internal List<byte[]> ReturnedPlaintext { get; } = new();
        public byte[] Protect(ReadOnlySpan<byte> plaintext, NativeSecretBinding binding)
        {
            var result = new byte[28 + plaintext.Length]; RandomNumberGenerator.Fill(result.AsSpan(0, 12));
            using var cipher = new AesGcm(key, 16);
            cipher.Encrypt(result.AsSpan(0, 12), plaintext, result.AsSpan(28), result.AsSpan(12, 16), binding.CreateEntropy());
            return result;
        }
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, NativeSecretBinding binding)
        {
            UnprotectCalls++;
            var output = new byte[ciphertext.Length - 28];
            using var cipher = new AesGcm(key, 16);
            cipher.Decrypt(ciphertext[..12], ciphertext[28..], ciphertext.Slice(12, 16), output, binding.CreateEntropy());
            ReturnedPlaintext.Add(output); return output;
        }
    }

    private sealed class FaultArtifacts(NativeConfigurationPackage package, bool rejectOwner) : INativeConfigurationArtifacts
    {
        public byte[] ReadDocument() => package.ReadDocument();
        public void ValidateOwner(NativeServiceConfigurationPlan plan)
        {
            if (rejectOwner) { throw new InvalidOperationException("synthetic-private-error"); }
            package.ValidateOwner(plan);
        }
        public byte[] ReadSecret(Guid id)
        { var bytes = package.ReadSecret(id); bytes[^1] ^= 1; return bytes; }
        public void Dispose() { }
    }
}
