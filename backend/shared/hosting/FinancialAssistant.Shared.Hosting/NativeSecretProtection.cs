using System.Security.Cryptography;
using System.Text.Json;

namespace FinancialAssistant.Shared.Hosting;

public sealed class NativeSecretBinding
{
    private NativeSecretBinding(Guid installationId, string serviceId, string serviceSid, string configurationKey, Guid secretId)
    { InstallationId = installationId; ServiceId = serviceId; ServiceSid = serviceSid; ConfigurationKey = configurationKey; SecretId = secretId; }

    public Guid InstallationId { get; }
    public string ServiceId { get; }
    public string ServiceSid { get; }
    public string ConfigurationKey { get; }
    public Guid SecretId { get; }

    public static NativeSecretBinding Create(Guid installationId, string serviceId, string serviceSid, string configurationKey, Guid secretId)
    {
        if (installationId == Guid.Empty || secretId == Guid.Empty || !NativeServiceCatalog.IsVirtualServiceSid(serviceSid) ||
            configurationKey is null || !IsOwnedSecretKey(serviceId, configurationKey))
        { throw new ArgumentException("Native secret binding is invalid."); }
        return new(installationId, serviceId, serviceSid, configurationKey, secretId);
    }

    internal static bool IsOwnedSecretKey(string serviceId, string key)
    {
        var service = NativeServiceCatalog.Services.SingleOrDefault(item => item.Id == serviceId);
        if (service is null) { return false; }
        if (service.RequiredSecretKeys.Contains(key, StringComparer.Ordinal)) { return true; }
        var passwordKey = service.RequiredSecretKeys.FirstOrDefault(item => item.EndsWith(":Storage:Postgres:Password", StringComparison.Ordinal));
        if (passwordKey is not null)
        {
            var prefix = passwordKey[..^"Password".Length] + "EncryptionKeys:";
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            { return ValidKeyId(key[prefix.Length..]); }
        }
        var prefixName = service.RequiredSecretKeys[0].Split(':')[0];
        if (new[] { "income", "expense", "transaction-intake", "analytics", "financial-score", "recommendations-notifications", "audit" }.Contains(serviceId) &&
            key == prefixName + ":Messaging:ConnectionString") { return true; }
        if (new[] { "analytics", "financial-score", "recommendations-notifications" }.Contains(serviceId) &&
            key == prefixName + ":Profile:SharedSecret") { return true; }
        if (serviceId == "profile" && new[] { "analytics", "financial-score", "recommendations-notifications" }
            .Any(reader => key == "Profile:FinancialReaders:" + reader + ":SharedSecret")) { return true; }
        if (serviceId == "receipt-processing" && key.StartsWith("ReceiptProcessing:Storage:Files:EncryptionKeys:", StringComparison.Ordinal))
        { return ValidKeyId(key["ReceiptProcessing:Storage:Files:EncryptionKeys:".Length..]); }
        return false;
    }

    internal static bool ValidKeyId(string value) => value.Length is >= 1 and <= 32 &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    internal byte[] CreateEntropy() => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        purpose = "FinancialAssistant.NativeSecret.v1",
        installation = InstallationId.ToString("N"),
        service = ServiceId,
        sid = ServiceSid,
        setting = ConfigurationKey,
        reference = SecretId.ToString("N")
    }));
}

public interface INativeSecretProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext, NativeSecretBinding binding);
    byte[] Unprotect(ReadOnlySpan<byte> ciphertext, NativeSecretBinding binding);
}

public sealed class WindowsDpapiSecretProtector : INativeSecretProtector
{
    public const int MaximumPlaintextBytes = 8192;
    public const int MaximumCiphertextBytes = 16384;

    public byte[] Protect(ReadOnlySpan<byte> plaintext, NativeSecretBinding binding) => Transform(plaintext, binding, protect: true);
    public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, NativeSecretBinding binding) => Transform(ciphertext, binding, protect: false);

    private static byte[] Transform(ReadOnlySpan<byte> input, NativeSecretBinding binding, bool protect)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (input.Length < 1 || input.Length > (protect ? MaximumPlaintextBytes : MaximumCiphertextBytes))
        { throw new ArgumentException("Native secret payload size is invalid."); }
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("Native secret protection requires Windows DPAPI."); }
        var copy = input.ToArray();
        var entropy = binding.CreateEntropy();
        byte[]? output = null;
        try
        {
            // Machine DPAPI is not service isolation: the store must enforce identity and ACLs before unprotecting.
            output = protect ? ProtectedData.Protect(copy, entropy, DataProtectionScope.LocalMachine) :
                ProtectedData.Unprotect(copy, entropy, DataProtectionScope.LocalMachine);
            if (output.Length < 1 || output.Length > (protect ? MaximumCiphertextBytes : MaximumPlaintextBytes))
            { throw new CryptographicException(); }
            return output;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            if (output is not null) { CryptographicOperations.ZeroMemory(output); }
            throw new InvalidOperationException("Native secret protection or binding validation failed.");
        }
        finally { CryptographicOperations.ZeroMemory(copy); CryptographicOperations.ZeroMemory(entropy); }
    }
}
