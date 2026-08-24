using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class WindowsPocDeploymentTests
{
    private static readonly string[] RequiredServices =
    [
        "reverse-proxy",
        "public-api-gateway",
        "identity",
        "profile",
        "category",
        "transaction-intake",
        "receipt-processing",
        "income",
        "expense",
        "ai-orchestration",
        "analytics",
        "financial-score",
        "recommendations-notifications",
        "monitoring",
        "audit",
        "mcp",
        "postgres",
        "elasticsearch",
        "rabbitmq",
        "redis",
        "minio",
        "prometheus",
        "grafana"
    ];

    private static readonly string[] RequiredSecretNames =
    [
        "POSTGRES_PASSWORD",
        "RABBITMQ_PASSWORD",
        "REDIS_PASSWORD",
        "MINIO_ROOT_PASSWORD",
        "GRAFANA_ADMIN_PASSWORD",
        "IDENTITY_LOOKUP_HMAC_KEY",
        "IDENTITY_REFRESH_TOKEN_HASH_KEY",
        "IDENTITY_ACCESS_TOKEN_SIGNING_KEY",
        "IDENTITY_PROVIDER_HMAC_KEY",
        "IDENTITY_EVENT_USER_HMAC_KEY",
        "INTERNAL_GATEWAY_SHARED_SECRET",
        "INTERNAL_SERVICE_SHARED_SECRET",
        "RECEIPT_EVENT_SHARED_SECRET",
        "MONITORING_SIGNAL_SHARED_SECRET",
        "MCP_SHARED_SECRET"
    ];

    [Fact]
    public void Compose_DefinesCompleteInternalServiceTopology()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = ReadRequiredFile(repositoryRoot, "infra/windows-poc/compose.yml");

        foreach (var service in RequiredServices)
        {
            Assert.Matches($"(?m)^  {Regex.Escape(service)}:$", compose);
        }

        Assert.Contains("internal: true", compose, StringComparison.Ordinal);
        Assert.Contains("condition: service_healthy", compose, StringComparison.Ordinal);
        Assert.Contains("path.repo: /usr/share/elasticsearch/snapshots", compose, StringComparison.Ordinal);
        Assert.Contains("Identity__Events__Mode: RabbitMq", compose, StringComparison.Ordinal);
        Assert.Contains("RecommendationsNotifications__Delivery__Push__Enabled: \"false\"", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_BuildArgumentsReferenceExistingApiProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = ReadRequiredFile(repositoryRoot, "infra/windows-poc/compose.yml");
        var matches = Regex.Matches(compose, @"(?m)^\s+PROJECT_PATH: (?<path>[^\r\n]+)$");

        Assert.Equal(15, matches.Count);
        foreach (Match match in matches)
        {
            var path = match.Groups["path"].Value.Trim();
            Assert.True(File.Exists(ToRepositoryPath(repositoryRoot, path)), $"Compose project does not exist: {path}");
        }
    }

    [Fact]
    public void Compose_RequiresExternalSecretsAndRestrictsPublishedPorts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = ReadRequiredFile(repositoryRoot, "infra/windows-poc/compose.yml");
        var environment = ReadRequiredFile(repositoryRoot, "infra/windows-poc/.env.poc.example");

        foreach (var secret in RequiredSecretNames)
        {
            Assert.Contains($"${{{secret}:?", compose, StringComparison.Ordinal);
            Assert.Matches($"(?m)^{Regex.Escape(secret)}=REQUIRED_", environment);
        }

        var publishedPorts = Regex.Matches(compose, "(?m)^\\s+- \\\"(?<mapping>[^\\\"]+:\\d+)\\\"$")
            .Select(match => match.Groups["mapping"].Value)
            .ToArray();
        var publicMappings = publishedPorts
            .Where(mapping => !mapping.StartsWith("127.0.0.1:", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["${POC_HTTP_PORT:-8080}:8080"], publicMappings);
        Assert.DoesNotContain("latest", environment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeImageAndProxy_EnforceExpectedSecurityBoundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerfile = ReadRequiredFile(repositoryRoot, "infra/windows-poc/backend.Dockerfile");
        var proxy = ReadRequiredFile(repositoryRoot, "infra/windows-poc/nginx/nginx.conf");
        var dockerIgnore = ReadRequiredFile(repositoryRoot, ".dockerignore");

        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:8.0.15-bookworm-slim", dockerfile, StringComparison.Ordinal);
        Assert.Contains("USER app", dockerfile, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://public_api_gateway", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain("$request_uri", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain("log_format privacy_safe '$remote_addr", proxy, StringComparison.Ordinal);
        Assert.DoesNotContain("$http_authorization", proxy, StringComparison.Ordinal);
        Assert.Contains("**/.env.*", dockerIgnore, StringComparison.Ordinal);
        Assert.Contains(".codex-runtime", dockerIgnore, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorScripts_AreNonInteractiveAndBackupOnlyAllowlistedVolumes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var common = ReadRequiredFile(repositoryRoot, "infra/windows-poc/scripts/common.ps1");
        var backup = ReadRequiredFile(repositoryRoot, "infra/windows-poc/scripts/backup.ps1");
        var restore = ReadRequiredFile(repositoryRoot, "infra/windows-poc/scripts/restore.ps1");
        var runbook = ReadRequiredFile(repositoryRoot, "infra/windows-poc/README.md");

        Assert.Contains("$ErrorActionPreference = \"Stop\"", common, StringComparison.Ordinal);
        Assert.Contains("^[A-Za-z0-9_-]+$", common, StringComparison.Ordinal);
        Assert.Contains("fa-poc-postgres-data", common, StringComparison.Ordinal);
        Assert.Contains("fa-poc-grafana-data", common, StringComparison.Ordinal);
        Assert.Contains("_snapshot/fa-poc-backups", backup, StringComparison.Ordinal);
        Assert.Contains("Invoke-PocCompose", backup, StringComparison.Ordinal);
        Assert.Contains("[Parameter(Mandatory)][switch]$ConfirmRestore", restore, StringComparison.Ordinal);
        Assert.Contains("Restore replaces the eight fixed PoC volumes", restore, StringComparison.Ordinal);
        Assert.Contains("first-user testing", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no paid provider credits", runbook, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BackendCi_ValidatesResolvedComposeModel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = ReadRequiredFile(repositoryRoot, ".github/workflows/backend-ci.yml");

        Assert.Contains("Validate Windows PoC Compose model", workflow, StringComparison.Ordinal);
        Assert.Contains("--env-file infra/windows-poc/.env.poc.example", workflow, StringComparison.Ordinal);
        Assert.Contains("--file infra/windows-poc/compose.yml", workflow, StringComparison.Ordinal);
        Assert.Contains("config --quiet", workflow, StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required Windows PoC asset is missing: {path}");
        return File.ReadAllText(fullPath);
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

        throw new DirectoryNotFoundException("Could not locate the Financial Assistant repository root.");
    }
}
