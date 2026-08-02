using FinancialAssistant.Expense.Api.Security;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Contracts;

namespace FinancialAssistant.Expense.Api.Endpoints;

public static class ExpenseEndpointExtensions
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        MapCollection(app, ExpenseApiRoutes.Expenses, "Expense");
        MapCollection(app, ExpenseApiRoutes.GatewayExpenses, "ExpenseFromGateway");
        MapRecord(app, ExpenseApiRoutes.Expense, "Expense");
        MapRecord(app, ExpenseApiRoutes.GatewayExpense, "ExpenseFromGateway");
        MapStatusCommand(app, ExpenseApiRoutes.Archive, "ArchiveExpense", HandleArchiveAsync);
        MapStatusCommand(
            app,
            ExpenseApiRoutes.GatewayArchive,
            "ArchiveExpenseFromGateway",
            HandleArchiveAsync);
        MapStatusCommand(app, ExpenseApiRoutes.Restore, "RestoreExpense", HandleRestoreAsync);
        MapStatusCommand(
            app,
            ExpenseApiRoutes.GatewayRestore,
            "RestoreExpenseFromGateway",
            HandleRestoreAsync);
        return app;
    }

    private static void MapCollection(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleCreateAsync)
            .WithName($"Create{name}")
            .Produces<ExpenseRecordResponse>(StatusCodes.Status201Created)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapGet(pattern, HandleListAsync)
            .WithName($"List{name}")
            .Produces<ExpenseListResponse>()
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static void MapRecord(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapGet(pattern, HandleGetAsync)
            .WithName($"Get{name}")
            .Produces<ExpenseRecordResponse>()
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPut(pattern, HandleUpdateAsync)
            .WithName($"Update{name}")
            .Produces<ExpenseRecordResponse>()
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static void MapStatusCommand(
        IEndpointRouteBuilder app,
        string pattern,
        string name,
        Delegate handler)
    {
        app.MapPost(pattern, handler)
            .WithName(name)
            .Produces<ExpenseRecordResponse>()
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ExpenseApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleCreateAsync(
        HttpContext httpContext,
        CreateExpenseRequest request,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var expense = await service.CreateAsync(userId!, request, cancellationToken);
            return Results.Created($"/api/v1/expenses/{expense.Id}", expense);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext httpContext,
        DateOnly from,
        DateOnly to,
        bool? includeArchived,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var expenses = await service.ListAsync(
                userId!,
                from,
                to,
                includeArchived ?? false,
                cancellationToken);
            return Results.Ok(expenses);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext httpContext,
        string expenseId,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var expense = await service.GetAsync(userId!, expenseId, cancellationToken);
            return expense is null ? NotFound(httpContext) : Results.Ok(expense);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext httpContext,
        string expenseId,
        UpdateExpenseRequest request,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var expense = await service.UpdateAsync(
                userId!,
                expenseId,
                request,
                cancellationToken);
            return expense is null ? NotFound(httpContext) : Results.Ok(expense);
        }
        catch (ExpenseRecordNotEditableException exception)
        {
            return Conflict(httpContext, exception.Message);
        }
        catch (ExpenseMutationConflictException exception)
        {
            return Conflict(httpContext, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static Task<IResult> HandleArchiveAsync(
        HttpContext httpContext,
        string expenseId,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            httpContext,
            expenseId,
            service,
            authenticator,
            service.ArchiveAsync,
            cancellationToken);

    private static Task<IResult> HandleRestoreAsync(
        HttpContext httpContext,
        string expenseId,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            httpContext,
            expenseId,
            service,
            authenticator,
            service.RestoreAsync,
            cancellationToken);

    private static async Task<IResult> HandleStatusAsync(
        HttpContext httpContext,
        string expenseId,
        IExpenseManagementService service,
        ExpenseGatewayAuthenticator authenticator,
        Func<string, string, CancellationToken, Task<ExpenseRecordResponse?>> command,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var expense = await command(userId!, expenseId, cancellationToken);
            return expense is null ? NotFound(httpContext) : Results.Ok(expense);
        }
        catch (ExpenseMutationConflictException exception)
        {
            return Conflict(httpContext, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static IResult? Authenticate(
        HttpContext httpContext,
        ExpenseGatewayAuthenticator authenticator,
        out string? userId)
    {
        userId = null;
        if (!authenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Expense requests are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = httpContext.Request.Headers[ExpenseGatewayHeaders.UserId].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Expense requests require a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static IResult NotFound(HttpContext httpContext) =>
        Problem(
            httpContext,
            "Expense record was not found.",
            "The Expense record does not exist for the authenticated user.",
            "expense_not_found",
            StatusCodes.Status404NotFound);

    private static IResult Invalid(HttpContext httpContext, ArgumentException exception) =>
        Problem(
            httpContext,
            "Expense request is invalid.",
            exception.Message,
            "invalid_expense_request",
            StatusCodes.Status400BadRequest);

    private static IResult Conflict(HttpContext httpContext, string detail) =>
        Problem(
            httpContext,
            "Expense record cannot be changed.",
            detail,
            "expense_conflict",
            StatusCodes.Status409Conflict);

    private static IResult Problem(
        HttpContext httpContext,
        string title,
        string detail,
        string code,
        int statusCode) =>
        Results.Problem(
            title: title,
            detail: detail,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            });
}
