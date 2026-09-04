using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class StructuredLoggingBaselineTests
{
    [Fact]
    public void EveryApiHost_ReferencesAndRegistersTheSharedBaseline()
    {
        var root = FindRepositoryRoot();
        var programs = Directory.GetFiles(
            Path.Combine(root, "backend"),
            "Program.cs",
            SearchOption.AllDirectories)
            .Where(path =>
                path.Contains(
                    $"{Path.DirectorySeparatorChar}services{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                || path.Contains(
                    $"{Path.DirectorySeparatorChar}templates{Path.DirectorySeparatorChar}service-template{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(15, programs.Length);

        foreach (var program in programs)
        {
            var source = File.ReadAllText(program);
            Assert.Contains(
                "AddFinancialAssistantObservability()",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "UseFinancialAssistantCorrelation()",
                source,
                StringComparison.Ordinal);

            var project = Directory.GetFiles(
                Path.GetDirectoryName(program)!,
                "*.csproj",
                SearchOption.TopDirectoryOnly).Single();
            Assert.Contains(
                "FinancialAssistant.Shared.Observability.csproj",
                File.ReadAllText(project),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Gateway_UsesSharedHostSetupAndItsEquivalentRequestMiddleware()
    {
        var root = FindRepositoryRoot();
        var gatewayRoot = Path.Combine(
            root,
            "backend",
            "gateways",
            "public-api-gateway",
            "FinancialAssistant.PublicApiGateway");
        var program = File.ReadAllText(Path.Combine(gatewayRoot, "Program.cs"));
        var middleware = File.ReadAllText(
            Path.Combine(gatewayRoot, "Observability", "CorrelationMiddleware.cs"));

        Assert.Contains("AddFinancialAssistantObservability()", program, StringComparison.Ordinal);
        Assert.Contains("UseMiddleware<CorrelationMiddleware>()", program, StringComparison.Ordinal);
        Assert.Contains("[\"CorrelationId\"]", middleware, StringComparison.Ordinal);
        Assert.Contains("[\"TraceId\"]", middleware, StringComparison.Ordinal);
        Assert.Contains("[\"ServiceName\"]", middleware, StringComparison.Ordinal);
        Assert.Contains("[\"Environment\"]", middleware, StringComparison.Ordinal);
        Assert.Contains("options.TraceIdHeaderName", middleware, StringComparison.Ordinal);
    }

    [Fact]
    public void Baseline_DocumentsCanonicalFieldsPropagationAndPrivacy()
    {
        var root = FindRepositoryRoot();
        var documentation = ReadRequiredFile(
            root,
            "docs/engineering/structured-logging-and-correlation.md");

        foreach (var phrase in new[]
                 {
                     "CorrelationId",
                     "TraceId",
                     "ServiceName",
                     "Environment",
                     "FailureType",
                     "correlationId",
                     "X-Correlation-Id",
                     "X-Trace-Id",
                     "CorrelationPropagationHandler",
                     "RabbitMQ",
                     "W3C",
                     "no external exporter"
                 })
        {
            Assert.Contains(phrase, documentation, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "financial values, receipts, raw OCR text, prompts, completions",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "adds no external exporter",
            documentation,
            StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string root, string path)
    {
        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-191 file '{path}' is missing.");
        return File.ReadAllText(fullPath);
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
