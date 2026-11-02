using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.Shared.Hosting;

public interface INativeConfigurationArtifacts : IDisposable
{
    byte[] ReadDocument();
    void ValidateOwner(NativeServiceConfigurationPlan plan);
    byte[] ReadSecret(Guid reference);
}

public sealed class NativeConfigurationPackage : INativeConfigurationArtifacts
{
    private readonly Dictionary<Guid, byte[]> ciphertext;
    private bool disposed;
    private NativeConfigurationPackage(NativeServiceConfigurationPlan plan, Dictionary<Guid, byte[]> ciphertext)
    { Plan = plan; this.ciphertext = ciphertext; }

    public NativeServiceConfigurationPlan Plan { get; }

    public static NativeConfigurationPackage Create(NativeServiceConfigurationPlan plan,
        IReadOnlyDictionary<string, byte[]> inputs, INativeSecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(inputs); ArgumentNullException.ThrowIfNull(protector);
        var result = new Dictionary<Guid, byte[]>();
        try
        {
            if (inputs.Count != plan.Secrets.Count || inputs.Keys.Any(key => !plan.Secrets.Any(binding => binding.ConfigurationKey == key))) { throw Invalid(); }
            var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var binding in plan.Secrets)
            {
                if (!inputs.TryGetValue(binding.ConfigurationKey, out var input) || input is null) { throw Invalid(); }
                decoded.Add(binding.ConfigurationKey, NativeSecretMaterial.Decode(binding, input));
            }
            NativeSecretMaterial.ValidateRelationships(plan, decoded);
            foreach (var binding in plan.Secrets)
            { result.Add(binding.SecretId, protector.Protect(inputs[binding.ConfigurationKey], binding)); }
            return new(plan, result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or CryptographicException or FormatException)
        {
            foreach (var bytes in result.Values) { CryptographicOperations.ZeroMemory(bytes); }
            throw Invalid();
        }
    }

    public byte[] ReadDocument() { ObjectDisposedException.ThrowIf(disposed, this); return Plan.ExportDocument(); }
    public byte[] ReadSecret(Guid reference)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return ciphertext.TryGetValue(reference, out var bytes) ? bytes.ToArray() : throw Invalid();
    }

    public void ValidateOwner(NativeServiceConfigurationPlan plan)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (plan.InstallationId != Plan.InstallationId || plan.ServiceId != Plan.ServiceId || plan.ServiceSid != Plan.ServiceSid || plan.Revision != Plan.Revision)
        { throw Invalid(); }
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        foreach (var bytes in ciphertext.Values) { CryptographicOperations.ZeroMemory(bytes); }
        ciphertext.Clear();
    }

    public override string ToString() => "Native encrypted configuration package (secret values omitted).";
    private static InvalidOperationException Invalid() => new("Native configuration package is missing or invalid.");
}

internal static class NativeSecretMaterial
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static string Decode(NativeSecretBinding binding, ReadOnlySpan<byte> plaintext)
    {
        try
        {
            if (plaintext.Length is < 32 or > WindowsDpapiSecretProtector.MaximumPlaintextBytes) { throw Invalid(); }
            var value = Utf8.GetString(plaintext);
            if (value.Any(character => character is < '!' or > '~')) { throw Invalid(); }
            if (binding.ConfigurationKey.EndsWith(":Messaging:ConnectionString", StringComparison.Ordinal)) { ValidateBroker(binding.ServiceId, value); }
            else if (value.Length > 256) { throw Invalid(); }
            if (IsDataKey(binding.ConfigurationKey))
            {
                var key = Convert.FromBase64String(value);
                try { if (key.Length != 32 || Convert.ToBase64String(key) != value) { throw Invalid(); } }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            return value;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        { throw Invalid(); }
    }

    internal static void ValidateRelationships(NativeServiceConfigurationPlan plan, IReadOnlyDictionary<string, string> values)
    {
        foreach (var index in values.Keys.Where(key => key.EndsWith(":IndexKey", StringComparison.Ordinal)))
        {
            var prefix = index[..^"IndexKey".Length] + "EncryptionKeys:";
            var keys = values.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).Select(pair => pair.Value).ToArray();
            if (keys.Any(key => key == values[index]) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) { throw Invalid(); }
        }
        if (plan.ServiceId == "profile")
        {
            var readers = NativeConfigurationSettings.ProfileReaders.Select(reader => values["Profile:FinancialReaders:" + reader + ":SharedSecret"]).ToArray();
            if (readers.Distinct(StringComparer.Ordinal).Count() != readers.Length || readers.Contains(values["Profile:Gateway:SharedSecret"], StringComparer.Ordinal)) { throw Invalid(); }
        }
    }

    internal static bool IsDataKey(string key) => key.EndsWith(":IndexKey", StringComparison.Ordinal) || key.Contains(":EncryptionKeys:", StringComparison.Ordinal);

    private static void ValidateBroker(string serviceId, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("amqp" or "amqps") ||
            uri.Port is < 1024 or > 65535 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                IPAddress.TryParse(uri.Host.Trim('[', ']'), out var host) && IPAddress.IsLoopback(host))) { throw Invalid(); }
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || uri.UserInfo.Count(character => character == ':') != 1 ||
            Uri.UnescapeDataString(credentials[0]) != "fa-" + serviceId) { throw Invalid(); }
        var decodedPassword = Uri.UnescapeDataString(credentials[1]);
        var vhost = Uri.UnescapeDataString(uri.AbsolutePath.Length > 0 ? uri.AbsolutePath[1..] : string.Empty);
        if (decodedPassword.Length is < 32 or > 1024 || decodedPassword.Any(character => character is < '!' or > '~') ||
            !vhost.StartsWith("fa-", StringComparison.Ordinal) || vhost.Length > 96 ||
            vhost.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))) { throw Invalid(); }
    }

    private static InvalidOperationException Invalid() => new("Native protected secret material is invalid.");
}
