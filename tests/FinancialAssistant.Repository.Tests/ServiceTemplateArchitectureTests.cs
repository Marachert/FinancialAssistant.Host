using System.Xml.Linq;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class ServiceTemplateArchitectureTests
{
    private static readonly string[] ProjectNames =
    [
        "ServiceTemplate.Api",
        "ServiceTemplate.Application",
        "ServiceTemplate.Domain",
        "ServiceTemplate.Infrastructure",
        "ServiceTemplate.Contracts"
    ];

    [Fact]
    public void ServiceTemplate_ContainsFiveNet8Projects()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectName in ProjectNames)
        {
            var projectPath = GetProjectPath(repositoryRoot, projectName);
            Assert.True(File.Exists(projectPath), $"Template project '{projectName}' is missing.");

            var document = XDocument.Load(projectPath);
            Assert.Equal("net8.0", GetProperty(document, "TargetFramework"));
            Assert.Equal("enable", GetProperty(document, "Nullable"));
            Assert.Equal("enable", GetProperty(document, "ImplicitUsings"));
            Assert.Equal($"FinancialAssistant.{projectName}", GetProperty(document, "RootNamespace"));
            Assert.Equal($"FinancialAssistant.{projectName}", GetProperty(document, "AssemblyName"));
        }

        var apiProject = XDocument.Load(GetProjectPath(repositoryRoot, "ServiceTemplate.Api"));
        Assert.Equal("Microsoft.NET.Sdk.Web", apiProject.Root?.Attribute("Sdk")?.Value);

        var apiProgram = ToRepositoryPath(
            repositoryRoot,
            "backend/templates/service-template/ServiceTemplate.Api/Program.cs");
        Assert.True(File.Exists(apiProgram), "The service template API host entry point is missing.");
    }

    [Fact]
    public void ServiceTemplate_ProjectReferencesFollowCleanArchitecture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ServiceTemplate.Api"] =
            [
                "ServiceTemplate.Application",
                "ServiceTemplate.Contracts",
                "ServiceTemplate.Infrastructure"
            ],
            ["ServiceTemplate.Application"] =
            [
                "ServiceTemplate.Contracts",
                "ServiceTemplate.Domain"
            ],
            ["ServiceTemplate.Domain"] = [],
            ["ServiceTemplate.Infrastructure"] =
            [
                "ServiceTemplate.Application",
                "ServiceTemplate.Contracts",
                "ServiceTemplate.Domain"
            ],
            ["ServiceTemplate.Contracts"] = []
        };

        foreach (var (projectName, expected) in expectedReferences)
        {
            var actual = ReadProjectReferences(GetProjectPath(repositoryRoot, projectName));
            Assert.Equal(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void DomainAndContracts_HaveNoOutwardOrProviderDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectName in new[] { "ServiceTemplate.Domain", "ServiceTemplate.Contracts" })
        {
            var document = XDocument.Load(GetProjectPath(repositoryRoot, projectName));
            Assert.Empty(FindElements(document, "ProjectReference"));
            Assert.Empty(FindElements(document, "PackageReference"));
            Assert.Empty(FindElements(document, "FrameworkReference"));
        }
    }

    [Fact]
    public void ServiceTemplate_IsBuiltByStandaloneAndRootSolutions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var standaloneSolutionPath = ToRepositoryPath(
            repositoryRoot,
            "backend/templates/service-template/FinancialAssistant.ServiceTemplate.sln");
        var rootSolutionPath = ToRepositoryPath(repositoryRoot, "FinancialAssistant.Backend.sln");

        Assert.True(File.Exists(standaloneSolutionPath), "Standalone template solution is missing.");
        Assert.True(File.Exists(rootSolutionPath), "Root backend solution is missing.");

        var standaloneSolution = File.ReadAllText(standaloneSolutionPath);
        var rootSolution = File.ReadAllText(rootSolutionPath);

        foreach (var projectName in ProjectNames)
        {
            Assert.Contains(
                $"{projectName}\\{projectName}.csproj",
                standaloneSolution,
                StringComparison.Ordinal);
            Assert.Contains(
                $"backend\\templates\\service-template\\{projectName}\\{projectName}.csproj",
                rootSolution,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ServiceTemplateReadme_DocumentsOwnershipAndFin52Boundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readmePath = ToRepositoryPath(repositoryRoot, "backend/templates/service-template/README.md");
        Assert.True(File.Exists(readmePath), "Service template README is missing.");

        var readme = File.ReadAllText(readmePath);
        var requiredPhrases = new[]
        {
            "Api -> Application",
            "Infrastructure -> Application",
            "Application -> Domain",
            "Domain -> no project references",
            "Contracts -> no project references",
            "dotnet build backend/templates/service-template/FinancialAssistant.ServiceTemplate.sln",
            "backend deterministic",
            "LLM or OCR output",
            "FIN-52",
            "liveness and readiness health endpoints"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyCollection<string> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return FindElements(document, "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!))
            .ToArray();
    }

    private static IEnumerable<XElement> FindElements(XDocument document, string localName) =>
        document.Descendants().Where(element => element.Name.LocalName == localName);

    private static string? GetProperty(XDocument document, string localName) =>
        FindElements(document, localName).SingleOrDefault()?.Value;

    private static string GetProjectPath(string repositoryRoot, string projectName) =>
        ToRepositoryPath(
            repositoryRoot,
            $"backend/templates/service-template/{projectName}/{projectName}.csproj");

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
