using System.Text.Json;

namespace FinancialAssistant.Repository.Tests;

public sealed class ElasticsearchBootstrapTests
{
    private const string TemplatePath =
        "infra/elasticsearch/bootstrap/templates/identity-accounts-v1.json";
    private const string BootstrapPath =
        "infra/elasticsearch/bootstrap/bootstrap.ps1";
    private const string ReadmePath =
        "infra/elasticsearch/bootstrap/README.md";

    [Fact]
    public void SampleTemplate_UsesCanonicalIdentityAccountsContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var templateFile = ToRepositoryPath(repositoryRoot, TemplatePath);

        Assert.True(File.Exists(templateFile), $"Template file '{TemplatePath}' is missing.");

        using var document = JsonDocument.Parse(File.ReadAllText(templateFile));
        var root = document.RootElement;

        Assert.Equal(
            "fa-local-identity-accounts-v1-*",
            root.GetProperty("index_patterns")[0].GetString());
        Assert.Equal(100, root.GetProperty("priority").GetInt32());
        Assert.Equal(1, root.GetProperty("version").GetInt32());

        var metadata = root.GetProperty("_meta");
        Assert.Equal("identity", metadata.GetProperty("owner").GetString());
        Assert.Equal("accounts", metadata.GetProperty("entity").GetString());
        Assert.Equal(1, metadata.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("FIN-60", metadata.GetProperty("jira").GetString());

        var template = root.GetProperty("template");
        var indexSettings = template
            .GetProperty("settings")
            .GetProperty("index");
        Assert.Equal(1, indexSettings.GetProperty("number_of_shards").GetInt32());
        Assert.Equal(0, indexSettings.GetProperty("number_of_replicas").GetInt32());

        var mappings = template.GetProperty("mappings");
        Assert.Equal("strict", mappings.GetProperty("dynamic").GetString());

        var properties = mappings.GetProperty("properties");
        AssertMapping(properties, "id", "keyword");
        AssertMapping(properties, "status", "keyword");
        AssertMapping(properties, "roles", "keyword");
        AssertMapping(properties, "createdAtUtc", "date");
        AssertMapping(properties, "updatedAtUtc", "date");
        AssertMapping(properties, "deletedAtUtc", "date");
        AssertMapping(properties, "schemaVersion", "integer");
    }

    [Fact]
    public void Bootstrap_IsNonInteractiveIdempotentAndVerifiesAliases()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bootstrap = File.ReadAllText(ToRepositoryPath(repositoryRoot, BootstrapPath));

        var requiredPhrases = new[]
        {
            "$ErrorActionPreference = \"Stop\"",
            "Set-StrictMode -Version Latest",
            "$service = \"identity\"",
            "$entity = \"accounts\"",
            "$environment = \"local\"",
            "$schemaVersion = 1",
            "$generation = 1",
            "\"{0}-v{1}-{2:D6}\"",
            "$readAlias = \"$prefix-read\"",
            "$writeAlias = \"$prefix-write\"",
            "is_write_index = $true",
            "Test-ElasticsearchResource",
            "-Path \"_aliases\"",
            "-Path \"_index_template/$templateName\"",
            "-Path \"_alias/$readAlias\"",
            "-Path \"_alias/$writeAlias\"",
            "Result        = \"verified\""
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, bootstrap, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Remove-Item", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-Method Delete", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Get-Random", bootstrap, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New-Guid", bootstrap, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BootstrapDocumentation_ExplainsRunRerunAndVerification()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = File.ReadAllText(ToRepositoryPath(repositoryRoot, ReadmePath));
        var infrastructureReadme = File.ReadAllText(
            ToRepositoryPath(repositoryRoot, "infra/README.md"));
        var composeReadme = File.ReadAllText(
            ToRepositoryPath(repositoryRoot, "infra/docker-compose/README.md"));

        var requiredPhrases = new[]
        {
            "pwsh -NoProfile -NonInteractive -File infra/elasticsearch/bootstrap/bootstrap.ps1",
            "Running the same command repeatedly is safe",
            "fa-local-identity-accounts-template-v1",
            "fa-local-identity-accounts-v1-000001",
            "fa-local-identity-accounts-read",
            "fa-local-identity-accounts-write",
            "dynamic: strict",
            "is_write_index: true",
            "Use synthetic data"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, readme, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "elasticsearch/bootstrap/README.md",
            infrastructureReadme,
            StringComparison.Ordinal);
        Assert.Contains(
            "../elasticsearch/bootstrap/README.md",
            composeReadme,
            StringComparison.Ordinal);
    }

    private static void AssertMapping(
        JsonElement properties,
        string propertyName,
        string expectedType)
    {
        Assert.Equal(
            expectedType,
            properties.GetProperty(propertyName).GetProperty("type").GetString());
    }

    private static string ToRepositoryPath(string repositoryRoot, string path) =>
        Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));

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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
