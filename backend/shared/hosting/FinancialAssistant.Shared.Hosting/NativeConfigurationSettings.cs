using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Shared.Hosting;

internal static class NativeConfigurationSettings
{
    internal static readonly string[] BrokerServices = ["income", "expense", "transaction-intake", "analytics", "financial-score", "recommendations-notifications", "audit"];
    internal static readonly string[] ProfileReaders = ["analytics", "financial-score", "recommendations-notifications"];

    internal static string Prefix(string serviceId) => NativeServiceCatalog.Get(serviceId).RequiredSecretKeys[0].Split(':')[0];
    internal static bool HasPostgres(string serviceId) => NativeServiceCatalog.Get(serviceId).RequiredSecretKeys.Any(key => key.EndsWith(":Storage:Postgres:Password", StringComparison.Ordinal));

    internal static string Validate(string serviceId, string key, JsonElement value)
    {
        var prefix = Prefix(serviceId);
        if (key == "NativeDiagnostics:RootPath") { return LocalPath(value); }
        if (key == "NativeDiagnostics:RetentionHours") { return Integer(value, 1, 24); }
        if (key == "NativeDiagnostics:MaximumBatches") { return Integer(value, 4, 128); }
        if (HasPostgres(serviceId))
        {
            if (key == prefix + ":Storage:Postgres:Database")
            {
                var database = Name(value, 63);
                if (database is "postgres" or "template0" or "template1") { throw Invalid(); }
                return database;
            }
            if (key == prefix + ":Storage:Postgres:Port") { return Integer(value, 1024, 65535); }
            if (key == prefix + ":Storage:Postgres:ActiveEncryptionKeyId") { return KeyId(value); }
        }
        if (serviceId is "identity" or "gateway")
        {
            var authentication = serviceId == "identity" ? "Identity:Authentication:" : "Gateway:Security:";
            if (key == authentication + "AccessTokenIssuer" || key == authentication + "AccessTokenAudience") { return Text(value, 200); }
        }
        if (serviceId == "identity")
        {
            if (key == "Identity:Authentication:AccessTokenLifetimeMinutes") { return Integer(value, 1, 60); }
            if (key == "Identity:Authentication:RefreshTokenLifetimeDays") { return Integer(value, 1, 30); }
        }
        if (serviceId == "receipt-processing")
        {
            if (key == "ReceiptProcessing:Storage:Files:RootPath") { return LocalPath(value); }
            if (key == "ReceiptProcessing:Storage:Files:RetentionHours") { return Integer(value, 1, 24); }
            if (key == "ReceiptProcessing:Storage:Files:MaximumObjects") { return Integer(value, 1, 9968); }
            if (key == "ReceiptProcessing:Storage:Files:ActiveEncryptionKeyId") { return KeyId(value); }
        }
        if (serviceId == "gateway" && key.StartsWith("Gateway:NativeHttps:", StringComparison.Ordinal))
        {
            return key["Gateway:NativeHttps:".Length..] switch
            {
                "PublicOrigin" => Text(value, 512),
                "BindAddress" => Text(value, 64),
                "CertificateSha256" => Text(value, 64),
                "PublicExposureApproved" => Boolean(value),
                "MaximumRequestBodyBytes" => Integer(value, 65536, 12582912),
                "MaximumRequestHeadersBytes" => Integer(value, 8192, 32768),
                "RequestHeadersTimeoutSeconds" => Integer(value, 5, 30),
                "MinimumCertificateDays" => Integer(value, 1, 90),
                _ => throw Invalid()
            };
        }
        throw Invalid();
    }

