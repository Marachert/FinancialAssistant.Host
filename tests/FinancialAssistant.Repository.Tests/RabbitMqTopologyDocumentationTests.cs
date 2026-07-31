using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class RabbitMqTopologyDocumentationTests
{
    [Fact]
    public void RabbitMqTopology_DefinesCanonicalNamesAndRouting()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");

        Assert.Contains("Related Jira: FIN-62", guide, StringComparison.Ordinal);
        Assert.Contains("fa-{environment}", guide, StringComparison.Ordinal);
        Assert.Contains("fa.events", guide, StringComparison.Ordinal);
        Assert.Contains("fa.retry", guide, StringComparison.Ordinal);
        Assert.Contains("fa.dead-letter", guide, StringComparison.Ordinal);
        Assert.Contains("{domain}.{action}.v{schemaVersion}", guide, StringComparison.Ordinal);
        Assert.Contains("fa.{consumer}.{purpose}.v{consumerContractVersion}", guide, StringComparison.Ordinal);
        Assert.Contains("transaction.confirmed.v1", guide, StringComparison.Ordinal);
        Assert.Contains(
            "The routing key is the complete event type, unchanged.",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Bindings such as `#` or `*.#` are forbidden",
            guide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_DefinesBoundedRetryAndDeadLetterHandling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");

        Assert.Contains("three delayed retries", guide, StringComparison.Ordinal);
        Assert.Contains(".retry.5s", guide, StringComparison.Ordinal);
        Assert.Contains(".retry.30s", guide, StringComparison.Ordinal);
        Assert.Contains(".retry.5m", guide, StringComparison.Ordinal);
        Assert.Contains(
            "Never use immediate requeue loops, unbounded retries",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "{applicationQueue}.dead-letter",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Dead-letter messages are not replayed automatically.",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "preserves the original `eventId`",
            guide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_DefinesAtLeastOnceOutboxAndInboxBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");

        Assert.Contains("at-least-once delivery", guide, StringComparison.Ordinal);
        Assert.Contains("Exactly-once delivery is not claimed.", guide, StringComparison.Ordinal);
        Assert.Contains("publisher confirms", guide, StringComparison.Ordinal);
        Assert.Contains("mandatory messages", guide, StringComparison.Ordinal);
        Assert.Contains("durable service-owned outbox", guide, StringComparison.Ordinal);
        Assert.Contains("durable inbox", guide, StringComparison.Ordinal);
        Assert.Contains("keyed by `eventId`", guide, StringComparison.Ordinal);
        Assert.Contains(
            "acknowledges the RabbitMQ delivery only after the durable result succeeds",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "RabbitMQ transports facts and is never a source of truth.",
            guide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_IsDiscoverableAndPrivacySafe()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");
        var eventsReadme = ReadRequiredFile(repositoryRoot, "docs/events/README.md");
        var documentationReadme = ReadRequiredFile(repositoryRoot, "docs/README.md");

        Assert.Contains(
            "[RabbitMQ Topology and Delivery Conventions](rabbitmq-topology.md)",
            eventsReadme,
            StringComparison.Ordinal);
        Assert.Contains("docs/events/rabbitmq-topology.md", documentationReadme, StringComparison.Ordinal);
        Assert.Contains("Use synthetic messages only.", guide, StringComparison.Ordinal);
        Assert.Contains(
            "must not include message payloads",
            guide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Production credentials, permissions, and virtual hosts must never be shared",
            guide,
            StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required RabbitMQ documentation file '{path}' is missing.");
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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
