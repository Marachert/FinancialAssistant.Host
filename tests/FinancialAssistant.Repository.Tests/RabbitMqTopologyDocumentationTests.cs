using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class RabbitMqTopologyDocumentationTests
{
    [Fact]
    public void RabbitMqTopology_DefinesCanonicalNamesAndRouting()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");
        var normalizedGuide = NormalizeWhitespace(guide);

        Assert.Contains("Related Jira: FIN-62", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("fa-{environment}", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("fa.events", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("fa.retry", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("fa.dead-letter", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("{domain}.{action}.v{schemaVersion}", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("fa.{consumer}.{purpose}.v{consumerContractVersion}", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("transaction.confirmed.v1", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(
            "The routing key is the complete event type, unchanged.",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Bindings such as `#` or `*.#` are forbidden",
            normalizedGuide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_DefinesBoundedRetryAndDeadLetterHandling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");
        var normalizedGuide = NormalizeWhitespace(guide);

        Assert.Contains("three delayed retries", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(".retry.5s", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(".retry.30s", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(".retry.5m", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(
            "Never use immediate requeue loops, unbounded retries",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            "{applicationQueue}.dead-letter",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Dead-letter messages are not replayed automatically.",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains("x-dead-letter-exchange=fa.dead-letter", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("x-dead-letter-exchange=fa.events", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("rejected without requeue", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(
            "preserves the original `eventId`",
            normalizedGuide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_DefinesAtLeastOnceOutboxAndInboxBoundaries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");
        var normalizedGuide = NormalizeWhitespace(guide);

        Assert.Contains("at-least-once delivery", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("Exactly-once delivery is not claimed.", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("publisher confirms", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("mandatory messages", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("durable service-owned outbox", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("durable inbox", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains("keyed by `eventId`", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(
            "acknowledges the RabbitMQ delivery only after the durable result succeeds",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            "RabbitMQ transports facts and is never a source of truth.",
            normalizedGuide,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RabbitMqTopology_IsDiscoverableAndPrivacySafe()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guide = ReadRequiredFile(repositoryRoot, "docs/events/rabbitmq-topology.md");
        var normalizedGuide = NormalizeWhitespace(guide);
        var eventsReadme = ReadRequiredFile(repositoryRoot, "docs/events/README.md");
        var documentationReadme = ReadRequiredFile(repositoryRoot, "docs/README.md");

        Assert.Contains(
            "[RabbitMQ Topology and Delivery Conventions](rabbitmq-topology.md)",
            eventsReadme,
            StringComparison.Ordinal);
        Assert.Contains("docs/events/rabbitmq-topology.md", documentationReadme, StringComparison.Ordinal);
        Assert.Contains("Use synthetic messages only.", normalizedGuide, StringComparison.Ordinal);
        Assert.Contains(
            "must not include message payloads",
            normalizedGuide,
            StringComparison.Ordinal);
        Assert.Contains(
            "Production credentials, permissions, and virtual hosts must never be shared",
            normalizedGuide,
            StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string content) =>
        string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
