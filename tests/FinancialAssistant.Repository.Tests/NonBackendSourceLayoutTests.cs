using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NonBackendSourceLayoutTests
{
    [Fact]
    public void NonBackendSourceLayout_ContainsRequiredDeliveryDirectories()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedDirectories = new[]
        {
            "mobile/app-react-native",
            "web-admin/monitoring-ui",
            "infra/docker-compose",
            "docs/architecture",
            "docs/api",
            "docs/events",
            "docs/security",
            "docs/delivery"
        };

        foreach (var directory in expectedDirectories)
        {
            Assert.True(
                Directory.Exists(ToRepositoryPath(repositoryRoot, directory)),
                $"Required non-backend source directory '{directory}' is missing.");
        }
    }

    [Fact]
    public void NonBackendSourceLayout_DocumentsClientAndDeliveryBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var requiredDocumentation = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["mobile/app-react-native/README.md"] =
            [
                "Public API Gateway",
                "backend remains the source of truth",
                "OCR or LLM output"
            ],
            ["web-admin/monitoring-ui/README.md"] =
            [
                "admin-protected REST APIs",
                "must not",
                "financial calculations"
            ],
            ["infra/docker-compose/README.md"] =
            [
                "local infrastructure baseline",
                "Each backend service must own only its own Elasticsearch indices",
                "Do not commit .env"
            ],
            ["docs/architecture/README.md"] =
            [
                "service boundaries and data ownership",
                "synchronous REST and asynchronous RabbitMQ",
                "LLM and OCR outputs are probabilistic inputs"
            ],
            ["docs/api/README.md"] =
            [
                "Public API Gateway is the only client-facing HTTP entry point",
                "owning service remains responsible",
                "synthetic data"
            ],
            ["docs/events/README.md"] =
            [
                "service that owns the state change owns the event intent",
                "tolerate duplicate or delayed delivery",
                "minimum information needed"
            ],
            ["docs/security/README.md"] =
            [
                "Never commit production credentials",
                "Client-side checks improve UX but are not authorization boundaries",
                "output remains untrusted"
            ],
            ["docs/delivery/README.md"] =
            [
                "separate MVP requirements from later enterprise hardening",
                "owning Jira item",
                "must not activate a gateway route"
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
