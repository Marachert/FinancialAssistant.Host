using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class EventContractVersioningDocumentationTests
{
    [Fact]
    public void EventVersioning_DefinesNamesAndSchemaAlignment()
    {
        var guide = ReadGuide();
        var normalized = NormalizeWhitespace(guide);

        Assert.Contains("Related Jira: FIN-64", normalized, StringComparison.Ordinal);
        Assert.Contains("{domain}.{action}.v{schemaVersion}", normalized, StringComparison.Ordinal);
        Assert.Contains("positive integer major compatibility version", normalized, StringComparison.Ordinal);
        Assert.Contains("The complete event type is the RabbitMQ routing key.", normalized, StringComparison.Ordinal);
        Assert.Contains("The event-type suffix and schema-version field must match", normalized, StringComparison.Ordinal);
        Assert.Contains("`occurrenceId` is an opaque identifier assigned once", normalized, StringComparison.Ordinal);
        Assert.Contains("A mismatch is an invalid contract", normalized, StringComparison.Ordinal);
        Assert.Contains("Published messages are immutable facts.", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void EventVersioning_SeparatesCompatibleAndBreakingChanges()
    {
        var normalized = NormalizeWhitespace(ReadGuide());

        Assert.Contains("adding an optional payload field", normalized, StringComparison.Ordinal);
        Assert.Contains("Consumers must ignore unknown fields", normalized, StringComparison.Ordinal);
        Assert.Contains("Producers must keep emitting every existing required field.", normalized, StringComparison.Ordinal);
        Assert.Contains("removing or renaming a field", normalized, StringComparison.Ordinal);
        Assert.Contains("making an optional field required", normalized, StringComparison.Ordinal);
        Assert.Contains("changing a field's business meaning", normalized, StringComparison.Ordinal);
        Assert.Contains("require a new major", normalized, StringComparison.Ordinal);
        Assert.Contains("Never silently publish a breaking payload", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void EventVersioning_DefinesBreakingChangeMigration()
    {
        var normalized = NormalizeWhitespace(ReadGuide());

        Assert.Contains("create the new event type, schema, tests, and routing key", normalized, StringComparison.Ordinal);
        Assert.Contains("inventory authorized consumers", normalized, StringComparison.Ordinal);
        Assert.Contains("dual-publish old and new versions", normalized, StringComparison.Ordinal);
        Assert.Contains("distinct `eventId` values", normalized, StringComparison.Ordinal);
        Assert.Contains("same `occurrenceId`", normalized, StringComparison.Ordinal);
        Assert.Contains("deduplicate cross-version business side effects by `occurrenceId`", normalized, StringComparison.Ordinal);
        Assert.Contains("correlation and causation are not deduplication identities", normalized, StringComparison.Ordinal);
        Assert.Contains("stop the old version only after every required consumer has migrated", normalized, StringComparison.Ordinal);
        Assert.Contains("must be reversible during the support window", normalized, StringComparison.Ordinal);
        Assert.Contains("Unsupported versions and invalid contracts are terminal failures", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void EventVersioning_IncludesRequiredExamplesAndIsDiscoverable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var normalized = NormalizeWhitespace(ReadGuide());
        var eventsReadme = ReadRequiredFile(repositoryRoot, "docs/events/README.md");
        var docsReadme = ReadRequiredFile(repositoryRoot, "docs/README.md");

        foreach (var eventType in new[]
                 {
                     "user.registered.v1",
                     "transaction.confirmed.v1",
                     "income.created.v1",
                     "expense.created.v1"
                 })
        {
            Assert.Contains(eventType, normalized, StringComparison.Ordinal);
        }

        Assert.Contains("Owner: Identity Service.", normalized, StringComparison.Ordinal);
        Assert.Contains("Owner: Transaction Intake Service.", normalized, StringComparison.Ordinal);
        Assert.Contains("transactionId userId draftId transactionType", normalized, StringComparison.Ordinal);
        Assert.Contains("Owner: Income Service.", normalized, StringComparison.Ordinal);
        Assert.Contains("Owner: Expense Service.", normalized, StringComparison.Ordinal);
        Assert.Contains(
            "[Integration Event Contract Versioning](event-contract-versioning.md)",
            eventsReadme,
            StringComparison.Ordinal);
        Assert.Contains("docs/events/event-contract-versioning.md", docsReadme, StringComparison.Ordinal);
        Assert.Contains("synthetic identifiers only", normalized, StringComparison.Ordinal);
        Assert.Contains("FIN-65 owns the shared integration event envelope implementation", normalized, StringComparison.Ordinal);
    }

    private static string ReadGuide()
    {
        var repositoryRoot = FindRepositoryRoot();
        return ReadRequiredFile(repositoryRoot, "docs/events/event-contract-versioning.md");
    }

    private static string NormalizeWhitespace(string content) =>
        string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required event contract document '{path}' is missing.");
        return File.ReadAllText(fullPath);
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
