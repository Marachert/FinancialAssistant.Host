using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class FinancialCoreEndToEndFlowTests
{
    [Fact]
    public void Flow_CoversEverySynchronousAndAsynchronousStage()
    {
        var flow = ReadFlow();

        for (var index = 1; index <= 10; index++)
        {
            Assert.Contains($"E2E-{index:000}", flow, StringComparison.Ordinal);
        }

        foreach (var section in new[]
                 {
                     "Synchronous REST phase",
                     "Asynchronous event phase",
                     "Synchronous dashboard read",
                     "Execution profiles",
                     "Idempotency and retry ledger",
                     "Failure matrix",
                     "Integration-test scenarios",
                     "Correlation and diagnostics"
                 })
        {
            Assert.Contains(section, flow, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Financial Summary response",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "contains the expected record contribution exactly once",
            flow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_UsesExistingPublicContractsAndStableFailureCodes()
    {
        var flow = ReadFlow();

        foreach (var route in new[]
                 {
                     "POST /api/v1/transactions/intake",
                     "GET /api/v1/transactions/drafts/{draftId}",
                     "PUT /api/v1/transactions/drafts/{draftId}",
                     "POST /api/v1/transactions/drafts/{draftId}/reject",
                     "POST /api/v1/transactions/drafts/{draftId}/confirm",
                     "GET /api/v1/financial-summary"
                 })
        {
            Assert.Contains(route, flow, StringComparison.Ordinal);
        }

        foreach (var code in new[]
                 {
                     "idempotency_key_conflict",
                     "invalid_transaction_input",
                     "transaction_draft_not_found",
                     "transaction_draft_not_editable",
                     "transaction_draft_not_confirmable"
                 })
        {
            Assert.Contains(code, flow, StringComparison.Ordinal);
        }

        Assert.Contains("The first successful request returns `201`", flow, StringComparison.Ordinal);
        Assert.Contains("returns `200` with the same draft ID", flow, StringComparison.Ordinal);
        Assert.Contains("Repeating or racing the same confirmation returns `200`", flow, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_PreservesFinancialAuthorityAndEventDirection()
    {
        var flow = ReadFlow();

        foreach (var eventName in new[]
                 {
                     "transaction.draft-created.v1",
                     "transaction.confirmed.v1",
                     "income.created.v1",
                     "expense.created.v1"
                 })
        {
            Assert.Contains(eventName, flow, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Authority begins only when Income or Expense independently validates and commits",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "No service reads another service's storage",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "The end-to-end completion signal is not the confirmation HTTP response",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not promise that the authoritative record or Summary projection is already visible",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "unlike currencies are never combined",
            flow,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Flow_DefinesIdempotencyRetriesAndObservableFailures()
    {
        var flow = ReadFlow();

        foreach (var identity in new[]
                 {
                     "authenticated owner + opaque idempotency key + normalized-input fingerprint",
                     "owner + draft ID + claimed revision",
                     "event ID + transaction ID",
                     "event type + record ID + revision",
                     "owner hash + record type + record ID + revision"
                 })
        {
            Assert.Contains(identity, flow, StringComparison.Ordinal);
        }

        Assert.Contains("bounded exponential backoff and jitter", flow, StringComparison.Ordinal);
        Assert.Contains("Tests must not use a fixed sleep", flow, StringComparison.Ordinal);
        Assert.Contains("Confirmation remains committed; Summary may be pending", flow, StringComparison.Ordinal);
        Assert.Contains("Authoritative record remains committed; Summary is stale", flow, StringComparison.Ordinal);
        Assert.Contains("does not repeat financial writes", flow, StringComparison.Ordinal);
        Assert.Contains("safe reason codes", flow, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_ProvidesStableIntegrationScenariosWithoutPaidProviders()
    {
        var flow = ReadFlow();

        for (var index = 1; index <= 12; index++)
        {
            Assert.Contains($"FC-E2E-{index:000}", flow, StringComparison.Ordinal);
        }

        Assert.Contains("No live or paid AI/OCR provider is required", flow, StringComparison.Ordinal);
        Assert.Contains("synthetic data", flow, StringComparison.Ordinal);
        Assert.Contains(
            "must not read or mutate private service tables",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "A test that bypasses confirmation or directly inserts a Summary row is not an end-to-end",
            flow,
            StringComparison.Ordinal);
        Assert.Contains(
            "before a full HTTP end-to-end test is classified green",
            flow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_IsLinkedFromDocumentationIndexes()
    {
        var root = FindRepositoryRoot();
        const string relativePath = "docs/architecture/financial-core-e2e-flow.md";

        var documentationIndex = File.ReadAllText(Path.Combine(root, "docs", "README.md"));
        var architectureIndex = File.ReadAllText(
            Path.Combine(root, "docs", "architecture", "README.md"));

        Assert.Contains(relativePath, documentationIndex, StringComparison.Ordinal);
        Assert.Contains(relativePath, architectureIndex, StringComparison.Ordinal);
    }

    private static string ReadFlow()
    {
        var root = FindRepositoryRoot();
        var content = File.ReadAllText(
            Path.Combine(
                root,
                "docs",
                "architecture",
                "financial-core-e2e-flow.md"));

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
