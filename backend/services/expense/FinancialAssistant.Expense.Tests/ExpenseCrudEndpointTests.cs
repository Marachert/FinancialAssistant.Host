using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Expense.Contracts;
using Xunit;

namespace FinancialAssistant.Expense.Tests;

public sealed class ExpenseCrudEndpointTests : IClassFixture<ExpenseWebApplicationFactory>
{
    private readonly HttpClient client;

    public ExpenseCrudEndpointTests(ExpenseWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUpdateAndRead_AreOwnerScopedAndNormalized()
    {
        const string userId = "synthetic-expense-crud-owner";
        var created = await CreateAsync(
            userId,
            new CreateExpenseRequest(
                1250.129m,
                "usd",
                "expense.groceries",
                "  Synthetic   Employer ",
                DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Equal("active", created.Status);
        Assert.Equal("manual", created.Origin);
        Assert.Equal(1250.13m, created.Amount);
        Assert.Equal("USD", created.Currency);
        Assert.Equal("Synthetic Market", created.Merchant);

        using var otherRequest = CreateRequest(
            HttpMethod.Get,
            ExpenseApiRoutes.Expense,
            "synthetic-expense-other-owner",
            created.Id);
        using var otherResponse = await client.SendAsync(otherRequest);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);

        using var updateRequest = CreateRequest(
            HttpMethod.Put,
            ExpenseApiRoutes.GatewayExpense,
            userId,
            created.Id,
            new UpdateExpenseRequest(
                1300m,
                "eur",
                "expense.utilities",
                "Synthetic Utility",
                created.Date));
        using var updateResponse = await client.SendAsync(updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ExpenseRecordResponse>();

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
        const string userId = "synthetic-expense-list-owner";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = await CreateAsync(
            userId,
            new CreateExpenseRequest(100m, "USD", "expense.groceries", null, date));
        _ = await CreateAsync(
            userId,
            new CreateExpenseRequest(50m, "USD", "expense.utilities", null, date));

        var active = await ListAsync(userId, date.AddDays(-1), date.AddDays(1));
        Assert.Equal(2, active.Records.Count);
        Assert.Equal(150m, Assert.Single(active.ActiveTotals).Amount);

        using var archiveRequest = CreateRequest(
            HttpMethod.Post,
            ExpenseApiRoutes.Archive,
            userId,
            first.Id);
        using var archiveResponse = await client.SendAsync(archiveRequest);
        var archived = await archiveResponse.Content.ReadFromJsonAsync<ExpenseRecordResponse>();
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
            ExpenseApiRoutes.Expense,
            userId,
            first.Id,
            new UpdateExpenseRequest(200m, "USD", "expense.groceries", null, date));
        using var updateArchivedResponse = await client.SendAsync(updateArchivedRequest);
        Assert.Equal(HttpStatusCode.Conflict, updateArchivedResponse.StatusCode);

        using var restoreRequest = CreateRequest(
            HttpMethod.Post,
            ExpenseApiRoutes.GatewayRestore,
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
        const string userId = "synthetic-expense-invalid-owner";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requests = new[]
        {
            new CreateExpenseRequest(0m, "USD", "expense.groceries", null, today),
            new CreateExpenseRequest(10m, "JPY", "expense.groceries", null, today),
            new CreateExpenseRequest(10m, "USD", "income.salary", null, today),
            new CreateExpenseRequest(10m, "USD", "expense.groceries", null, today.AddDays(367))
        };

        foreach (var request in requests)
        {
            using var message = CreateRequest(
                HttpMethod.Post,
                ExpenseApiRoutes.Expenses,
                userId,
                body: request);
            using var response = await client.SendAsync(message);
            var problem = await response.Content.ReadFromJsonAsync<ExpenseApiErrorResponse>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(problem);
            Assert.Equal("invalid_expense_request", problem.Code);
        }
    }

    [Fact]
    public async Task ExpenseRoutes_RequireTrustedGatewayContext()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ExpenseApiRoutes.Expenses}?from=2026-01-01&to=2026-12-31");
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ExpenseApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("trusted_gateway_authentication_required", problem.Code);
    }

    private async Task<ExpenseRecordResponse> CreateAsync(
        string userId,
        CreateExpenseRequest body)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            ExpenseApiRoutes.Expenses,
            userId,
            body: body);
        using var response = await client.SendAsync(request);
        var expense = await response.Content.ReadFromJsonAsync<ExpenseRecordResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(expense);
        return expense;
    }

    private async Task<ExpenseListResponse> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived = false)
    {
        var route =
            $"{ExpenseApiRoutes.GatewayExpenses}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&includeArchived={includeArchived}";
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<ExpenseListResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        return result;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string route,
        string userId,
        string? expenseId = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(
            method,
            expenseId is null
                ? route
                : route.Replace("{expenseId}", expenseId, StringComparison.Ordinal));
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
            ExpenseGatewayHeaders.Authentication,
            ExpenseWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(ExpenseGatewayHeaders.UserId, userId);
    }
}
