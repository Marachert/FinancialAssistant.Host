using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Income.Contracts;
using Xunit;

namespace FinancialAssistant.Income.Tests;

public sealed class IncomeCrudEndpointTests : IClassFixture<IncomeWebApplicationFactory>
{
    private readonly HttpClient client;

    public IncomeCrudEndpointTests(IncomeWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUpdateAndRead_AreOwnerScopedAndNormalized()
    {
        const string userId = "synthetic-income-crud-owner";
        var created = await CreateAsync(
            userId,
            new CreateIncomeRequest(
                1250.129m,
                "usd",
                "income.salary",
                "  Synthetic   Employer ",
                DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal("active", created.Status);
        Assert.Equal("manual", created.Origin);
        Assert.Equal(1250.13m, created.Amount);
        Assert.Equal("USD", created.Currency);
        Assert.Equal("Synthetic Employer", created.Merchant);

        using var otherRequest = CreateRequest(
            HttpMethod.Get,
            IncomeApiRoutes.Income,
            "synthetic-income-other-owner",
            created.Id);
        using var otherResponse = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);

        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            IncomeApiRoutes.GatewayIncome,
            userId,
            created.Id,
            new UpdateIncomeRequest(
                1300m,
                "eur",
                "income.freelance",
                "Synthetic Client",
                created.Date));
        using var updateResponse = await client.SendAsync(updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<IncomeRecordResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Revision);
        Assert.Equal(1300m, updated.Amount);
        Assert.Equal("EUR", updated.Currency);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task ListByPeriod_ExcludesArchivedRecordsFromRecordsAndTotalsByDefault()
    {
        const string userId = "synthetic-income-list-owner";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = await CreateAsync(
            userId,
            new CreateIncomeRequest(100m, "USD", "income.salary", null, date));
        _ = await CreateAsync(
            userId,
            new CreateIncomeRequest(50m, "USD", "income.freelance", null, date));

        var active = await ListAsync(userId, date.AddDays(-1), date.AddDays(1));
        Assert.Equal(2, active.Records.Count);
        Assert.Equal(150m, Assert.Single(active.ActiveTotals).Amount);

        using var archiveRequest = CreateRequest(
            HttpMethod.Post,
            IncomeApiRoutes.Archive,
            userId,
            first.Id);
        using var archiveResponse = await client.SendAsync(archiveRequest);
        var archived = await archiveResponse.Content.ReadFromJsonAsync<IncomeRecordResponse>();
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
        Assert.NotNull(archived);
        Assert.Equal("archived", archived.Status);

        var afterArchive = await ListAsync(userId, date.AddDays(-1), date.AddDays(1));
        Assert.Single(afterArchive.Records);
        Assert.Equal(50m, Assert.Single(afterArchive.ActiveTotals).Amount);

        var includingArchived = await ListAsync(
            userId,
            date.AddDays(-1),
            date.AddDays(1),
            includeArchived: true);
        Assert.Equal(2, includingArchived.Records.Count);
        Assert.Equal(50m, Assert.Single(includingArchived.ActiveTotals).Amount);

        using var updateArchivedRequest = CreateRequest(
            HttpMethod.Put,
            IncomeApiRoutes.Income,
            userId,
            first.Id,
            new UpdateIncomeRequest(200m, "USD", "income.salary", null, date));
        using var updateArchivedResponse = await client.SendAsync(updateArchivedRequest);
        Assert.Equal(HttpStatusCode.Conflict, updateArchivedResponse.StatusCode);

        using var restoreRequest = CreateRequest(
            HttpMethod.Post,
            IncomeApiRoutes.GatewayRestore,
            userId,
            first.Id);
        using var restoreResponse = await client.SendAsync(restoreRequest);
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);

        var restored = await ListAsync(userId, date.AddDays(-1), date.AddDays(1));
        Assert.Equal(2, restored.Records.Count);
        Assert.Equal(150m, Assert.Single(restored.ActiveTotals).Amount);
    }

    [Fact]
    public async Task Create_RejectsInvalidFinancialValues()
    {
        const string userId = "synthetic-income-invalid-owner";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requests = new[]
        {
            new CreateIncomeRequest(0m, "USD", "income.salary", null, today),
            new CreateIncomeRequest(10m, "JPY", "income.salary", null, today),
            new CreateIncomeRequest(10m, "USD", "expense.salary", null, today),
            new CreateIncomeRequest(10m, "USD", "income.salary", null, today.AddDays(367))
        };

        foreach (var request in requests)
        {
            using var message = CreateRequest(
                HttpMethod.Post,
                IncomeApiRoutes.Incomes,
                userId,
                body: request);
            using var response = await client.SendAsync(message);
            var problem = await response.Content.ReadFromJsonAsync<IncomeApiErrorResponse>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(problem);
            Assert.Equal("invalid_income_request", problem.Code);
        }
    }

    [Fact]
    public async Task IncomeRoutes_RequireTrustedGatewayContext()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{IncomeApiRoutes.Incomes}?from=2026-01-01&to=2026-12-31");
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<IncomeApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("trusted_gateway_authentication_required", problem.Code);
    }

    private async Task<IncomeRecordResponse> CreateAsync(
        string userId,
        CreateIncomeRequest body)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            IncomeApiRoutes.Incomes,
            userId,
            body: body);
        using var response = await client.SendAsync(request);
        var income = await response.Content.ReadFromJsonAsync<IncomeRecordResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(income);
        return income;
    }

    private async Task<IncomeListResponse> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived = false)
    {
        var route =
            $"{IncomeApiRoutes.GatewayIncomes}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&includeArchived={includeArchived}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<IncomeListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        return result;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string route,
        string userId,
        string? incomeId = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(
            method,
            incomeId is null
                ? route
                : route.Replace("{incomeId}", incomeId, StringComparison.Ordinal));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        AddTrustedHeaders(request, userId);
        return request;
    }

    private static void AddTrustedHeaders(HttpRequestMessage request, string userId)
    {
        request.Headers.TryAddWithoutValidation(
            IncomeGatewayHeaders.Authentication,
            IncomeWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(IncomeGatewayHeaders.UserId, userId);
    }
}
