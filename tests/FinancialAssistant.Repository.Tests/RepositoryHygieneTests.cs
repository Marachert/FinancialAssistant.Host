using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class RepositoryHygieneTests
{
    [Fact]
    public void RepositoryHygieneFiles_CoverSupportedToolchains()
    {
        var repositoryRoot = FindRepositoryRoot();

        var gitIgnore = ReadRequiredFile(repositoryRoot, ".gitignore");
        var requiredIgnoreRules = new[]
        {
            "[Bb]in/",
            "[Oo]bj/",
            "x64/",
            "x86/",
            "[Ww][Ii][Nn]32/",
            "[Aa][Rr][Mm]/",
            "[Aa][Rr][Mm]64/",
            "bld/",
            "node_modules/",
            ".expo/",
            "mobile/**/android/.gradle/",
            "mobile/**/ios/Pods/",
            ".vs/",
            ".idea/",
            ".env.*",
            "*.pem",
            "!*.example.pem",
            "!*.pem.example",
            "docker-compose.override.yml",
            "*.nupkg",
            "*.apk"
        };

        foreach (var rule in requiredIgnoreRules)
        {
            Assert.Contains(rule, gitIgnore, StringComparison.Ordinal);
        }

        var editorConfig = ReadRequiredFile(repositoryRoot, ".editorconfig");
        Assert.Contains("root = true", editorConfig, StringComparison.Ordinal);
        Assert.Contains("charset = utf-8", editorConfig, StringComparison.Ordinal);
        Assert.Contains("[*.{js,jsx,ts,tsx,mjs,cjs,json,yml,yaml,css,scss,md}]", editorConfig, StringComparison.Ordinal);
        Assert.Contains("[*.{cs,csx}]", editorConfig, StringComparison.Ordinal);

        var gitAttributes = ReadRequiredFile(repositoryRoot, ".gitattributes");
        Assert.Contains("* text=auto eol=lf", gitAttributes, StringComparison.Ordinal);
        Assert.Contains("*.bat text eol=crlf", gitAttributes, StringComparison.Ordinal);
        Assert.Contains("*.png binary", gitAttributes, StringComparison.Ordinal);

        var license = ReadRequiredFile(repositoryRoot, "LICENSE");
        Assert.Contains("License", license, StringComparison.OrdinalIgnoreCase);
        Assert.True(license.Length > 100, "LICENSE must contain a meaningful license or internal placeholder.");
    }

    [Fact]
    public void BackendCi_TriggersForRepositoryHygieneChanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = ReadRequiredFile(repositoryRoot, ".github/workflows/backend-ci.yml");
        var requiredFilters = new[]
        {
            "'.gitignore'",
            "'.gitattributes'",
            "'.editorconfig'",
            "'LICENSE'"
        };

        foreach (var filter in requiredFilters)
        {
            Assert.Equal(2, CountOccurrences(workflow, filter));
        }
    }

    [Fact]
    public void CanonicalDirectories_HaveTrackedPlaceholderOrSourceContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trackedFiles = GetTrackedFiles(repositoryRoot);
        var canonicalDirectories = new[]
        {
            "backend/gateways",
            "backend/services",
            "backend/shared/building-blocks",
            "backend/shared/contracts",
            "backend/shared/elasticsearch",
            "backend/shared/testing",
            "mobile/app-react-native",
            "web-admin/monitoring-ui",
            "infra/docker-compose",
            "docs/architecture",
            "docs/api",
            "docs/events",
            "docs/security",
            "docs/delivery"
        };

        foreach (var directory in canonicalDirectories)
        {
            Assert.Contains(
                trackedFiles,
                path => path.StartsWith($"{directory}/", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TrackedFiles_DoNotContainLocalSecretsOrGeneratedArtifacts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trackedFiles = GetTrackedFiles(repositoryRoot);
        var bannedDirectorySegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            "node_modules",
            "TestResults",
            "BuildResults",
            "coverage",
            "Pods",
            "DerivedData",
            ".gradle",
            ".expo"
        };
        var bannedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll",
            ".exe",
            ".pdb",
            ".nupkg",
            ".snupkg",
            ".trx",
            ".binlog",
            ".apk",
            ".aab",
            ".ipa",
            ".keystore",
            ".jks",
            ".pfx",
            ".p12",
            ".key",
            ".pem"
        };

        foreach (var path in trackedFiles)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Assert.DoesNotContain(segments, segment => bannedDirectorySegments.Contains(segment));

            var fileName = segments[^1];
            Assert.False(
                string.Equals(fileName, ".env", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".example", StringComparison.OrdinalIgnoreCase),
                $"Tracked local environment file is not allowed: {path}");
            Assert.False(
                string.Equals(fileName, "secrets.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase),
                $"Tracked local or secret file is not allowed: {path}");
            Assert.False(
                fileName.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".Local.json", StringComparison.OrdinalIgnoreCase),
                $"Tracked local application settings are not allowed: {path}");

            var extension = Path.GetExtension(fileName);
            var isAllowedPemTemplate =
                fileName.EndsWith(".example.pem", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".pem.example", StringComparison.OrdinalIgnoreCase);

            Assert.False(
                bannedExtensions.Contains(extension) && !isAllowedPemTemplate,
                $"Tracked generated or private credential artifact is not allowed: {path}");
        }
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required repository hygiene file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static IReadOnlyList<string> GetTrackedFiles(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git to inspect tracked repository files.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"git ls-files failed: {error}");
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(searchValue, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += searchValue.Length;
        }

        return count;
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
