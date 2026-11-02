using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Shared.Hosting;

internal sealed class NativeConfigurationSource(string serviceId, Func<INativeConfigurationArtifacts> openArtifacts,
    INativeSecretProtector protector) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(serviceId, openArtifacts, protector);
    public override string ToString() => "Native protected configuration source (secret values omitted).";

    private sealed class Provider(string serviceId, Func<INativeConfigurationArtifacts> openArtifacts,
        INativeSecretProtector protector) : ConfigurationProvider, IDisposable
    {
        public override void Load()
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var artifacts = openArtifacts();
                var plan = NativeServiceConfigurationPlan.Parse(artifacts.ReadDocument());
                if (plan.ServiceId != serviceId) { throw new InvalidOperationException(); }
                // Filesystem ownership and actual service identity must be checked before decrypting anything.
                artifacts.ValidateOwner(plan);
                var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var binding in plan.Secrets)
                {
                    var ciphertext = artifacts.ReadSecret(binding.SecretId);
                    byte[]? plaintext = null;
                    try
                    {
                        plaintext = protector.Unprotect(ciphertext, binding);
                        decoded.Add(binding.ConfigurationKey, NativeSecretMaterial.Decode(binding, plaintext));
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(ciphertext);
                        if (plaintext is not null) { CryptographicOperations.ZeroMemory(plaintext); }
                    }
                }
                NativeSecretMaterial.ValidateRelationships(plan, decoded);
                foreach (var pair in plan.Settings) { values.Add(pair.Key, pair.Value); }
                foreach (var pair in decoded) { values.Add(pair.Key, pair.Value); }
                Data = values;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or
                UnauthorizedAccessException or CryptographicException or System.Security.SecurityException)
            {
                values.Clear(); Data.Clear();
                throw new InvalidOperationException("Native protected configuration cannot be loaded safely.");
            }
        }

        public void Dispose() => Data.Clear();
        public override string ToString() => "Native protected configuration provider (secret values omitted).";
    }
}
