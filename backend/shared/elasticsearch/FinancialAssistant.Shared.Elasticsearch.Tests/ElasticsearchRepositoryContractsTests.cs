using FinancialAssistant.Shared.Elasticsearch;

namespace FinancialAssistant.Shared.Elasticsearch.Tests;

public sealed class ElasticsearchRepositoryContractsTests
{
    [Fact]
    public void IndexNames_RenderCanonicalReadAndWriteAliases()
    {
        var names = ElasticsearchIndexNames.Create(
            "local",
            "transaction-intake",
            "transaction");

        Assert.Equal(
            "fa-local-transaction-intake-transaction-read",
            names.ReadAlias);
        Assert.Equal(
            "fa-local-transaction-intake-transaction-write",
            names.WriteAlias);
    }

    [Fact]
    public void IndexNameProvider_ExposesValidatedNames()
    {
        var provider = new ElasticsearchIndexNameProvider(
            "test",
            "income",
            "income-entry");

        Assert.Equal(
            "fa-test-income-income-entry-read",
            provider.Names.ReadAlias);
    }

    [Theory]
    [InlineData("", "service", "entity")]
    [InlineData("dev", "", "entity")]
    [InlineData("dev", "service", "")]
    [InlineData("-dev", "service", "entity")]
    [InlineData("dev-", "service", "entity")]
    [InlineData("dev--east", "service", "entity")]
    [InlineData("Dev", "service", "entity")]
    [InlineData("dev", "service_name", "entity")]
    public void IndexNames_RejectInvalidSegments(
        string environment,
        string service,
        string entity)
    {
        Assert.Throws<ArgumentException>(
            () => ElasticsearchIndexNames.Create(environment, service, entity));
    }

    [Fact]
    public void IndexNames_RejectAliasesLongerThanElasticsearchLimit()
    {
        var oversizedService = new string('a', 245);

        Assert.Throws<ArgumentException>(
            () => ElasticsearchIndexNames.Create(
                "dev",
                oversizedService,
                "transaction"));
    }

    [Fact]
    public void ConcurrencyToken_AcceptsValidSequenceNumberAndPrimaryTerm()
    {
        var token = new ElasticsearchConcurrencyToken(0, 1);

        Assert.Equal(0, token.SequenceNumber);
        Assert.Equal(1, token.PrimaryTerm);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    public void ConcurrencyToken_RejectsInvalidValues(
        long sequenceNumber,
        long primaryTerm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ElasticsearchConcurrencyToken(
                sequenceNumber,
                primaryTerm));
    }

    [Fact]
    public void StoredDocument_RequiresIdentitySourceAndConcurrencyToken()
    {
        var token = new ElasticsearchConcurrencyToken(3, 2);

        Assert.Throws<ArgumentException>(
            () => new ElasticsearchStoredDocument<TestDocument>(
                " ",
                new TestDocument("draft-1"),
                token));
        Assert.Throws<ArgumentNullException>(
            () => new ElasticsearchStoredDocument<TestDocument>(
                "document-1",
                null!,
                token));
        Assert.Throws<ArgumentNullException>(
            () => new ElasticsearchStoredDocument<TestDocument>(
                "document-1",
                new TestDocument("draft-1"),
                null!));
    }

    [Fact]
    public void WriteResult_RequiresConcurrencyToken()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ElasticsearchWriteResult(true, null!));
    }

    [Fact]
    public async Task MappingBootstrapHook_SupportsIdempotentServiceBootstrap()
    {
        var bootstrap = new RecordingMappingBootstrap();

        await bootstrap.EnsureCurrentMappingAsync();
        await bootstrap.EnsureCurrentMappingAsync();

        Assert.Equal(2, bootstrap.InvocationCount);
    }

    private sealed record TestDocument(string DraftId);

    private sealed class RecordingMappingBootstrap : IElasticsearchMappingBootstrap
    {
        public int InvocationCount { get; private set; }

        public Task EnsureCurrentMappingAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            return Task.CompletedTask;
        }
    }
}
