namespace FinancialAssistant.Shared.Contracts.Events;

public static class RecommendationEventTypes
{
    public const int SchemaVersion = 1;
    public const string RecommendationGenerated = "recommendation.generated.v1";
}

public sealed record RecommendationFactV1(
    string Code,
    decimal Value);

public sealed record RecommendationGeneratedV1(
    string RecommendationId,
    string Currency,
    string Code,
    string Severity,
    string Title,
    string Body,
    IReadOnlyList<RecommendationFactV1> Facts,
    DateTimeOffset GeneratedAtUtc);
