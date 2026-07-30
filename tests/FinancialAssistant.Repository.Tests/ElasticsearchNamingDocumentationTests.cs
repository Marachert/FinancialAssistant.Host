using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class ElasticsearchNamingDocumentationTests
{
    [Fact]
    public void NamingGuide_DefinesOwnershipVersioningAndAliases()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(
            repositoryRoot,
            "docs/engineering/elasticsearch-index-naming.md");

        var normalizedGuide = NormalizeDocumentation(guide);

        var requiredPhrases = new[]
        {
            "Only the owning service may read or write its indices",
            "[a-z0-9]+(?:-[a-z0-9]+)*",
            "fa-{environment}-{service}-{entity}-v{schemaVersion}-{generation}",
            "fa-{environment}-{service}-{entity}-read",
            "fa-{environment}-{service}-{entity}-write",
            "schemaVersion is a positive integer, such as 1",
            "the rendered version segment adds the v prefix",
            "generation",
            "quiesce writes for the cutover window",
            "dual-write to both generations",
            "capture and replay every change",
            "one atomic Elasticsearch _aliases request",
            "write alias always has exactly one write index",
            "wildcard access across service namespaces is forbidden"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, normalizedGuide, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NamingGuide_CoversRequiredServiceExamples()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(
            repositoryRoot,
            "docs/engineering/elasticsearch-index-naming.md");

        var requiredExamples = new[]
        {
            "fa-dev-identity-accounts-v1-000001",
            "fa-dev-transaction-intake-drafts-v1-000001",
            "fa-dev-income-entries-v1-000001",
            "fa-dev-expense-entries-v1-000001",
            "fa-dev-analytics-monthly-summaries-v1-000001",
            "fa-dev-financial-score-snapshots-v1-000001",
            "fa-dev-audit-events-v1-000001",
            "fa-dev-monitoring-service-health-v1-000001"
        };

        foreach (var example in requiredExamples)
        {
            Assert.Contains(example, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DocumentationIndex_LinksNamingGuide()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentationIndex = ReadRequiredFile(repositoryRoot, "docs/README.md");

        Assert.Contains(
            "docs/engineering/elasticsearch-index-naming.md",
            documentationIndex,
            StringComparison.Ordinal);
    }

    private static string NormalizeDocumentation(string value) =>
        string.Join(
            ' ',
            value
                .Replace("`", string.Empty, StringComparison.Ordinal)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = Path.Combine(
            repositoryRoot,
            path.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(fullPath), $"Required file '{path}' is missing.");
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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
