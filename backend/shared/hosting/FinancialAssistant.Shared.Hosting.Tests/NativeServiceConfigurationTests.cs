using System.Text;
using System.Text.Json.Nodes;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeServiceConfigurationTests
{
    public static IEnumerable<object[]> Hosts => NativeServiceCatalog.Services.Select(service => new object[] { service.Id });

    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryInstalledHostHasStrictNonSecretSettingsAndOpaqueReferences(string serviceId)
    {
        var plan = NativeServiceConfigurationPlan.Create(Document(serviceId));
        Assert.Equal(serviceId, plan.ServiceId);
        Assert.Equal("true", plan.Settings["NativeDiagnostics:Enabled"]);
        Assert.Equal("false", plan.Settings["AiOrchestration:Provider:Enabled"]);
        Assert.Equal("false", plan.Settings["ReceiptProcessing:Ocr:Enabled"]);
        Assert.Equal("false", plan.Settings["Identity:Providers:Phone:Enabled"]);
        Assert.DoesNotContain(plan.Settings.Keys, key => NativeSecretBinding.IsOwnedSecretKey(serviceId, key));
        Assert.Equal(plan.Secrets.Count, plan.Secrets.Select(secret => secret.SecretId).Distinct().Count());
        var again = NativeServiceConfigurationPlan.Parse(plan.ExportDocument());
        Assert.Equal(plan.Revision, again.Revision);
        Assert.Equal(plan.Settings.OrderBy(pair => pair.Key), again.Settings.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void PlanIsAnImmutableSnapshotNotAMutableOperatorObject()
    {
        var source = Document("gateway"); var plan = NativeServiceConfigurationPlan.Create(source);
        source.Settings.Clear(); source.SecretReferences.Clear();
        var exported = plan.ExportDocument(); exported[0] = 0;
        Assert.Equal(2, plan.Secrets.Count);
        Assert.Equal("https://localhost:7443", plan.Settings["Gateway:NativeHttps:PublicOrigin"]);
        Assert.Equal(plan.Revision, NativeServiceConfigurationPlan.Parse(plan.ExportDocument()).Revision);
    }

    [Theory]
    [InlineData("unknown-root")]
    [InlineData("unknown-nested")]
    [InlineData("plaintext-secret")]
    [InlineData("cross-service-secret")]
    [InlineData("duplicate-reference")]
    [InlineData("duplicate-key")]
    [InlineData("missing-secret")]
    [InlineData("wrong-type")]
    [InlineData("paid-provider")]
    [InlineData("secret-path")]
    public void InvalidDocumentsFailWithoutEchoingPrivateInputs(string mutation)
    {
        var node = JsonNode.Parse(NativeServiceConfigurationPlan.Create(Document("gateway")).ExportDocument())!.AsObject();
        const string sensitive = "synthetic-private-operator-value";
        switch (mutation)
        {
            case "unknown-root": node["unknown"] = sensitive; break;
            case "unknown-nested": node["secretReferences"]![0]!["password"] = sensitive; break;
            case "plaintext-secret": node["settings"]![0]!["key"] = "Gateway:Security:AccessTokenSigningKey"; node["settings"]![0]!["value"] = sensitive; break;
            case "cross-service-secret": node["secretReferences"]![0]!["key"] = "Identity:Storage:Postgres:Password"; break;
            case "duplicate-reference": node["secretReferences"]![1]!["id"] = node["secretReferences"]![0]!["id"]!.DeepClone(); break;
            case "duplicate-key": node["settings"]!.AsArray().Add(node["settings"]![0]!.DeepClone()); break;
            case "missing-secret": node["secretReferences"]!.AsArray().RemoveAt(0); break;
            case "wrong-type": node["settings"]![1]!["value"] = sensitive; break;
            case "paid-provider": node["settings"]!.AsArray().Add(new JsonObject { ["key"] = "AiOrchestration:Provider:Enabled", ["value"] = true }); break;
            case "secret-path": node["settings"]![0]!["value"] = "C:\\synthetic-private-operator-value\\..\\escape"; break;
        }
        var error = Assert.Throws<InvalidOperationException>(() => NativeServiceConfigurationPlan.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(sensitive, error.ToString());
    }

    [Fact]
    public void DuplicateJsonPropertiesAndOversizedDocumentsAreRejected()
    {
        var json = Encoding.UTF8.GetString(NativeServiceConfigurationPlan.Create(Document("gateway")).ExportDocument());
        var duplicate = json.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => NativeServiceConfigurationPlan.Parse(Encoding.UTF8.GetBytes(duplicate)));
        Assert.Throws<InvalidOperationException>(() => NativeServiceConfigurationPlan.Parse(new byte[65537]));
    }

    [Fact]
    public void MissingActiveKeyAndExcessRetainedKeysDoNotProduceAUsablePlan()
    {
        var model = Document("income");
        model.SecretReferences.RemoveAll(reference => reference.Key.EndsWith(":EncryptionKeys:v1", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => NativeServiceConfigurationPlan.Create(model));
        model = Document("income");
        for (var index = 2; index <= 9; index++)
        { model.SecretReferences.Add(new() { Key = "Income:Storage:Postgres:EncryptionKeys:v" + index, Id = Guid.NewGuid() }); }
        Assert.Throws<InvalidOperationException>(() => NativeServiceConfigurationPlan.Create(model));
    }

    internal static NativeConfigurationDocument Document(string serviceId)
    {
        var service = NativeServiceCatalog.Get(serviceId);
        var prefix = NativeConfigurationSettings.Prefix(serviceId);
        var settings = new List<NativeConfigurationSetting>
        {
            NativeConfigurationSetting.Text("NativeDiagnostics:RootPath", "C:\\ProgramData\\FinancialAssistant\\" + serviceId + "\\diagnostics"),
            NativeConfigurationSetting.Number("NativeDiagnostics:RetentionHours", 24)
        };
        var keys = service.RequiredSecretKeys.ToList();
        if (NativeConfigurationSettings.HasPostgres(serviceId))
        {
            settings.Add(NativeConfigurationSetting.Text(prefix + ":Storage:Postgres:Database", "fa_synthetic"));
            settings.Add(NativeConfigurationSetting.Number(prefix + ":Storage:Postgres:Port", 5432));
            settings.Add(NativeConfigurationSetting.Text(prefix + ":Storage:Postgres:ActiveEncryptionKeyId", "v1"));
            keys.Add(prefix + ":Storage:Postgres:EncryptionKeys:v1");
        }
        if (NativeConfigurationSettings.BrokerServices.Contains(serviceId)) { keys.Add(prefix + ":Messaging:ConnectionString"); }
        if (NativeConfigurationSettings.ProfileReaders.Contains(serviceId)) { keys.Add(prefix + ":Profile:SharedSecret"); }
        if (serviceId == "profile") { keys.AddRange(NativeConfigurationSettings.ProfileReaders.Select(reader => "Profile:FinancialReaders:" + reader + ":SharedSecret")); }
        if (serviceId == "receipt-processing")
        {
            settings.Add(NativeConfigurationSetting.Text("ReceiptProcessing:Storage:Files:RootPath", "C:\\ProgramData\\FinancialAssistant\\receipts"));
            settings.Add(NativeConfigurationSetting.Number("ReceiptProcessing:Storage:Files:RetentionHours", 24));
            settings.Add(NativeConfigurationSetting.Text("ReceiptProcessing:Storage:Files:ActiveEncryptionKeyId", "v1"));
            keys.Add("ReceiptProcessing:Storage:Files:EncryptionKeys:v1");
        }
        if (serviceId == "gateway")
        {
            settings.Add(NativeConfigurationSetting.Text("Gateway:NativeHttps:PublicOrigin", "https://localhost:7443"));
            settings.Add(NativeConfigurationSetting.Text("Gateway:NativeHttps:CertificateSha256", new string('A', 64)));
        }
        return new NativeConfigurationDocument
        {
            SchemaVersion = 1,
            InstallationId = Guid.NewGuid(),
            Revision = Guid.NewGuid(),
            ServiceId = serviceId,
            ServiceSid = "S-1-5-80-1-2-3-4-5",
            Settings = settings,
            SecretReferences = keys.Select(key => new NativeConfigurationReference { Key = key, Id = Guid.NewGuid() }).ToList()
        };
    }
}
