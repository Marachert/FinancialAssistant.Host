using FinancialAssistant.RecommendationsNotifications.Api.Security;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Contracts;
using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Api.Endpoints;

public static class RecommendationNotificationEndpointExtensions
{
    public static IEndpointRouteBuilder MapRecommendationNotificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        MapRecommendations(
            app,
            RecommendationNotificationApiRoutes.Recommendations,
            "GetRecommendations");
        MapRecommendations(
            app,
            RecommendationNotificationApiRoutes.GatewayRecommendations,
            "GetRecommendationsFromGateway");
        MapRecommendationDismissal(
            app,
            RecommendationNotificationApiRoutes.RecommendationDismissal,
            "DismissRecommendation");
        MapRecommendationDismissal(
            app,
            RecommendationNotificationApiRoutes.GatewayRecommendationDismissal,
            "DismissRecommendationFromGateway");
        MapRecommendationRead(
            app,
            RecommendationNotificationApiRoutes.RecommendationRead,
            "MarkRecommendationRead");
        MapRecommendationRead(
            app,
            RecommendationNotificationApiRoutes.GatewayRecommendationRead,
            "MarkRecommendationReadFromGateway");
        MapNotifications(
            app,
            RecommendationNotificationApiRoutes.Notifications,
            "GetNotifications");
        MapNotifications(
            app,
            RecommendationNotificationApiRoutes.GatewayNotifications,
            "GetNotificationsFromGateway");
        MapDeliveryStatus(
            app,
            RecommendationNotificationApiRoutes.NotificationStatus,
            "UpdateNotificationDeliveryStatus");
        MapDeliveryStatus(
            app,
            RecommendationNotificationApiRoutes.GatewayNotificationStatus,
            "UpdateNotificationDeliveryStatusFromGateway");
        MapNotificationPreferences(
            app,
            RecommendationNotificationApiRoutes.NotificationPreferences,
            "GetNotificationPreferences",
            "UpdateNotificationPreferences");
        MapNotificationPreferences(
            app,
            RecommendationNotificationApiRoutes.GatewayNotificationPreferences,
            "GetNotificationPreferencesFromGateway",
            "UpdateNotificationPreferencesFromGateway");
        return app;
    }

    private static void MapRecommendations(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapGet(route, GetRecommendationsAsync)
            .WithName(name)
            .Produces<RecommendationListResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized);
    }

    private static void MapRecommendationDismissal(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapPut(route, DismissRecommendationAsync)
            .WithName(name)
            .Produces<RecommendationResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status404NotFound)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status409Conflict);
    }

    private static void MapRecommendationRead(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapPut(route, MarkRecommendationReadAsync)
            .WithName(name)
            .Produces<RecommendationResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status404NotFound)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status409Conflict);
    }

    private static void MapNotifications(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapGet(route, GetNotificationsAsync)
            .WithName(name)
            .Produces<NotificationListResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized);
    }

    private static void MapDeliveryStatus(
        IEndpointRouteBuilder app,
        string route,
        string name)
    {
        app.MapPut(route, UpdateDeliveryStatusAsync)
            .WithName(name)
            .Produces<NotificationResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status404NotFound)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status409Conflict);
    }

    private static void MapNotificationPreferences(
        IEndpointRouteBuilder app,
        string route,
        string getName,
        string updateName)
    {
        app.MapGet(route, GetNotificationPreferencesAsync)
            .WithName(getName)
            .Produces<NotificationPreferencesResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized);
        app.MapPut(route, UpdateNotificationPreferencesAsync)
            .WithName(updateName)
            .Produces<NotificationPreferencesResponse>()
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status400BadRequest)
            .Produces<RecommendationNotificationApiErrorResponse>(
                StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetRecommendationsAsync(
        HttpContext context,
        RecommendationService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var currency = ReadCurrency(context);
            var values = await service.GetAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                currency,
                cancellationToken);
            return Results.Ok(new RecommendationListResponse(
                currency.ToUpperInvariant(),
                values.Select(MapRecommendation).ToArray()));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
    }

    private static async Task<IResult> DismissRecommendationAsync(
        string recommendationId,
        DismissRecommendationRequest request,
        HttpContext context,
        RecommendationService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var updated = await service.DismissAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                recommendationId,
                request.ChangedAtUtc,
                cancellationToken);
            return updated is null
                ? Problem(
                    context,
                    "Recommendation was not found.",
                    "The recommendation does not exist in the authenticated user scope.",
                    "recommendation_not_found",
                    StatusCodes.Status404NotFound)
                : Results.Ok(MapRecommendation(updated));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                context,
                "Recommendation dismissal conflicts with current state.",
                exception.Message,
                "recommendation_status_conflict",
                StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> MarkRecommendationReadAsync(
        string recommendationId,
        MarkRecommendationReadRequest request,
        HttpContext context,
        RecommendationService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var updated = await service.MarkReadAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                recommendationId,
                request.ChangedAtUtc,
                cancellationToken);
            return updated is null
                ? Problem(
                    context,
                    "Recommendation was not found.",
                    "The recommendation does not exist in the authenticated user scope.",
                    "recommendation_not_found",
                    StatusCodes.Status404NotFound)
                : Results.Ok(MapRecommendation(updated));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                context,
                "Recommendation read update conflicts with current state.",
                exception.Message,
                "recommendation_status_conflict",
                StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetNotificationsAsync(
        HttpContext context,
        NotificationPreparationService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var currency = ReadCurrency(context);
            var values = await service.GetAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                currency,
                cancellationToken);
            return Results.Ok(new NotificationListResponse(
                currency.ToUpperInvariant(),
                values.Select(MapNotification).ToArray()));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
    }

    private static async Task<IResult> UpdateDeliveryStatusAsync(
        string notificationId,
        UpdateNotificationDeliveryStatusRequest request,
        HttpContext context,
        NotificationPreparationService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var updated = await service.UpdateStatusAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                notificationId,
                request.DeliveryStatus,
                request.ChangedAtUtc,
                cancellationToken);
            return updated is null
                ? Problem(
                    context,
                    "Notification was not found.",
                    "The notification does not exist in the authenticated user scope.",
                    "notification_not_found",
                    StatusCodes.Status404NotFound)
                : Results.Ok(MapNotification(updated));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                context,
                "Notification status update conflicts with current state.",
                exception.Message,
                "notification_status_conflict",
                StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetNotificationPreferencesAsync(
        HttpContext context,
        NotificationPreferenceService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        var preferences = await service.GetAsync(
            RecommendationNotificationOwnerHasher.Hash(userId!),
            cancellationToken);
        return Results.Ok(MapNotificationPreferences(preferences));
    }

    private static async Task<IResult> UpdateNotificationPreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        HttpContext context,
        NotificationPreferenceService service,
        RecommendationNotificationGatewayAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var error = Authenticate(context, authenticator, out var userId);
        if (error is not null)
        {
            return error;
        }

        try
        {
            var quietHours = request.QuietHours is null
                ? null
                : new NotificationQuietHours(
                    request.QuietHours.StartsAt,
                    request.QuietHours.EndsAt,
                    request.QuietHours.TimeZoneId);
            var preferences = await service.UpdateAsync(
                RecommendationNotificationOwnerHasher.Hash(userId!),
                request.PushEnabled,
                request.WebEnabled,
                request.EnabledNotificationTypes,
                quietHours,
                cancellationToken);
            return Results.Ok(MapNotificationPreferences(preferences));
        }
        catch (ArgumentException exception)
        {
            return Invalid(context, exception.Message);
        }
    }

    private static NotificationPreferencesResponse MapNotificationPreferences(
        NotificationPreferences preferences) =>
        new(
            preferences.PushEnabled,
            preferences.WebEnabled,
            NotificationTriggerCodes.All
                .Where(type =>
                    preferences.DisabledNotificationTypes?.Contains(
                        type,
                        StringComparer.Ordinal) != true)
                .ToArray(),
            preferences.QuietHours is null
                ? null
                : new NotificationQuietHoursContract(
                    preferences.QuietHours.StartsAt,
                    preferences.QuietHours.EndsAt,
                    preferences.QuietHours.TimeZoneId));

    private static RecommendationResponse MapRecommendation(
        FinancialRecommendation recommendation) =>
        new(
            recommendation.RecommendationId,
            recommendation.Currency,
            recommendation.Code,
            recommendation.Severity,
            recommendation.Title,
            recommendation.Body,
            recommendation.Facts
                .Select(item => new RecommendationFactResponse(item.Code, item.Value))
                .ToArray(),
            recommendation.GeneratedAtUtc,
            recommendation.Status,
            recommendation.StatusChangedAtUtc);

    private static NotificationResponse MapNotification(
        PreparedNotification notification) =>
        new(
            notification.NotificationId,
            notification.RecommendationId,
            notification.Currency,
            notification.Channel,
            notification.TemplateCode,
            notification.Title,
            notification.Body,
            notification.DeliveryStatus,
            notification.PreparedAtUtc,
            notification.StatusChangedAtUtc);

    private static string ReadCurrency(HttpContext context)
    {
        var value = context.Request.Query["currency"].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Query parameter 'currency' is required.")
            : value;
    }

    private static IResult? Authenticate(
        HttpContext context,
        RecommendationNotificationGatewayAuthenticator authenticator,
        out string? userId)
    {
        userId = null;
        if (!authenticator.IsAuthenticated(context))
        {
            return Problem(
                context,
                "Trusted gateway authentication is required.",
                "Recommendation and notification requests are accepted only from the authenticated gateway.",
                "trusted_gateway_authentication_required",
                StatusCodes.Status401Unauthorized);
        }

        userId = context.Request.Headers[RecommendationNotificationGatewayHeaders.UserId]
            .FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(userId)
            ? Problem(
                context,
                "Authentication is required.",
                "A trusted gateway user context is required.",
                "authentication_required",
                StatusCodes.Status401Unauthorized)
            : null;
    }

    private static IResult Invalid(HttpContext context, string detail) =>
        Problem(
            context,
            "Recommendation or notification request is invalid.",
            detail,
            "invalid_recommendation_notification_request",
            StatusCodes.Status400BadRequest);

    private static IResult Problem(
        HttpContext context,
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
                ["traceId"] = context.TraceIdentifier
            });
}
