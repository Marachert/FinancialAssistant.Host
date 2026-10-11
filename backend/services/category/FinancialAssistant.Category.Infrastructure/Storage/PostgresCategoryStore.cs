using System.Text.Json.Serialization;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Domain.Categories;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Category.Infrastructure.Storage;

public sealed class PostgresCategoryStore(CategoryPersistenceContext context) : ICategoryStore
{
    public Task<UserCategoryCatalog> CreateDefaultIfMissingAsync(UserCategoryCatalog catalog, CancellationToken cancellationToken) =>
        context.WithRecordsAsync(catalog.UserId, async (records, _) =>
        {
            if (await records.GetAsync("deleted_catalogs", catalog.UserId, "marker") is not null) { throw new StorageUnavailableException(); }
            var existing = await records.GetAsync("catalogs", catalog.UserId, "catalog");
            if (existing is not null) { return Restore(existing, catalog.UserId); }
            await records.InsertAsync("catalogs", catalog.UserId, "catalog", Snapshot(catalog));
            return catalog;
        }, cancellationToken);

    public Task<UserCategoryCatalog?> GetAsync(string userId, CancellationToken cancellationToken) =>
        context.WithRecordsAsync(userId, async (records, _) =>
        {
            if (await records.GetAsync("deleted_catalogs", userId, "marker") is not null) { return null; }
            var existing = await records.GetAsync("catalogs", userId, "catalog");
            return existing is null ? null : Restore(existing, userId);
        }, cancellationToken);

    public Task<CategoryStoreUpdateResult?> ReplaceAliasesAsync(string userId, string categoryId,
        IReadOnlyCollection<string> aliases, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken) =>
        context.WithRecordsAsync(userId, async (records, _) =>
        {
            if (await records.GetAsync("deleted_catalogs", userId, "marker") is not null) { return null; }
            var existing = await records.GetAsync("catalogs", userId, "catalog");
            if (existing is null) { return null; }
            var catalog = Restore(existing, userId);
            var category = catalog.Find(categoryId);
            if (category is null) { return null; }
            var updated = category.ReplaceAliases(aliases, updatedAtUtc);
            if (ReferenceEquals(category, updated)) { return new CategoryStoreUpdateResult(category, false); }
            await records.ReplaceAsync("catalogs", userId, "catalog", existing.Revision, Snapshot(catalog.Replace(updated)));
            return new CategoryStoreUpdateResult(updated, true);
        }, cancellationToken);

    public Task<bool> DeleteAsync(string userId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync(userId, async (records, _) =>
        {
            var existing = await records.GetAsync("catalogs", userId, "catalog");
            if (existing is not null) { await records.DeleteAsync("catalogs", userId, "catalog", existing.Revision); }
            if (await records.GetAsync("deleted_catalogs", userId, "marker") is null)
            { await records.InsertAsync("deleted_catalogs", userId, "marker", OwnedRecordCodec.Encode(1, new DeletionMarker(nowUtc))); }
            return existing is not null;
        }, cancellationToken);

    private static byte[] Snapshot(UserCategoryCatalog catalog)
    {
        UserCategoryCatalog.Rehydrate(catalog.UserId, catalog.Categories);
        foreach (var category in catalog.Categories)
        {
            CategoryDefinition.Rehydrate(category.Id, category.Key, category.DisplayName, category.LocalizationKey,
                category.IconKey, category.ColorKey, category.Kind, category.SortOrder, category.Aliases,
                category.Version, category.CreatedAtUtc, category.UpdatedAtUtc);
        }
        return OwnedRecordCodec.Encode(1, new CatalogState(catalog.UserId, catalog.Categories.Select(category =>
            new CategoryState(category.Id, category.Key, category.DisplayName, category.LocalizationKey, category.IconKey,
                category.ColorKey, category.Kind, category.SortOrder, category.Aliases.ToArray(), category.Version,
                category.CreatedAtUtc, category.UpdatedAtUtc)).ToArray()));
    }

    private static UserCategoryCatalog Restore(DurableRecord record, string owner)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<CatalogState>(1, record);
            if (state.UserId != owner || state.Categories is null || state.Categories.Any(category => category is null)) { throw new StorageUnavailableException(); }
            var categories = state.Categories.Select(category => CategoryDefinition.Rehydrate(category.Id, category.Key,
                category.DisplayName, category.LocalizationKey, category.IconKey, category.ColorKey, category.Kind,
                category.SortOrder, category.Aliases, category.Version, category.CreatedAtUtc, category.UpdatedAtUtc)).ToArray();
            return UserCategoryCatalog.Rehydrate(state.UserId, categories);
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record CatalogState([property: JsonRequired] string UserId, [property: JsonRequired] CategoryState[] Categories);
    private sealed record CategoryState([property: JsonRequired] string Id, [property: JsonRequired] string Key,
        [property: JsonRequired] string DisplayName, [property: JsonRequired] string LocalizationKey,
        [property: JsonRequired] string IconKey, [property: JsonRequired] string ColorKey, [property: JsonRequired] string Kind,
        [property: JsonRequired] int SortOrder, [property: JsonRequired] string[] Aliases, [property: JsonRequired] int Version,
        [property: JsonRequired] DateTimeOffset CreatedAtUtc, [property: JsonRequired] DateTimeOffset UpdatedAtUtc);
    private sealed record DeletionMarker(DateTimeOffset DeletedAtUtc);
}
