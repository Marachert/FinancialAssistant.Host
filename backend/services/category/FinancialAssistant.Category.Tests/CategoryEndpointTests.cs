using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Category.Contracts;
using FinancialAssistant.Category.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryEndpointTests : IClassFixture<CategoryContractWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly InMemoryCategoryEventPublisher eventPublisher;

    public CategoryEndpointTests(CategoryContractWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        eventPublisher = factory.Services.GetRequiredService<InMemoryCategoryEventPublisher>();
    }

    [Fact]
    public async Task UserRegisteredEvent_SeedsStableDefaultTaxonomyIdempotently()
    {
        const string userId = "synthetic-category-seed";
        var occurredAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

        var first = await SeedDefaultsAsync(userId, occurredAt);
        var second = await SeedDefaultsAsync(userId, occurredAt.AddHours(1));

        Assert.Equal(11, first.Count);
        Assert.Equal(first.Select(category => category.Id), second.Select(category => category.Id));
        Assert.Equal(first.Select(category => category.CreatedAtUtc), second.Select(category => category.CreatedAtUtc));
        Assert.Equal("income.salary", first[0].Id);
        Assert.Equal("expense.other", first[^1].Id);
        Assert.All(first, category => Assert.Equal(1, category.Version));
    }

    [Fact]
    public async Task Search_MatchesDefaultAliasesDeterministically()
    {
        const string userId = "synthetic-category-search";
        await SeedDefaultsAsync(userId);

        using var request = CreateUserRequest(
            HttpMethod.Get,
            "/categories?query=groceries",
            userId);
        var response = await client.SendAsync(request);
        var categories = await response.Content.ReadFromJsonAsync<CategoryResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(categories);
        var category = Assert.Single(categories);
        Assert.Equal("expense.food", category.Id);
    }

    [Fact]
    public async Task ReplaceAliases_IsUserScopedSearchableAndPublishesSafeEvent()
    {
        const string userId = "synthetic-category-alias-owner";
        const string otherUserId = "synthetic-category-alias-other";
        await SeedDefaultsAsync(userId);
        await SeedDefaultsAsync(otherUserId);

        using var updateRequest = CreateUserRequest(
            HttpMethod.Put,
            "/categories/expense.food/aliases",
            userId,
            new UpdateCategoryAliasesRequest(
                new[] { "Morning Brew", "Coffee Shop" },
                "synthetic-category-correlation"));
        var updateResponse = await client.SendAsync(updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(new[] { "coffee shop", "morning brew" }, updated.Aliases);
        Assert.Equal(2, updated.Version);

        using var ownerSearch = CreateUserRequest(
            HttpMethod.Get,
            "/categories?query=morning%20brew",
            userId);
        var ownerResponse = await client.SendAsync(ownerSearch);
        var ownerMatches = await ownerResponse.Content.ReadFromJsonAsync<CategoryResponse[]>();
        Assert.NotNull(ownerMatches);
        Assert.Equal("expense.food", Assert.Single(ownerMatches).Id);

        using var otherSearch = CreateUserRequest(
            HttpMethod.Get,
            "/categories?query=morning%20brew",
            otherUserId);
        var otherResponse = await client.SendAsync(otherSearch);
        var otherMatches = await otherResponse.Content.ReadFromJsonAsync<CategoryResponse[]>();
        Assert.NotNull(otherMatches);
        Assert.Empty(otherMatches);

        var published = Assert.Single(
            eventPublisher.PublishedEvents,
            item => item.UserId == userId);
        Assert.Equal(CategoryUpdatedIntegrationEvent.Name, published.EventType);
        Assert.Equal("expense.food", published.CategoryId);
        Assert.Equal(2, published.Version);
        Assert.Equal("aliases_replaced", published.ChangeType);
        Assert.Equal("synthetic-category-correlation", published.CorrelationId);
    }

    [Fact]
    public async Task ReplacingAliasesWithSameNormalizedValues_DoesNotPublishDuplicateEvent()
    {
        const string userId = "synthetic-category-idempotent-alias";
        await SeedDefaultsAsync(userId);

        await ReplaceAliasesAsync(userId, new[] { "Coffee Shop", "Morning Brew" });
        await ReplaceAliasesAsync(userId, new[] { " morning   brew ", "coffee shop" });

        Assert.Single(eventPublisher.PublishedEvents, item => item.UserId == userId);
    }

    [Fact]
    public async Task InvalidAliases_ReturnProblemDetailsContract()
    {
        const string userId = "synthetic-category-invalid-alias";
        await SeedDefaultsAsync(userId);

        using var request = CreateUserRequest(
            HttpMethod.Put,
            "/categories/expense.food/aliases",
            userId,
            new UpdateCategoryAliasesRequest(new[] { " " }, null));
        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<CategoryApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal("Category request is invalid.", problem.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_category_aliases", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Categories_WithoutGatewayUserContext_ReturnUnauthorized()
    {
        var response = await client.GetAsync(CategoryApiRoutes.Categories);
        var problem = await response.Content.ReadFromJsonAsync<CategoryApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("authentication_required", problem.Code);
    }

    private async Task<IReadOnlyList<CategoryResponse>> SeedDefaultsAsync(
        string userId,
        DateTimeOffset? occurredAt = null)
    {
        var response = await client.PostAsJsonAsync(
            CategoryApiRoutes.UserRegisteredEvent,
            new UserRegisteredCategoryEvent(
                userId,
                occurredAt ?? DateTimeOffset.UtcNow,
                "synthetic-registration-correlation",
                "synthetic-registration-causation"));
        var categories = await response.Content.ReadFromJsonAsync<CategoryResponse[]>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(categories);
        return categories;
    }

    private async Task ReplaceAliasesAsync(string userId, IReadOnlyCollection<string> aliases)
    {
        using var request = CreateUserRequest(
            HttpMethod.Put,
            "/categories/expense.food/aliases",
            userId,
            new UpdateCategoryAliasesRequest(aliases, "synthetic-idempotency-correlation"));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateUserRequest(
        HttpMethod method,
        string route,
        string userId,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, route);
        request.Headers.TryAddWithoutValidation(CategoryGatewayHeaders.UserId, userId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
