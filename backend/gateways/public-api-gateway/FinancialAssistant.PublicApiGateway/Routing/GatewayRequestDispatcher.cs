using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Security;

namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRequestDispatcher
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Host"
    };
    private static readonly HashSet<string> ClientControlledGatewayHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "X-Gateway-Route-Key",
        GatewayUserContextHeaders.UserId,
        GatewayUserContextHeaders.SessionId,
        GatewayUserContextHeaders.Roles,
        GatewayUserContextHeaders.LegacyAdminScope
    };

    private readonly HttpClient httpClient;
    private readonly GatewayDestinationCatalog destinationCatalog;
    private readonly ILogger<GatewayRequestDispatcher> logger;

    public GatewayRequestDispatcher(
        HttpClient httpClient,
        GatewayDestinationCatalog destinationCatalog,
        ILogger<GatewayRequestDispatcher> logger)
    {
        this.httpClient = httpClient;
        this.destinationCatalog = destinationCatalog;
        this.logger = logger;
    }

    public async Task DispatchAsync(HttpContext context, GatewayRouteDefinition route)
    {
        if (!string.Equals(route.Status, GatewayRouteStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status501NotImplemented,
                "route_not_active",
                "Route is not active.",
                "The requested API route is not active yet.");
            return;
        }

        if (!destinationCatalog.TryGetDestination(route.InternalDestination, out var destination)
            || !destination.Enabled
            || !destinationCatalog.TryGetBaseAddress(route.InternalDestination, out var baseAddress))
        {
            GatewayOperationalLog.DestinationUnavailable(
                logger,
                route.RouteKey,
                route.InternalDestination);
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "destination_unavailable",
                "Service is temporarily unavailable.",
                "The requested service is temporarily unavailable.");
            return;
        }

        var targetUri = BuildTargetUri(baseAddress, context.Request.Path, context.Request.QueryString);
        using var requestMessage = CreateRequestMessage(context, targetUri, route);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(destination.RequestTimeoutSeconds));

        try
        {
            using var responseMessage = await httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            await CopyResponseAsync(context, responseMessage, timeout.Token);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            GatewayOperationalLog.DispatchCancelled(logger, route.RouteKey);
        }
        catch (OperationCanceledException)
        {
            GatewayOperationalLog.DestinationTimedOut(
                logger,
                route.RouteKey,
                route.InternalDestination,
                destination.RequestTimeoutSeconds);
            await WriteProblemAsync(
                context,
                StatusCodes.Status504GatewayTimeout,
                "destination_timeout",
                "Service response timed out.",
                "The requested service did not respond in time.");
        }
        catch (HttpRequestException exception)
        {
            GatewayOperationalLog.DestinationCallFailed(
                logger,
                route.RouteKey,
                route.InternalDestination,
                exception.GetType().Name);
            await WriteProblemAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "destination_unavailable",
                "Service is temporarily unavailable.",
                "The requested service is temporarily unavailable.");
        }
    }

    private static HttpRequestMessage CreateRequestMessage(
        HttpContext context,
        Uri targetUri,
        GatewayRouteDefinition route)
    {
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) || ClientControlledGatewayHeaders.Contains(header.Key))
            {
                continue;
            }

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content ??= new StreamContent(context.Request.Body);
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (requestMessage.Content is null && RequestMayHaveBody(context.Request))
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        AddGatewayHeaders(context, requestMessage, route);
        return requestMessage;
    }

    private static void AddGatewayHeaders(
        HttpContext context,
        HttpRequestMessage requestMessage,
        GatewayRouteDefinition route)
    {
        requestMessage.Headers.Remove("X-Gateway-Route-Key");
        requestMessage.Headers.TryAddWithoutValidation("X-Gateway-Route-Key", route.RouteKey);

        requestMessage.Headers.Remove(GatewayUserContextHeaders.UserId);
        requestMessage.Headers.Remove(GatewayUserContextHeaders.SessionId);
        requestMessage.Headers.Remove(GatewayUserContextHeaders.Roles);
        var userContext = GatewayUserContext.Get(context);
        if (userContext is not null)
        {
            requestMessage.Headers.TryAddWithoutValidation(GatewayUserContextHeaders.UserId, userContext.UserId);
            requestMessage.Headers.TryAddWithoutValidation(GatewayUserContextHeaders.SessionId, userContext.SessionId);
            requestMessage.Headers.TryAddWithoutValidation(
                GatewayUserContextHeaders.Roles,
                string.Join(',', userContext.Roles));
        }

        var correlationId = CorrelationHeaders.GetCorrelationId(context);
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        requestMessage.Headers.Remove(CorrelationHeaders.CorrelationId);
        requestMessage.Headers.Remove(CorrelationHeaders.XCorrelationId);
        requestMessage.Headers.TryAddWithoutValidation(CorrelationHeaders.CorrelationId, correlationId);
        requestMessage.Headers.TryAddWithoutValidation(CorrelationHeaders.XCorrelationId, correlationId);
    }

    private static bool RequestMayHaveBody(HttpRequest request)
    {
        if (request.ContentLength > 0)
        {
            return true;
        }

        return request.Headers.TryGetValue("Transfer-Encoding", out var transferEncoding)
            && transferEncoding.Any(value =>
                string.Equals(value, "chunked", StringComparison.OrdinalIgnoreCase));
    }

    private static Uri BuildTargetUri(Uri baseAddress, PathString path, QueryString query)
    {
        var baseUri = baseAddress.AbsoluteUri.TrimEnd('/');
        var pathValue = path.Value ?? string.Empty;
        if (!pathValue.StartsWith("/", StringComparison.Ordinal))
        {
            pathValue = "/" + pathValue;
        }

        return new Uri($"{baseUri}{pathValue}{query.Value}", UriKind.Absolute);
    }

    private static async Task CopyResponseAsync(
        HttpContext context,
        HttpResponseMessage responseMessage,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            if (!HopByHopHeaders.Contains(header.Key))
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
        }

        await responseMessage.Content.CopyToAsync(context.Response.Body, cancellationToken);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int status,
        string code,
        string title,
        string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemJson;
        context.Response.Headers.CacheControl = "no-store";
        var response = new
        {
            type = $"https://errors.financial-assistant.app/gateway/{code.Replace('_', '-')}",
            title,
            status,
            code,
            detail,
            correlationId = CorrelationHeaders.GetCorrelationId(context)
        };
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            JsonOptions,
            context.RequestAborted);
    }
}
