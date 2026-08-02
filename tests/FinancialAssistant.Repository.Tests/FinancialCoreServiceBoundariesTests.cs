using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class FinancialCoreServiceBoundariesTests
{
    [Fact]
    public void BoundaryContract_AssignsAllServiceOwnersAndAuthorityRules()
    {
        var contract = ReadContract();

        foreach (var service in new[]
                 {
                     "Profile Service",
                     "Category Service",
                     "Transaction Intake",
                     "Income Service",
                     "Expense Service",
                     "Financial Summary"
                 })
        {
            Assert.Contains(service, contract, StringComparison.Ordinal);
        }

        foreach (var rule in new[] { "FCB-001", "FCB-002", "FCB-003", "FCB-004", "FCB-005" })
        {
            Assert.Contains(rule, contract, StringComparison.Ordinal);
        }

        Assert.Contains("One owner for every mutable fact", contract, StringComparison.Ordinal);
        Assert.Contains("Only Income owns income records", contract, StringComparison.Ordinal);
        Assert.Contains("only Expense owns expense records", contract, StringComparison.Ordinal);
        Assert.Contains("must not read or write another service's database", contract, StringComparison.Ordinal);
        Assert.Contains("Shared contract packages define transport shapes only", contract, StringComparison.Ordinal);
        Assert.Contains("gateway-derived owner is mandatory", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundaryContract_SeparatesSynchronousReadsFromAsynchronousStatePropagation()
    {
        var contract = ReadContract();

        foreach (var section in new[]
                 {
                     "Synchronous interaction rules",
                     "Cross-service synchronous reads",
                     "Forbidden synchronous coupling",
                     "Asynchronous interaction map",
                     "Command and event direction",
                     "Consistency, retries, and failure ownership",
                     "Implementation review checklist"
                 })
        {
            Assert.Contains(section, contract, StringComparison.Ordinal);
        }

        Assert.Contains("versioned authenticated API", contract, StringComparison.Ordinal);
        Assert.Contains("bounded timeout", contract, StringComparison.Ordinal);
        Assert.Contains("must not create a distributed transaction", contract, StringComparison.Ordinal);
        Assert.Contains("service-owned transactional outboxes", contract, StringComparison.Ordinal);
        Assert.Contains("service-owned inboxes", contract, StringComparison.Ordinal);
        Assert.Contains("process at-least-once delivery idempotently", contract, StringComparison.Ordinal);
        Assert.Contains("does not synchronously call Income or Expense", contract, StringComparison.Ordinal);
        Assert.Contains("explicit freshness metadata", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundaryContract_ListsRequiredEventInteractionPoints()
    {
        var contract = ReadContract();

        foreach (var eventName in new[]
                 {
                     "user.registered.v1",
                     "category.updated.v1",
                     "transaction.draft-created.v1",
                     "transaction.confirmed.v1",
                     "income.created.v1",
                     "income.updated.v1",
                     "income.archived.v1",
                     "income.restored.v1",
                     "expense.created.v1",
                     "expense.updated.v1",
                     "expense.archived.v1",
                     "expense.restored.v1"
                 })
        {
            Assert.Contains(eventName, contract, StringComparison.Ordinal);
        }

        Assert.Contains("changes no totals", contract, StringComparison.Ordinal);
        Assert.Contains("matching Income or Expense consumer", contract, StringComparison.Ordinal);
        Assert.Contains("Financial Summary updates", contract, StringComparison.Ordinal);
        Assert.Contains("publishes no command that mutates", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundaryContract_PreservesSuggestionAndPrivacyBoundaries()
    {
        var contract = ReadContract();

        Assert.Contains("AI, OCR, clients, and the gateway cannot create or mutate", contract, StringComparison.Ordinal);
        Assert.Contains("must not promote an AI/OCR category guess", contract, StringComparison.Ordinal);
        Assert.Contains("raw input, receipts, OCR text, prompts, credentials", contract, StringComparison.Ordinal);
        Assert.Contains("synthetic data", contract, StringComparison.Ordinal);
        Assert.Contains("unlike currencies remain separate", contract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BoundaryContract_IsLinkedFromDocumentationIndexes()
    {
        var root = FindRepositoryRoot();
        var relativePath = "docs/architecture/financial-core-service-boundaries.md";

        var documentationIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var architectureIndex = File.ReadAllText(
            Path.Combine(root, "docs", "architecture", "README.md"));

        Assert.Contains(relativePath, documentationIndex, StringComparison.Ordinal);
        Assert.Contains(relativePath, architectureIndex, StringComparison.Ordinal);
    }

    private static string ReadContract()
    {
        var root = FindRepositoryRoot();
        var content = File.ReadAllText(
            Path.Combine(
                root,
                "docs",
                "architecture",
                "financial-core-service-boundaries.md"));

        return string.Join(
            ' ',
            content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
