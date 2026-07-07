namespace FinancialAssistant.PublicApiGateway.Routing;

public sealed class GatewayRequestDispatcher
{
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
        if (!string.Equals(route.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status501NotImplemented;
            await context.Response.WriteAsJsonAsync(new
            {
                status = route.Status,
                routeKey = route.RouteKey,
                publicPath = context.Request.Path.Value,
                serviceOwner = route.ServiceOwner,
                internalDestination = route.InternalDestination,
                accessPolicy = route.AccessPolicy,
                message = "Route configured. Service integration is not active yet."
            });
            return;
        }

        if (!destinationCatalog.TryGetDestination(route.InternalDestination, out var destination) || !destination.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                status = "destination_unavailable",
                routeKey = route.RouteKey,
                internalDestination = route.InternalDestination,
                message = "Destination is not configured or not enabled."
            });
            return;
        }

        if (!Uri.TryCreate(destination.BaseAddress, UriKind.Absolute, out var baseAddress))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                status = "destination_invalid",
                routeKey = route.RouteKey,
                internalDestination = route.InternalDestination,
                message = "Destination base address is invalid."
            });
            return;
        }

        var targetUri = BuildTargetUri(baseAddress, context.Request.Path, context.Request.QueryString);

        using var requestMessage = CreateRequestMessage(context, targetUri, route);

        try
        {
            using var responseMessage = await httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            await CopyResponseAsync(context, responseMessage);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Gateway request was cancelled for route {RouteKey}.", route.RouteKey);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Gateway destination call failed for route {RouteKey}.", route.RouteKey);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                status = "destination_call_failed",
                routeKey = route.RouteKey,
                internalDestination = route.InternalDestination,
                message = "Destination call failed."
            });
        }
    }

    private static HttpRequestMessage CreateRequestMessage(HttpContext context, Uri targetUri, GatewayRouteDefinition route)
    {
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);
        requestMessage.Headers.TryAddWithoutValidation("X-Gateway-Route-Key", route.RouteKey);

        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key))
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

        return requestMessage;
    }

    private static bool RequestMayHaveBody(HttpRequest request)
    {
        return request.ContentLength > 0 || string.Equals(request.Headers.TransferEncoding, "chunked", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri BuildTargetUri(Uri baseAddress, PathString path, QueryString query)
    {
        var baseUri = baseAddress.ToString().TrimEnd('/');
        var pathValue = path.Value ?? string.Empty;
        var queryValue = query.Value ?? string.Empty;
        return new Uri($"{baseUri}{pathValue}{queryValue}");
    }

    private static async Task CopyResponseAsync(HttpContext context, HttpResponseMessage responseMessage)
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

        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
}
