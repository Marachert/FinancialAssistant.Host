using FinancialAssistant.Category.Api.Security;
using FinancialAssistant.Category.Application.Categories;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Api.Endpoints;

public static class CategoryEndpointExtensions
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
                CategoryApiRoutes.UserRegisteredEvent,
                async (
                    HttpContext httpContext,
                    UserRegisteredCategoryEvent integrationEvent,
                    ICategoryService categoryService,
                    CategoryGatewayAuthenticator gatewayAuthenticator,
                    CancellationToken cancellationToken) =>
                {
                    if (!gatewayAuthenticator.IsAuthenticated(httpContext))
                    {
                        return MissingGatewayAuthentication(httpContext);
                    }

                    try
                    {
                        var categories = await categoryService.SeedDefaultsAsync(
                            integrationEvent,
                            cancellationToken);
                        return Results.Accepted(CategoryApiRoutes.Categories, categories);
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidRequest(exception, "invalid_registration_event", httpContext);
                    }
                })
            .WithName("SeedDefaultCategoriesFromUserRegistered")
            .Produces<CategoryResponse[]>(StatusCodes.Status202Accepted)
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status400BadRequest);

        app.MapGet(
                CategoryApiRoutes.Categories,
                async (
                    HttpContext httpContext,
                    string? query,
                    ICategoryService categoryService,
                    CategoryGatewayAuthenticator gatewayAuthenticator,
                    CancellationToken cancellationToken) =>
                {
                    if (!gatewayAuthenticator.IsAuthenticated(httpContext))
                    {
                        return MissingGatewayAuthentication(httpContext);
                    }

                    var userId = GetGatewayUserId(httpContext);
                    if (userId is null)
                    {
                        return MissingGatewayUser(httpContext);
                    }

                    try
                    {
                        var categories = await categoryService.SearchAsync(
                            userId,
                            query,
                            cancellationToken);
                        return categories is null
                            ? MissingCatalog(httpContext)
                            : Results.Ok(categories);
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidRequest(exception, "invalid_category_query", httpContext);
                    }
                })
            .WithName("SearchCategories")
            .Produces<CategoryResponse[]>()
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPut(
                CategoryApiRoutes.CategoryAliases,
                async (
                    HttpContext httpContext,
                    string categoryId,
                    UpdateCategoryAliasesRequest request,
                    ICategoryService categoryService,
                    CategoryGatewayAuthenticator gatewayAuthenticator,
                    CancellationToken cancellationToken) =>
                {
                    if (!gatewayAuthenticator.IsAuthenticated(httpContext))
                    {
                        return MissingGatewayAuthentication(httpContext);
                    }

                    var userId = GetGatewayUserId(httpContext);
                    if (userId is null)
                    {
                        return MissingGatewayUser(httpContext);
                    }

                    try
                    {
                        var category = await categoryService.ReplaceAliasesAsync(
                            userId,
                            categoryId,
                            request,
                            cancellationToken);
                        return category is null
                            ? Results.Problem(
                                title: "Category was not found.",
                                detail: "The category does not exist in the authenticated user's catalog.",
                                statusCode: StatusCodes.Status404NotFound,
                                extensions: ErrorExtensions("category_not_found", httpContext))
                            : Results.Ok(category);
                    }
                    catch (ArgumentException exception)
                    {
                        return InvalidRequest(exception, "invalid_category_aliases", httpContext);
                    }
                })
            .WithName("ReplaceCategoryAliases")
            .Produces<CategoryResponse>()
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<CategoryApiErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static string? GetGatewayUserId(HttpContext httpContext)
    {
        var value = httpContext.Request.Headers[CategoryGatewayHeaders.UserId].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IResult MissingGatewayAuthentication(HttpContext httpContext) =>
        Results.Problem(
            title: "Trusted gateway authentication is required.",
            detail: "Category requests are accepted only from an authenticated gateway or internal publisher.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: ErrorExtensions("trusted_gateway_authentication_required", httpContext));

    private static IResult MissingGatewayUser(HttpContext httpContext) =>
        Results.Problem(
            title: "Authentication is required.",
            detail: "Category requests must be forwarded with a trusted gateway user context.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: ErrorExtensions("authentication_required", httpContext));

    private static IResult MissingCatalog(HttpContext httpContext) =>
        Results.Problem(
            title: "Category catalog was not found.",
            detail: "Default categories are created after the user registration event is processed.",
            statusCode: StatusCodes.Status404NotFound,
            extensions: ErrorExtensions("category_catalog_not_found", httpContext));

    private static IResult InvalidRequest(
        ArgumentException exception,
        string code,
        HttpContext httpContext) =>
        Results.Problem(
            title: "Category request is invalid.",
            detail: exception.Message,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: ErrorExtensions(code, httpContext));

    private static Dictionary<string, object?> ErrorExtensions(string code, HttpContext httpContext) =>
        new(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["traceId"] = httpContext.TraceIdentifier
        };
}
