using System.Text.Json;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayPublicApiGroupsDocumentationTests
{
    private const string AppSettingsPath =
        "backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/appsettings.json";
    private const string DocumentationPath =
        "docs/engineering/gateway-public-api-groups.md";

    [Fact]
    public void Documentation_CoversConfiguredRoutesAndPublicIdentityAllowlist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markdown = File.ReadAllText(ToRepositoryPath(repositoryRoot, DocumentationPath));
        using var configuration = JsonDocument.Parse(
            File.ReadAllText(ToRepositoryPath(repositoryRoot, AppSettingsPath)));

        var gateway = configuration.RootElement.GetProperty("Gateway");
        var routes = gateway
            .GetProperty("RouteMap")
            .GetProperty("Routes")
            .EnumerateArray()
            .ToArray();

        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            var routeKey = route.GetProperty("RouteKey").GetString();
            var publicPattern = route.GetProperty("PublicPattern").GetString();
            var serviceOwner = route.GetProperty("ServiceOwner").GetString();
            var accessPolicy = route.GetProperty("AccessPolicy").GetString();
            var status = route.GetProperty("Status").GetString();

            Assert.False(string.IsNullOrWhiteSpace(routeKey));
            var routeRow = FindRouteRow(markdown, routeKey!);

            Assert.Contains($"`{publicPattern}`", routeRow, StringComparison.Ordinal);
            Assert.Contains(serviceOwner!, routeRow, StringComparison.Ordinal);
            Assert.Contains($"`{accessPolicy}`", routeRow, StringComparison.Ordinal);
            Assert.Contains($"`{status}`", routeRow, StringComparison.Ordinal);

            if (route.TryGetProperty("CatchAllPattern", out var catchAllPattern))
            {
                Assert.Contains(
                    $"`{catchAllPattern.GetString()}`",
                    routeRow,
                    StringComparison.Ordinal);
            }

            foreach (var method in route.GetProperty("Methods").EnumerateArray())
            {
                Assert.Contains(method.GetString()!, routeRow, StringComparison.Ordinal);
            }
        }

        var publicEndpoints = gateway
            .GetProperty("Security")
            .GetProperty("PublicEndpoints")
            .EnumerateArray()
            .ToArray();

        Assert.NotEmpty(publicEndpoints);
        foreach (var endpoint in publicEndpoints)
        {
            var method = endpoint.GetProperty("Method").GetString();
            var path = endpoint.GetProperty("Path").GetString();

            Assert.Contains($"`{method} {path}`", markdown, StringComparison.Ordinal);
        }

        var authRoute = Assert.Single(
            routes,
            route => route.GetProperty("RouteKey").GetString() == "auth");
        Assert.Equal("authenticated", authRoute.GetProperty("AccessPolicy").GetString());
        Assert.Contains("deny-by-default", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact public allowlist", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Documentation_CoversEveryFin84CapabilityGroupAndBoundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var markdown = File.ReadAllText(ToRepositoryPath(repositoryRoot, DocumentationPath));
        var headings = new[]
        {
            "## Identity API group",
            "## Profile API group",
            "## Category API group",
            "## Transaction API group",
            "## Receipt API group",
            "## Analytics API group",
            "## Financial score API group",
            "## Recommendation API group",
            "## Notification API group",
            "## Admin monitoring API group"
        };

        foreach (var heading in headings)
        {
            Assert.Contains(heading, markdown, StringComparison.Ordinal);
        }

        Assert.Contains("## Gateway responsibility boundary", markdown, StringComparison.Ordinal);
        Assert.Contains("## Synchronous and asynchronous boundaries", markdown, StringComparison.Ordinal);
        Assert.Contains("The gateway does not publish these business events", markdown, StringComparison.Ordinal);
        Assert.Contains("LLM output", markdown, StringComparison.Ordinal);
        Assert.Contains("financial calculations", markdown, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRouteRow(string markdown, string routeKey)
    {
        var row = markdown
            .Split('\n', StringSplitOptions.TrimEntries)
            .SingleOrDefault(line => line.StartsWith($"| `{routeKey}` |", StringComparison.Ordinal));

        return Assert.IsType<string>(row);
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
