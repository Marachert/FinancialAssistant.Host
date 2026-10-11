using System.Security.Cryptography;

namespace FinancialAssistant.Shared.Persistence;

public sealed class NativePostgresSettings
{
    public string Database { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Password { get; set; } = string.Empty;
    public string IndexKey { get; set; } = string.Empty;
    public string ActiveEncryptionKeyId { get; set; } = string.Empty;
    public Dictionary<string, string> EncryptionKeys { get; set; } = new(StringComparer.Ordinal);

    public OwnedPostgresOptions CreateOptions(string serviceId) => new(new StorageIdentity(serviceId), Database, Port, Password);

    public RecordProtection CreateProtection()
    {
        var material = new List<byte[]>();
        try
        {
            if (EncryptionKeys is null) { throw new ArgumentException(); }
            var index = Convert.FromBase64String(IndexKey);
            material.Add(index);
            var keys = EncryptionKeys.ToDictionary(pair => pair.Key, pair =>
            {
                var key = Convert.FromBase64String(pair.Value); material.Add(key); return key;
            }, StringComparer.Ordinal);
            return new RecordProtection(index, ActiveEncryptionKeyId, keys);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { throw new InvalidOperationException("Native owned persistence keys are invalid."); }
        finally { foreach (var bytes in material) { CryptographicOperations.ZeroMemory(bytes); } }
    }
}
