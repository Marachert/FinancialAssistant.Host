using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialAssistant.Shared.Hosting;

public sealed class NativeConfigurationDocument
{
    public int SchemaVersion { get; init; }
    public Guid InstallationId { get; init; }
    public string ServiceId { get; init; } = "";
    public string ServiceSid { get; init; } = "";
    public Guid Revision { get; init; }
    public List<NativeConfigurationSetting> Settings { get; init; } = new();
    public List<NativeConfigurationReference> SecretReferences { get; init; } = new();
}

public sealed class NativeConfigurationSetting
{
    public string Key { get; init; } = "";
    public JsonElement Value { get; init; }
    public static NativeConfigurationSetting Text(string key, string value) => new() { Key = key, Value = JsonSerializer.SerializeToElement(value) };
    public static NativeConfigurationSetting Number(string key, int value) => new() { Key = key, Value = JsonSerializer.SerializeToElement(value) };
    public static NativeConfigurationSetting Flag(string key, bool value) => new() { Key = key, Value = JsonSerializer.SerializeToElement(value) };
}

public sealed class NativeConfigurationReference
{
    public string Key { get; init; } = "";
    public Guid Id { get; init; }
}

public sealed class NativeServiceConfigurationPlan
{
    public const int MaximumDocumentBytes = 65536;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };
    private readonly byte[] document;

    private NativeServiceConfigurationPlan(NativeConfigurationDocument model, byte[] document,
        Dictionary<string, string?> settings, NativeSecretBinding[] secrets)
    {
        InstallationId = model.InstallationId; ServiceId = model.ServiceId; ServiceSid = model.ServiceSid; Revision = model.Revision;
        this.document = document; Settings = new ReadOnlyDictionary<string, string?>(settings); Secrets = Array.AsReadOnly(secrets);
    }

    public Guid InstallationId { get; }
    public string ServiceId { get; }
    public string ServiceSid { get; }
    public Guid Revision { get; }
    public IReadOnlyDictionary<string, string?> Settings { get; }
    public IReadOnlyList<NativeSecretBinding> Secrets { get; }
    public byte[] ExportDocument() => document.ToArray();

    public static NativeServiceConfigurationPlan Create(NativeConfigurationDocument model)
    {
        try { return Parse(JsonSerializer.SerializeToUtf8Bytes(model, Json)); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        { throw Invalid(); }
    }

    public static NativeServiceConfigurationPlan Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            if (utf8.Length is < 2 or > MaximumDocumentBytes) { throw Invalid(); }
            using var parsed = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicateProperties(parsed.RootElement);
            var model = JsonSerializer.Deserialize<NativeConfigurationDocument>(utf8, Json) ?? throw Invalid();
            if (model.SchemaVersion != 1 || model.InstallationId == Guid.Empty || model.Revision == Guid.Empty ||
                !NativeServiceCatalog.Services.Any(service => service.Id == model.ServiceId) || !NativeServiceCatalog.IsVirtualServiceSid(model.ServiceSid) ||
                model.Settings is null || model.SecretReferences is null || model.Settings.Count > 96 || model.SecretReferences.Count is < 1 or > 64)
            { throw Invalid(); }
            var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in model.Settings)
            {
                if (setting is null || string.IsNullOrEmpty(setting.Key) || !allKeys.Add(setting.Key)) { throw Invalid(); }
                settings.Add(setting.Key, NativeConfigurationSettings.Validate(model.ServiceId, setting.Key, setting.Value));
            }
            var ids = new HashSet<Guid>();
            var secrets = new List<NativeSecretBinding>();
            foreach (var reference in model.SecretReferences)
            {
                if (reference is null || string.IsNullOrEmpty(reference.Key) || !allKeys.Add(reference.Key) || !ids.Add(reference.Id)) { throw Invalid(); }
                secrets.Add(NativeSecretBinding.Create(model.InstallationId, model.ServiceId, model.ServiceSid, reference.Key, reference.Id));
            }
            NativeConfigurationSettings.Complete(model.ServiceId, model.ServiceSid, settings, secrets);
            var canonical = JsonSerializer.SerializeToUtf8Bytes(model, Json);
            if (canonical.Length > MaximumDocumentBytes) { throw Invalid(); }
            return new(model, canonical, settings, secrets.OrderBy(secret => secret.ConfigurationKey, StringComparer.Ordinal).ToArray());
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        { throw Invalid(); }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw Invalid(); }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { foreach (var item in element.EnumerateArray()) { RejectDuplicateProperties(item); } }
    }

    private static InvalidOperationException Invalid() => new("Native protected configuration document is invalid.");
}
