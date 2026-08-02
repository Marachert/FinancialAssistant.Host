using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class AnalyticsServiceBaselineTests
{
    [Fact]
    public void Solution_ContainsCompleteAnalyticsServiceLayers()
    {
        var root = FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "FinancialAssistant.Backend.sln"));

        foreach (var layer in new[] { "Api", "Application", "Contracts", "Domain", "Infrastructure", "Tests" })
        {
            var project = $"FinancialAssistant.Analytics.{layer}";
            Assert.Contains(project, solution, StringComparison.Ordinal);
            Assert.True(
                File.Exists(
                    Path.Combine(
                        root,
                        "backend",
                        "services",
                        "analytics",
                        project,
                        $"{project}.csproj")),
                $"Missing {project} project.");
        }
    }

    [Fact]
    public void Analytics_ConsumesConfirmedEventsAndPersistsDeterministicAggregates()
    {
        var documentation = Read(rootRelativePath: "docs/engineering/analytics-dashboard-read-model.md");
        var projector = Read(
            rootRelativePath:
            "backend/services/analytics/FinancialAssistant.Analytics.Application/AnalyticsProjector.cs");
        var store = Read(
            rootRelativePath:
            "backend/services/analytics/FinancialAssistant.Analytics.Infrastructure/InMemoryAnalyticsReadModelStore.cs");
        var consumer = Read(
            rootRelativePath:
            "backend/services/analytics/FinancialAssistant.Analytics.Infrastructure/AnalyticsFinancialEventConsumer.cs");
        var dependencyInjection = Read(
            rootRelativePath:
            "backend/services/analytics/FinancialAssistant.Analytics.Infrastructure/DependencyInjection.cs");

        foreach (var eventName in new[]
                 {
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
            Assert.Contains(eventName, documentation, StringComparison.Ordinal);
        }

        Assert.Contains("FinancialRecordChangedV1", projector, StringComparison.Ordinal);
        Assert.Contains("fa.analytics.financial-events.v1", Read("backend/services/analytics/FinancialAssistant.Analytics.Infrastructure/AnalyticsServiceOptions.cs"), StringComparison.Ordinal);
        Assert.Contains("BasicConsumeAsync", consumer, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<AnalyticsFinancialEventConsumer>", dependencyInjection, StringComparison.Ordinal);
        Assert.Contains("current.Revision >= projection.Revision", store, StringComparison.Ordinal);
        Assert.Contains("new AnalyticsProjectionSnapshot", store, StringComparison.Ordinal);
        Assert.Contains("WeeklyTotals", Read("backend/services/analytics/FinancialAssistant.Analytics.Domain/AnalyticsReadModels.cs"), StringComparison.Ordinal);
        Assert.Contains("StartOfWeek", store, StringComparison.Ordinal);
        Assert.Contains("new AnalyticsMonthlyAggregate", store, StringComparison.Ordinal);
        Assert.Contains("new AnalyticsCategoryTotal", store, StringComparison.Ordinal);
        Assert.Contains("Unlike currencies are never mixed", documentation, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardContract_ExposesRequiredValuesWithoutInternalState()
    {
        var contracts = Read(
            "backend/services/analytics/FinancialAssistant.Analytics.Contracts/AnalyticsContracts.cs");
        var api = Read("docs/api/analytics-dashboard-v1.md");

        foreach (var responseSection in new[]
                 {
                     "AnalyticsPeriodSummaryResponse",
                     "AnalyticsDailyLimitResponse",
                     "AnalyticsMonthlyProgressResponse",
                     "AnalyticsCategoryTotalResponse",
                     "AnalyticsTrendPointResponse",
                     "AnalyticsFreshnessResponse",
                     "AnalyticsCategoryBreakdownResponse",
                     "AnalyticsCategoryBreakdownItemResponse"
                 })
        {
            Assert.Contains(responseSection, contracts, StringComparison.Ordinal);
        }

        Assert.Contains("GET /api/v1/analytics/dashboard", api, StringComparison.Ordinal);
        Assert.Contains("GET /analytics/dashboard", api, StringComparison.Ordinal);
        var breakdownApi = Read("docs/api/analytics-category-breakdown-v1.md");
        Assert.Contains(
            "GET /api/v1/analytics/category-breakdown",
            breakdownApi,
            StringComparison.Ordinal);
        Assert.Contains("daily", breakdownApi, StringComparison.Ordinal);
        Assert.Contains("weekly", breakdownApi, StringComparison.Ordinal);
        Assert.Contains("monthly", breakdownApi, StringComparison.Ordinal);
        Assert.Contains("uncategorized", breakdownApi, StringComparison.Ordinal);
        Assert.Contains("dailySummary", api, StringComparison.Ordinal);
        Assert.Contains("weeklySummary", api, StringComparison.Ordinal);
        Assert.Contains("monthlySummary", api, StringComparison.Ordinal);
        Assert.Contains("Empty periods return zero totals", api, StringComparison.Ordinal);
        Assert.Contains("Monday through Sunday", api, StringComparison.Ordinal);
        Assert.Contains("isConfigured = false", Read("docs/engineering/analytics-dashboard-read-model.md"), StringComparison.Ordinal);
        Assert.Contains("Callers cannot submit a limit", Read("docs/engineering/analytics-dashboard-read-model.md"), StringComparison.Ordinal);
        Assert.DoesNotContain("UserIdHash", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordId", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("EventId", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyticsDocumentation_IsLinkedFromIndexes()
    {
        var documentationIndex = Read("docs/README.md");
        var apiIndex = Read("docs/api/README.md");

        Assert.Contains(
            "docs/engineering/analytics-dashboard-read-model.md",
            documentationIndex,
            StringComparison.Ordinal);
        Assert.Contains(
            "docs/api/analytics-dashboard-v1.md",
            documentationIndex,
            StringComparison.Ordinal);
        Assert.Contains("analytics-dashboard-v1.md", apiIndex, StringComparison.Ordinal);
        Assert.Contains(
            "analytics-category-breakdown-v1.md",
            apiIndex,
            StringComparison.Ordinal);
    }

    private static string Read(string rootRelativePath) =>
        File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                rootRelativePath.Replace('/', Path.DirectorySeparatorChar)));

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
