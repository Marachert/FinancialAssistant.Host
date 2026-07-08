namespace FinancialAssistant.Repository.Tests;

public sealed class BackendSourceLayoutTests
{
    [Fact]
    public void BackendSourceLayout_UsesCanonicalGatewayServiceAndSharedDirectories()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedDirectories = new[]
        {
            "backend/gateways",
            "backend/services",
            "backend/shared/building-blocks",
            "backend/shared/contracts",
            "backend/shared/elasticsearch",
            "backend/shared/testing"
        };

        foreach (var directory in expectedDirectories)
        {
            Assert.True(
                Directory.Exists(ToRepositoryPath(repositoryRoot, directory)),
                $"Required backend source directory '{directory}' is missing.");
        }

        Assert.False(
            Directory.Exists(ToRepositoryPath(repositoryRoot, "backend/gateway")),
            "Use the canonical plural path 'backend/gateways'; do not create a parallel 'backend/gateway' root.");
    }

    [Fact]
    public void SharedDirectories_DocumentTechnicalOwnershipBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requiredDocumentation = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["backend/shared/README.md"] =
            [
                "technical assets",
                "service owns its domain model",
                "backend/gateways/"
            ],
            ["backend/shared/building-blocks/README.md"] =
            [
                "Technical utilities",
                "shared business repositories"
            ],
            ["backend/shared/contracts/README.md"] =
            [
                "Stable integration contracts",
                "contracts are versioned"
            ],
            ["backend/shared/elasticsearch/README.md"] =
            [
                "reusable technical Elasticsearch integration helpers",
                "Each service owns its Elasticsearch indices"
            ],
            ["backend/shared/testing/README.md"] =
            [
                "deterministic test helpers",
                "real user, credential, transaction, receipt, OCR, or LLM data"
            ]
        };

        foreach (var (path, requiredPhrases) in requiredDocumentation)
        {
            var fullPath = ToRepositoryPath(repositoryRoot, path);
            Assert.True(File.Exists(fullPath), $"Required boundary document '{path}' is missing.");

            var content = File.ReadAllText(fullPath);
            foreach (var requiredPhrase in requiredPhrases)
            {
                Assert.Contains(requiredPhrase, content, StringComparison.OrdinalIgnoreCase);
            }
        }
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
