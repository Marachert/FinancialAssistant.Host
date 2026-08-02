using FinancialAssistant.Income.Api.Security;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Contracts;

namespace FinancialAssistant.Income.Api.Endpoints;

public static class IncomeEndpointExtensions
{
    public static IEndpointRouteBuilder MapIncomeEndpoints(this IEndpointRouteBuilder app)
    {
        MapCollection(app, IncomeApiRoutes.Incomes, "Income");
        MapCollection(app, IncomeApiRoutes.GatewayIncomes, "IncomeFromGateway");
        MapRecord(app, IncomeApiRoutes.Income, "Income");
        MapRecord(app, IncomeApiRoutes.GatewayIncome, "IncomeFromGateway");
        MapStatusCommand(app, IncomeApiRoutes.Archive, "ArchiveIncome", HandleArchiveAsync);
        MapStatusCommand(
            app,
            IncomeApiRoutes.GatewayArchive,
            "ArchiveIncomeFromGateway",
            HandleArchiveAsync);
        MapStatusCommand(app, IncomeApiRoutes.Restore, "RestoreIncome", HandleRestoreAsync);
        MapStatusCommand(
            app,
            IncomeApiRoutes.GatewayRestore,
            "RestoreIncomeFromGateway",
            HandleRestoreAsync);
        return app;
    }

    private static void MapCollection(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapPost(pattern, HandleCreateAsync)
            .WithName($"Create{name}")
            .Produces<IncomeRecordResponse>(StatusCodes.Status201Created)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapGet(pattern, HandleListAsync)
            .WithName($"List{name}")
            .Produces<IncomeListResponse>()
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static void MapRecord(IEndpointRouteBuilder app, string pattern, string name)
    {
        app.MapGet(pattern, HandleGetAsync)
            .WithName($"Get{name}")
            .Produces<IncomeRecordResponse>()
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPut(pattern, HandleUpdateAsync)
            .WithName($"Update{name}")
            .Produces<IncomeRecordResponse>()
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static void MapStatusCommand(
        IEndpointRouteBuilder app,
        string pattern,
        string name,
        Delegate handler)
    {
        app.MapPost(pattern, handler)
            .WithName(name)
            .Produces<IncomeRecordResponse>()
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<IncomeApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleCreateAsync(
        HttpContext httpContext,
        CreateIncomeRequest request,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var income = await service.CreateAsync(userId!, request, cancellationToken);
            return Results.Created($"/api/v1/incomes/{income.Id}", income);
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
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var incomes = await service.ListAsync(
                userId!,
                from,
                to,
                includeArchived ?? false,
                cancellationToken);
            return Results.Ok(incomes);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext httpContext,
        string incomeId,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var income = await service.GetAsync(userId!, incomeId, cancellationToken);
            return income is null ? NotFound(httpContext) : Results.Ok(income);
        }
        catch (ArgumentException exception)
        {
            return Invalid(httpContext, exception);
        }
    }

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext httpContext,
        string incomeId,
        UpdateIncomeRequest request,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var income = await service.UpdateAsync(
                userId!,
                incomeId,
                request,
                cancellationToken);
            return income is null ? NotFound(httpContext) : Results.Ok(income);
        }
        catch (IncomeRecordNotEditableException exception)
        {
            return Conflict(httpContext, exception.Message);
        }
        catch (IncomeMutationConflictException exception)
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
        string incomeId,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            httpContext,
            incomeId,
            service,
            authenticator,
            service.ArchiveAsync,
            cancellationToken);

    private static Task<IResult> HandleRestoreAsync(
        HttpContext httpContext,
        string incomeId,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        CancellationToken cancellationToken) =>
        HandleStatusAsync(
            httpContext,
            incomeId,
            service,
            authenticator,
            service.RestoreAsync,
            cancellationToken);

    private static async Task<IResult> HandleStatusAsync(
        HttpContext httpContext,
        string incomeId,
        IIncomeManagementService service,
        IncomeGatewayAuthenticator authenticator,
        Func<string, string, CancellationToken, Task<IncomeRecordResponse?>> command,
        CancellationToken cancellationToken)
    {
        var authenticationError = Authenticate(httpContext, authenticator, out var userId);
        if (authenticationError is not null)
        {
            return authenticationError;
        }

        try
        {
            var income = await command(userId!, incomeId, cancellationToken);
            return income is null ? NotFound(httpContext) : Results.Ok(income);
        }
        catch (IncomeMutationConflictException exception)
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
        IncomeGatewayAuthenticator authenticator,
        out string? userId)
    {
        userId = null;
        if (!authenticator.IsAuthenticated(httpContext))
        {
            return Problem(
                httpContext,
                "Trusted gateway authentication is required.",
                "Income requests are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = httpContext.Request.Headers[IncomeGatewayHeaders.UserId].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Problem(
                httpContext,
                "Authentication is required.",
                "Income requests require a trusted gateway user context.",
                "authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        return null;
    }

    private static IResult NotFound(HttpContext httpContext) =>
        Problem(
            httpContext,
            "Income record was not found.",
            "The Income record does not exist for the authenticated user.",
            "income_not_found",
            StatusCodes.Status404NotFound);

    private static IResult Invalid(HttpContext httpContext, ArgumentException exception) =>
        Problem(
            httpContext,
            "Income request is invalid.",
            exception.Message,
            "invalid_income_request",
            StatusCodes.Status400BadRequest);

    private static IResult Conflict(HttpContext httpContext, string detail) =>
        Problem(
            httpContext,
            "Income record cannot be changed.",
            detail,
            "income_conflict",
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
