namespace FinancialAssistant.Identity.Infrastructure.Storage;

public sealed record IdentityOwnedIndexDefinition(
    string Entity,
    int SchemaVersion,
    string PhysicalIndex,
    string ReadAlias,
    string WriteAlias);
