using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MobileStoreReleaseTests
{
    [Fact]
    public void AppConfig_UsesStableStoreIdentityAndMinimalPermissions()
    {
        using var appDocument = ReadJson("mobile/app-react-native/app.json");
        var expo = appDocument.RootElement.GetProperty("expo");
        var ios = expo.GetProperty("ios");
        var android = expo.GetProperty("android");
        var imagePicker = expo.GetProperty("plugins")
            .EnumerateArray()
            .Single(plugin => plugin.ValueKind == JsonValueKind.Array && plugin[0].GetString() == "expo-image-picker");

        Assert.Equal("Financial Assistant", expo.GetProperty("name").GetString());
        Assert.Equal("com.financialassistant.mobile", ios.GetProperty("bundleIdentifier").GetString());
        Assert.True(int.Parse(ios.GetProperty("buildNumber").GetString()!) > 0);
        Assert.False(ios.GetProperty("config").GetProperty("usesNonExemptEncryption").GetBoolean());
        Assert.Equal("com.financialassistant.mobile", android.GetProperty("package").GetString());
        Assert.True(android.GetProperty("versionCode").GetInt32() > 0);
        Assert.False(imagePicker[1].GetProperty("microphonePermission").GetBoolean());
    }

    [Fact]
    public void EasConfig_KeepsStoreBuildsAndPlaySubmissionControlled()
    {
        using var easDocument = ReadJson("mobile/app-react-native/eas.json");
        var root = easDocument.RootElement;
        var productionBuild = root.GetProperty("build").GetProperty("production");
        var androidSubmit = root.GetProperty("submit").GetProperty("production").GetProperty("android");

        Assert.Equal("22.4.0", root.GetProperty("cli").GetProperty("version").GetString());
        Assert.True(root.GetProperty("cli").GetProperty("requireCommit").GetBoolean());
        Assert.Equal("store", productionBuild.GetProperty("distribution").GetString());
        Assert.Equal("app-bundle", productionBuild.GetProperty("android").GetProperty("buildType").GetString());
        Assert.Equal("internal", androidSubmit.GetProperty("track").GetString());
        Assert.Equal("draft", androidSubmit.GetProperty("releaseStatus").GetString());
    }

    [Fact]
    public void ReleaseEvidence_IsCandidateBoundAndCredentialsStayLocal()
    {
        using var templateDocument = ReadJson("mobile/app-react-native/store/console-records.example.json");
        var template = templateDocument.RootElement;
        var candidate = template.GetProperty("candidate");
        var gitIgnore = ReadRequiredFile(".gitignore");
        var easIgnore = ReadRequiredFile("mobile/app-react-native/.easignore");
        var validator = ReadRequiredFile("mobile/app-react-native/scripts/verify-store-release.mjs");

        Assert.Equal("0.1.0", candidate.GetProperty("version").GetString());
        Assert.Equal("1", candidate.GetProperty("iosBuildNumber").GetString());
        Assert.Equal(1, candidate.GetProperty("androidVersionCode").GetInt32());
        Assert.StartsWith("REQUIRED_", candidate.GetProperty("commitSha").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("REQUIRED_", candidate.GetProperty("treeSha").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("REQUIRED_", template.GetProperty("productionApiUrl").GetString(), StringComparison.Ordinal);
        Assert.False(template.GetProperty("apple").GetProperty("appRecordCreated").GetBoolean());
        Assert.False(template.GetProperty("googlePlay").GetProperty("appRecordCreated").GetBoolean());
        Assert.Contains("console-records.local.json", gitIgnore, StringComparison.Ordinal);
        Assert.Contains("service-account*.json", gitIgnore, StringComparison.Ordinal);
        Assert.Contains("auth-key*.p8", easIgnore, StringComparison.Ordinal);
        Assert.Contains("template.candidate?.commitSha.startsWith('REQUIRED_')", validator, StringComparison.Ordinal);
        Assert.Contains("template.googlePlay?.serviceAccountKeyPath.startsWith('REQUIRED_')", validator, StringComparison.Ordinal);
        Assert.Contains("local.candidate?.version === app.version", validator, StringComparison.Ordinal);
        Assert.Contains("local.candidate?.iosBuildNumber === app.ios.buildNumber", validator, StringComparison.Ordinal);
        Assert.Contains("local.candidate?.androidVersionCode === app.android.versionCode", validator, StringComparison.Ordinal);
        Assert.Contains("local.candidate?.commitSha === gitCandidate.commitSha", validator, StringComparison.Ordinal);
        Assert.Contains("local.candidate?.treeSha === gitCandidate.treeSha", validator, StringComparison.Ordinal);
        Assert.Contains("await requireReadableFile(resolvedServiceAccountPath", validator, StringComparison.Ordinal);
        Assert.Contains("outside the repository", validator, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivacyAndMetadata_AreConservativeAndLinkedFromSettings()
    {
        using var disclosuresDocument = ReadJson("mobile/app-react-native/store/privacy-disclosures.json");
        using var metadataDocument = ReadJson("mobile/app-react-native/store/release-metadata.json");
        var disclosures = disclosuresDocument.RootElement;
        var urls = metadataDocument.RootElement.GetProperty("urls");
        var settings = ReadRequiredFile("mobile/app-react-native/src/app/(app)/settings.tsx");

        Assert.False(disclosures.GetProperty("tracking").GetBoolean());
        Assert.False(disclosures.GetProperty("thirdPartyAdvertising").GetBoolean());
        Assert.False(disclosures.GetProperty("dataSharedWithThirdParties").GetBoolean());
        Assert.All(disclosures.GetProperty("googlePlay").EnumerateArray(), item => Assert.False(item.GetProperty("shared").GetBoolean()));
        Assert.StartsWith("https://", urls.GetProperty("privacyPolicy").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("https://", urls.GetProperty("support").GetString(), StringComparison.Ordinal);
        Assert.Contains(urls.GetProperty("privacyPolicy").GetString()!, settings, StringComparison.Ordinal);
        Assert.Contains(urls.GetProperty("support").GetString()!, settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Runbook_SeparatesRepositoryReadinessFromPaidOwnerActions()
    {
        var package = ReadRequiredFile("mobile/app-react-native/package.json");
        var runbook = ReadRequiredFile("docs/delivery/mobile-store-release-tracks.md");

        Assert.Contains("verify:release", package, StringComparison.Ordinal);
        Assert.Contains("--strict", runbook, StringComparison.Ordinal);
        Assert.Contains("increment `expo.version`", runbook, StringComparison.Ordinal);
        Assert.Contains("candidate.commitSha", runbook, StringComparison.Ordinal);
        Assert.Contains("existing readable file", runbook, StringComparison.Ordinal);
        Assert.Contains("explicit release-owner approval", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not create cloud builds", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prevents unapproved spend", runbook, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadJson(string path) => JsonDocument.Parse(ReadRequiredFile(path));

    private static string ReadRequiredFile(string path)
    {
        var fullPath = Path.Combine(FindRepositoryRoot(), path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required mobile store release asset is missing: {path}");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Financial Assistant repository root.");
    }
}
