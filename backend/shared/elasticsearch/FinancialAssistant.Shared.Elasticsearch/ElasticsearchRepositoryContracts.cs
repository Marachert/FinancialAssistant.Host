namespace FinancialAssistant.Shared.Elasticsearch;

public interface IElasticsearchRepository<TDocument>
    where TDocument : notnull
{
    ElasticsearchIndexNames IndexNames { get; }

    Task<ElasticsearchStoredDocument<TDocument>?> GetAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<ElasticsearchWriteResult> SaveAsync(
        string documentId,
        TDocument document,
        ElasticsearchConcurrencyToken? expectedConcurrencyToken = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string documentId,
        ElasticsearchConcurrencyToken? expectedConcurrencyToken = null,
        CancellationToken cancellationToken = default);
}

public sealed record ElasticsearchStoredDocument<TDocument>
    where TDocument : notnull
{
    public ElasticsearchStoredDocument(
        string documentId,
        TDocument source,
        ElasticsearchConcurrencyToken concurrencyToken)
    {
        DocumentId = RequireDocumentId(documentId);
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ConcurrencyToken = concurrencyToken;
    }

    public string DocumentId { get; }

    public TDocument Source { get; }

    public ElasticsearchConcurrencyToken ConcurrencyToken { get; }

    private static string RequireDocumentId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException(
                "Document ID must not be empty.",
                nameof(documentId));
        }

        return documentId;
    }
}

public sealed record ElasticsearchWriteResult(
    bool Created,
    ElasticsearchConcurrencyToken ConcurrencyToken);

public readonly record struct ElasticsearchConcurrencyToken
{
    public ElasticsearchConcurrencyToken(
        long sequenceNumber,
        long primaryTerm)
    {
        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequenceNumber),
                sequenceNumber,
                "Sequence number must not be negative.");
        }

        if (primaryTerm < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(primaryTerm),
                primaryTerm,
                "Primary term must be positive.");
        }

        SequenceNumber = sequenceNumber;
        PrimaryTerm = primaryTerm;
    }

    public long SequenceNumber { get; }

    public long PrimaryTerm { get; }
}
