using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MvpE2eScenarioTests
{
    private const string Guide = "docs/engineering/mvp-e2e-scenarios.md";

    [Fact]
    public void Manifest_IsPlanOnlyWithSevenOwnedJourneyScenarios()
    {
        using var document = Manifest();
        var root = document.RootElement;
        Assert.Equal("synthetic-scenario-plan-only", root.GetProperty("classification").GetString());
        Assert.False(root.GetProperty("automaticExecution").GetBoolean());
        Assert.False(root.GetProperty("runtimeAcceptance").GetBoolean());
        Assert.Equal(Guide, root.GetProperty("guide").GetString());
        var scenarios = root.GetProperty("scenarios").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 7).Select(number => $"MVP-E2E-{number:000}"),
            scenarios.Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(7, scenarios.Select(item => item.GetProperty("journey").GetString()).Distinct().Count());
        foreach (var scenario in scenarios)
        {
            Assert.Equal("planned", scenario.GetProperty("status").GetString());
            var id = scenario.GetProperty("id").GetString()!;
            Assert.Contains($"### {id} ", Read(Guide), StringComparison.Ordinal);
            var reference = scenario.GetProperty("reference").GetString()!;
            Assert.DoesNotContain("..", reference, StringComparison.Ordinal);
            Assert.DoesNotContain(":", reference, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(reference));
            Assert.NotEmpty(Read(reference));
        }
    }

    [Fact]
    public void SyntheticFixture_UsesDecimalArithmeticAndSeparateCurrency()
    {
        using var document = Manifest();
        var fixture = document.RootElement.GetProperty("fixture");
        Assert.Equal("USD", fixture.GetProperty("currency").GetString());
        Assert.Equal("UTC", fixture.GetProperty("timeZone").GetString());
        Assert.True(DateOnly.TryParseExact(fixture.GetProperty("controlledDate").GetString(), "yyyy-MM-dd", out _));
        decimal Number(string name) => fixture.GetProperty(name).GetDecimal();
        Assert.Equal(60m, Number("textExpense") + Number("receiptExpense"));
        Assert.Equal(Number("expectedExpense"), Number("textExpense") + Number("receiptExpense"));
        Assert.Equal(40m, Number("income") - Number("expectedExpense"));
        Assert.Equal(Number("expectedBalanceDelta"), Number("income") - Number("expectedExpense"));
        Assert.Equal(Number("expectedBudgetUsagePercent"), 100m * Number("expectedExpense") / Number("monthlyBudget"));
        Assert.Equal(120m, Number("expectedBudgetUsagePercent"));
    }

    [Fact]
    public void EveryJourney_HasPreconditionsAndNegativePaths()
    {
        var guide = Read(Guide);
        var sections = Regex.Split(guide, @"(?m)^### MVP-E2E-\d{3} ").Skip(1).ToArray();
        Assert.Equal(7, sections.Length);
        foreach (var section in sections)
        {
            Assert.Contains("Preconditions:", section, StringComparison.Ordinal);
            Assert.Contains("Negatives:", section, StringComparison.Ordinal);
        }
        var normalized = Regex.Replace(guide, @"\s+", " ");
        foreach (var phrase in new[]
        {
            "planned scenarios, not execution results", "placeholder routes", "never proves those totals converged",
            "Default additional spend is zero", "do not change the host clock", "no implicit conversion",
            "same transaction", "B cannot read or confirm A's draft", "over 10 MiB", "not assumed content-deduplicated",
            "Never use a fixed sleep as proof", "not the expected score after these transactions",
            "provider acceptance alone is not receiver receipt evidence", "Never manually mark delivered to pass",
            "Only an explicit Pass with valid evidence qualifies", "Blank, Fail, Blocked and Not run",
            "First-user testing remains"
        })
        {
            Assert.Contains(phrase, normalized, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReferencesAndNavigation_ResolveToOwnedFiles()
    {
        var directory = Path.GetDirectoryName(Rooted(Guide))!;
        foreach (Match match in Regex.Matches(Read(Guide), @"\]\(([^)]+\.\w+)\)"))
        {
            var target = match.Groups[1].Value;
            Assert.DoesNotContain(":", target, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.GetFullPath(Path.Combine(directory, target))), target);
        }
        foreach (var path in new[] { "docs/README.md", "docs/engineering/poc-qa-strategy.md", "docs/engineering/mobile-smoke-regression-test-plan.md" })
        {
            Assert.Contains("mvp-e2e-scenarios.md", Read(path), StringComparison.Ordinal);
        }
    }

    private static JsonDocument Manifest() => JsonDocument.Parse(Read("docs/engineering/mvp-e2e-scenarios.json"));
    private static string Read(string path) => File.ReadAllText(Rooted(path));

    private static string Rooted(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
