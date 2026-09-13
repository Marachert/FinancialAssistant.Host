using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class FailedJobSupportWorkflowTests
{
    private const string Guide = "docs/engineering/failed-job-support-workflow.md";

    [Fact]
    public void SyntheticCase_HasOnlyApprovedFieldsAndNoInventedRecovery()
    {
        using var document = JsonDocument.Parse(Read("docs/delivery/failed-job-support-case.example.json"));
        var root = document.RootElement;
        Assert.Equal(new[]
        {
            "classification", "caseReference", "kind", "owner", "observationState",
            "failureCategory", "verifiedAttempt", "verifiedRetryAtUtc", "decision",
            "userMessageChoice", "handoff", "verification", "recoveryConfirmed", "supportMutationAllowed"
        }, root.EnumerateObject().Select(x => x.Name));
        Assert.Equal("synthetic-support-example-only", root.GetProperty("classification").GetString());
        Assert.Equal("synthetic-case-001", root.GetProperty("caseReference").GetString());
        Assert.Equal("ocr", root.GetProperty("kind").GetString());
        Assert.Equal("receipt-processing", root.GetProperty("owner").GetString());
        Assert.Equal("unavailable", root.GetProperty("observationState").GetString());
        Assert.Equal("visibility_gap", root.GetProperty("decision").GetString());
        Assert.Equal("unknown_outcome", root.GetProperty("userMessageChoice").GetString());
        Assert.Equal("service-owner", root.GetProperty("handoff").GetString());
        Assert.Equal("pending-owner-evidence", root.GetProperty("verification").GetString());
        foreach (var field in new[] { "failureCategory", "verifiedAttempt", "verifiedRetryAtUtc" })
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty(field).ValueKind);
        }

        Assert.False(root.GetProperty("recoveryConfirmed").GetBoolean());
        Assert.False(root.GetProperty("supportMutationAllowed").GetBoolean());
    }

    [Theory]
    [InlineData("ai-orchestration", "FinancialAssistant.AiOrchestration.Contracts/AiJobFailureHandling.cs")]
    [InlineData("receipt-processing", "FinancialAssistant.ReceiptProcessing.Contracts/OcrJobFailureHandling.cs")]
    public void JobRetryGuidance_UsesExistingCategoryAndAttemptContracts(string service, string relativePath)
    {
        var code = Read($"backend/services/{service}/{relativePath}");
        var guide = Read(Guide);
        foreach (var category in new[] { "provider_timeout", "provider_unavailable", "rate_limited", "transport_failure", "provider_disabled" })
        {
            Assert.Contains($"\"{category}\"", code, StringComparison.Ordinal);
            Assert.Contains($"`{category}`", guide, StringComparison.Ordinal);
        }

        Assert.Contains("MaximumAttempts = 3", code, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(30)", code, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMinutes(2)", code, StringComparison.Ordinal);
        Assert.Contains("At most three attempts", guide, StringComparison.Ordinal);
        Assert.Contains("attempt 3 after 120 seconds", guide, StringComparison.Ordinal);
        Assert.Contains("eligibility does not itself schedule work", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void Guide_DistinguishesVisibilityRetryAndFinancialAuthority()
    {
        var guide = Read(Guide);
        foreach (var phrase in new[]
        {
            "In-process provider call", "AI/OCR logical job", "Notification adapter",
            "Broker redelivery", "six external attempts", "not an approved spend allowance",
            "no blind resubmission", "no mutation capability", "not authorization",
            "processing_temporarily_delayed", "a retryable flag alone is insufficient",
            "support lookup remains disabled", "non-sending placeholders", "process-local",
            "Support never confirms", "Unknown values stay null", "not public Jira",
            "FIN-198", "FIN-199", "FIN-200/P9"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Guide_IsLinkedFromOwnedOperationalContracts()
    {
        foreach (var path in new[]
        {
            "docs/README.md", "docs/engineering/mcp-operational-diagnostics.md",
            "docs/engineering/notification-delivery-adapters.md", "docs/engineering/async-ai-ocr-processing-flow.md"
        })
        {
            Assert.Contains("failed-job-support-workflow.md", Read(path), StringComparison.Ordinal);
        }

        Assert.Contains("failed-job-support-case.example.json", Read(Guide), StringComparison.Ordinal);
    }

    private static string Read(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return File.ReadAllText(Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
