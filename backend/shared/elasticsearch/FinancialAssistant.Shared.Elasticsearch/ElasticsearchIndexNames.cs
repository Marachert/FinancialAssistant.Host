namespace FinancialAssistant.Shared.Elasticsearch;

public interface IElasticsearchIndexNameProvider
{
    ElasticsearchIndexNames Names { get; }
}

public sealed class ElasticsearchIndexNameProvider : IElasticsearchIndexNameProvider
{
    public ElasticsearchIndexNameProvider(
        string environment,
        string service,
        string entity)
    {
        Names = ElasticsearchIndexNames.Create(environment, service, entity);
    }

    public ElasticsearchIndexNames Names { get; }
}

public sealed record ElasticsearchIndexNames
{
    private const int MaximumNameLength = 255;

    private ElasticsearchIndexNames(
        string environment,
        string service,
        string entity,
        string readAlias,
        string writeAlias)
    {
        Environment = environment;
        Service = service;
        Entity = entity;
        ReadAlias = readAlias;
        WriteAlias = writeAlias;
    }

    public string Environment { get; }

    public string Service { get; }

    public string Entity { get; }

    public string ReadAlias { get; }

    public string WriteAlias { get; }

    public static ElasticsearchIndexNames Create(
        string environment,
        string service,
        string entity)
    {
        ValidateSegment(environment, nameof(environment));
        ValidateSegment(service, nameof(service));
        ValidateSegment(entity, nameof(entity));

        var prefix = $"fa-{environment}-{service}-{entity}";
        var readAlias = $"{prefix}-read";
        var writeAlias = $"{prefix}-write";

        if (readAlias.Length > MaximumNameLength ||
            writeAlias.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Rendered aliases must not exceed {MaximumNameLength} ASCII characters.");
        }

        return new ElasticsearchIndexNames(
            environment,
            service,
            entity,
            readAlias,
            writeAlias);
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Name segment must not be empty.", parameterName);
        }

        if (value[0] == '-' || value[^1] == '-')
        {
            throw new ArgumentException(
                "Name segment must not start or end with a hyphen.",
                parameterName);
        }

        var previousWasHyphen = false;
        foreach (var character in value)
        {
            var isHyphen = character == '-';
            var isLowerAsciiLetter = character is >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';

            if ((!isLowerAsciiLetter && !isDigit && !isHyphen) ||
                (isHyphen && previousWasHyphen))
            {
                throw new ArgumentException(
                    "Name segment must use lowercase ASCII letters, digits, and single hyphens.",
                    parameterName);
            }

            previousWasHyphen = isHyphen;
        }
    }
}
