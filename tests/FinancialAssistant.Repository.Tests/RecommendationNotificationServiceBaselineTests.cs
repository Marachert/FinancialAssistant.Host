using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class RecommendationNotificationServiceBaselineTests
{
    [Fact]
    public void Fin30_ServiceProjectsAndContractsAreTracked()
    {
        var root = FindRepositoryRoot();
        var required = new[]
        {
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Api/FinancialAssistant.RecommendationsNotifications.Api.csproj",
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Application/FinancialAssistant.RecommendationsNotifications.Application.csproj",
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Contracts/FinancialAssistant.RecommendationsNotifications.Contracts.csproj",
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Domain/FinancialAssistant.RecommendationsNotifications.Domain.csproj",
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Infrastructure/FinancialAssistant.RecommendationsNotifications.Infrastructure.csproj",
            "backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Tests/FinancialAssistant.RecommendationsNotifications.Tests.csproj",
            "backend/shared/contracts/FinancialAssistant.Shared.Contracts/Events/AnalyticsUpdatedV1.cs",
            "backend/shared/contracts/FinancialAssistant.Shared.Contracts/Events/RecommendationGeneratedV1.cs",
            "backend/shared/contracts/FinancialAssistant.Shared.Contracts/Events/NotificationPreparedV1.cs",
            "docs/api/recommendations-notifications-v1.md",
            "docs/engineering/recommendations-notifications-v1.md"
        };

        Assert.All(required, path => Assert.True(
            File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
            $"Required FIN-30 file is missing: {path}"));
    }

    [Fact]
    public void Fin30_DoesNotEnableExternalAiOrDeliveryProviders()
    {
        var root = FindRepositoryRoot();
        var serviceRoot = Path.Combine(
            root,
            "backend",
            "services",
            "recommendations-notifications");
        var text = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(serviceRoot, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".md")
                .Select(File.ReadAllText));

        Assert.DoesNotContain("OpenAI", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FirebaseAdmin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApplePush", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InMemory", text, StringComparison.Ordinal);
        Assert.Contains("notification.prepared.v1", text, StringComparison.Ordinal);
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