    internal static void Complete(string serviceId, string sid, Dictionary<string, string?> values, IReadOnlyList<NativeSecretBinding> secrets)
    {
        var prefix = Prefix(serviceId);
        Require(values, "NativeDiagnostics:RootPath"); Require(values, "NativeDiagnostics:RetentionHours");
        values.Add("NativeDiagnostics:Enabled", "true"); values.Add("NativeDiagnostics:ServiceSid", sid);
        foreach (var key in new[] { "AiOrchestration:Provider:Enabled", "ReceiptProcessing:Ocr:Enabled", "Identity:Providers:Phone:Enabled" })
        { values.Add(key, "false"); }
        var required = NativeServiceCatalog.Get(serviceId).RequiredSecretKeys.ToList();
        if (HasPostgres(serviceId))
        {
            Require(values, prefix + ":Storage:Postgres:Database"); Require(values, prefix + ":Storage:Postgres:Port");
            RequireKeySet(prefix + ":Storage:Postgres:", values, secrets);
            values.Add(prefix + ":Storage:Provider", "PostgreSQL");
        }
        if (BrokerServices.Contains(serviceId, StringComparer.Ordinal))
        { values.Add(prefix + ":Messaging:Enabled", "true"); required.Add(prefix + ":Messaging:ConnectionString"); }
        if (ProfileReaders.Contains(serviceId, StringComparer.Ordinal)) { required.Add(prefix + ":Profile:SharedSecret"); }
        if (serviceId == "profile")
        {
            values.Add("Profile:FinancialReaders:Enabled", "true");
            required.AddRange(ProfileReaders.Select(reader => "Profile:FinancialReaders:" + reader + ":SharedSecret"));
        }
        if (serviceId == "identity")
        {
            values.Add("Identity:Authentication:RuntimeAdapter", "NativePostgres");
            values.Add("Identity:Events:Mode", "DurableOutboxPendingTransport");
            values.Add("Identity:Providers:Phone:Adapter", "Disabled");
        }
        if (serviceId == "receipt-processing")
        {
            Require(values, "ReceiptProcessing:Storage:Files:RootPath"); Require(values, "ReceiptProcessing:Storage:Files:RetentionHours");
            RequireKeySet("ReceiptProcessing:Storage:Files:", values, secrets);
            values.Add("ReceiptProcessing:Storage:Files:ServiceSid", sid);
        }
        if (required.Any(key => !secrets.Any(binding => binding.ConfigurationKey == key))) { throw Invalid(); }
        if (serviceId == "gateway")
        {
            values.Add("Gateway:NativeHttps:Enabled", "true"); values.Add("Gateway:NativeHttps:ServiceSid", sid);
            _ = NativeHttpsPlan.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build()) ?? throw Invalid();
        }
    }

    private static void RequireKeySet(string prefix, IReadOnlyDictionary<string, string?> values, IReadOnlyList<NativeSecretBinding> secrets)
    {
        var active = Require(values, prefix + "ActiveEncryptionKeyId");
        var keys = secrets.Where(binding => binding.ConfigurationKey.StartsWith(prefix + "EncryptionKeys:", StringComparison.Ordinal)).ToArray();
        if (keys.Length is < 1 or > 8 || !keys.Any(binding => binding.ConfigurationKey == prefix + "EncryptionKeys:" + active)) { throw Invalid(); }
    }

    private static string Require(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : throw Invalid();

    private static string Text(JsonElement value, int maximum)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || text.Length < 1 ||
            text.Length > maximum || text.Any(char.IsControl)) { throw Invalid(); }
        return text;
    }

    private static string Integer(JsonElement value, int minimum, int maximum) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= minimum && number <= maximum
            ? number.ToString(CultureInfo.InvariantCulture) : throw Invalid();

    private static string Boolean(JsonElement value) => value.ValueKind switch
    { JsonValueKind.True => "true", JsonValueKind.False => "false", _ => throw Invalid() };

    private static string Name(JsonElement value, int maximum)
    {
        var name = Text(value, maximum);
        if (!name.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) { throw Invalid(); }
        return name;
    }

    private static string KeyId(JsonElement value)
    { var id = Text(value, 32); return NativeSecretBinding.ValidKeyId(id) ? id : throw Invalid(); }

    private static string LocalPath(JsonElement value)
    {
        var path = Text(value, 2048);
        if (path.Length < 4 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] is not ('\\' or '/') ||
            path.IndexOf(':', 2) >= 0 || path.Split(['\\', '/']).Any(segment => segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ')))
        { throw Invalid(); }
        return path;
    }

    private static InvalidOperationException Invalid() => new("Native protected configuration settings are missing or invalid.");
}
