namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class NativeReceiptObjectOptions
{
    public string RootPath { get; init; } = "";
    public string ServiceSid { get; init; } = "";
    public string IndexKey { get; init; } = "";
    public string ActiveEncryptionKeyId { get; init; } = "";
    public Dictionary<string, string> EncryptionKeys { get; init; } = new(StringComparer.Ordinal);
    public int RetentionHours { get; init; }
    public int MaximumObjects { get; init; } = 1000;

    public void Validate()
    {
        if (RetentionHours is < 1 or > 24 || MaximumObjects is < 1 or > 9968 || string.IsNullOrWhiteSpace(RootPath)
            || string.IsNullOrWhiteSpace(ServiceSid) || EncryptionKeys.Count is < 1 or > 8
            || !EncryptionKeys.ContainsKey(ActiveEncryptionKeyId)
            || EncryptionKeys.Keys.Any(key => key.Length is < 1 or > 32 || key.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))))
        { throw new InvalidOperationException("Native receipt storage requires protected identity/keys and explicit bounded retention/capacity."); }
        try
        {
            var index = Convert.FromBase64String(IndexKey);
            try
            {
                if (index.Length != 32) { throw new FormatException(); }
                foreach (var value in EncryptionKeys.Values)
                {
                    var key = Convert.FromBase64String(value);
                    try { if (key.Length != 32 || System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(index, key)) { throw new FormatException(); } }
                    finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); }
                }
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(index); }
        }
        catch (FormatException) { throw new InvalidOperationException("Native receipt storage key configuration is invalid."); }
    }
}
