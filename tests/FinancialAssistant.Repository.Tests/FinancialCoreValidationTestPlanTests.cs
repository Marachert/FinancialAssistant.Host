using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class FinancialCoreValidationTestPlanTests
{
    [Fact]
    public void Plan_CoversEveryStableValidationCase()
    {
        var plan = ReadPlan();

        for (var index = 1; index <= 66; index++)
        {
            Assert.Contains($"FCV-{index:000}", plan, StringComparison.Ordinal);
        }

        foreach (var section in new[]
                 {
                     "Amount validation",
                     "Currency validation",
                     "Date, timezone, and period validation",
                     "Duplicate and concurrency behavior",
                     "Archive, restore, and correction",
                     "Category references",
                     "Ownership and privacy",
                     "Draft, confirmed, manual, and AI boundaries",
                     "Summary and calculation invariants",
                     "Required fixtures",
                     "Implementation order and exit gate"
                 })
        {
            Assert.Contains(section, plan, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Plan_AssignsExecutableProjectsAndPreservesAuthorityBoundary()
    {
        var plan = ReadPlan();

        foreach (var project in new[]
                 {
                     "FinancialAssistant.TransactionIntake.Tests",
                     "FinancialAssistant.Category.Tests",
                     "FinancialAssistant.Income.Tests",
                     "FinancialAssistant.Expense.Tests",
                     "FinancialAssistant.FinancialSummary.Tests",
                     "FinancialAssistant.Repository.Tests"
                 })
        {
            Assert.Contains(project, plan, StringComparison.Ordinal);
        }

        Assert.Contains("Backend rules, not AI/OCR output", plan, StringComparison.Ordinal);
        Assert.Contains("suggestion remains reviewable but cannot be confirmed", plan, StringComparison.Ordinal);
        Assert.Contains("still excluded from authoritative records and totals", plan, StringComparison.Ordinal);
        Assert.Contains("synthetic data only", plan, StringComparison.Ordinal);
        Assert.Contains("no live paid provider", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_IsDiscoverableFromDocumentationIndex()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentationIndex = File.ReadAllText(
            Path.Combine(repositoryRoot, "docs", "README.md"));

        Assert.Contains(
            "docs/engineering/financial-core-validation-test-plan.md",
            documentationIndex,
            StringComparison.Ordinal);
    }

    private static string ReadPlan()
    {
        var repositoryRoot = FindRepositoryRoot();
        var content = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "docs",
                "engineering",
                "financial-core-validation-test-plan.md"));
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
