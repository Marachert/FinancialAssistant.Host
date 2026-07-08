using System.Net;
using System.Text;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayRequestDispatcherTests
{
    [Fact]
    public async Task ActiveRoute_ForwardsMethodPathQueryBodyAndGatewayHeaders()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"accepted\":true}", Encoding.UTF8, "application/json")
            });
        var dispatcher = CreateDispatcher(
            handler,
            new GatewayDestinationDefinition
            {
                DestinationKey = "auth-service",
                BaseAddress = "http://identity.internal/base",
                Enabled = true,
                RequestTimeoutSeconds = 10
            });
        var context = CreateContext(
            HttpMethods.Post,
            "/auth/v1/sign-in",
            "?source=mobile",
            "{\"email\":\"synthetic@example.invalid\"}");
        var route = CreateRoute(GatewayRouteStatuses.Active, "auth-service");

        await dispatcher.DispatchAsync(context, route);

        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "http://identity.internal/base/auth/v1/sign-in?source=mobile",
            handler.RequestUri?.ToString());
        Assert.Equal("{\"email\":\"synthetic@example.invalid\"}", handler.Body);
        Assert.Equal("auth", handler.GetHeader("X-Gateway-Route-Key"));
        Assert.Equal("test-correlation-id", handler.GetHeader(CorrelationHeaders.CorrelationId));
        Assert.Equal("test-correlation-id", handler.GetHeader(CorrelationHeaders.XCorrelationId));
        Assert.Equal("{\"accepted\":true}", await ReadResponseBodyAsync(context));
    }

    [Fact]
    public async Task ActiveRoute_WhenDestinationIsMissing_ReturnsSafe503()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Handler must not be called."));
        var dispatcher = CreateDispatcher(
            handler,
            new GatewayDestinationDefinition
            {
                DestinationKey = "other-service",
                BaseAddress = "http://other.internal",
                Enabled = false
            });
        var context = CreateContext(HttpMethods.Get, "/categories");
        var route = CreateRoute(GatewayRouteStatuses.Active, "category-service");

        await dispatcher.DispatchAsync(context, route);
        var body = await ReadResponseBodyAsync(context);
        var problem = JsonSerializer.Deserialize<GatewayProblem>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("destination_unavailable", problem.Code);
        Assert.Equal("test-correlation-id", problem.CorrelationId);
        Assert.DoesNotContain("category-service", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("other.internal", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Category Service", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PlaceholderRoute_ReturnsSafe501WithoutInternalMetadata()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Handler must not be called."));
        var dispatcher = CreateDispatcher(
            handler,
            new GatewayDestinationDefinition
            {
                DestinationKey = "auth-service",
                BaseAddress = "http://identity.internal",
                Enabled = true
            });
        var context = CreateContext(HttpMethods.Get, "/auth");
        var route = CreateRoute(GatewayRouteStatuses.Placeholder, "auth-service");

        await dispatcher.DispatchAsync(context, route);
        var body = await ReadResponseBodyAsync(context);
        var problem = JsonSerializer.Deserialize<GatewayProblem>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(StatusCodes.Status501NotImplemented, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("route_not_active", problem.Code);
        Assert.DoesNotContain("auth-service", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity.internal", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ActiveRoute_WhenDestinationCallFails_ReturnsSafe503WithoutRequestData()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("synthetic transport failure"));
        var dispatcher = CreateDispatcher(
            handler,
            new GatewayDestinationDefinition
            {
                DestinationKey = "auth-service",
                BaseAddress = "http://identity.internal",
                Enabled = true
            });
        var sensitiveBody = "{\"password\":\"Synthetic-Password-123!\"}";
        var context = CreateContext(HttpMethods.Post, "/auth/v1/sign-in", body: sensitiveBody);
        var route = CreateRoute(GatewayRouteStatuses.Active, "auth-service");

        await dispatcher.DispatchAsync(context, route);
        var responseBody = await ReadResponseBodyAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.DoesNotContain("identity.internal", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic-Password-123!", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-service", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    private static GatewayRequestDispatcher CreateDispatcher(
        HttpMessageHandler handler,
        params GatewayDestinationDefinition[] destinations)
    {
        var catalog = new GatewayDestinationCatalog(
            Options.Create(new GatewayDestinationMapOptions
            {
                Destinations = destinations
            }));
        return new GatewayRequestDispatcher(
            new HttpClient(handler),
            catalog,
            NullLogger<GatewayRequestDispatcher>.Instance);
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        string? query = null,
        string? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query ?? string.Empty);
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationHeaders.ContextItemKey] = "test-correlation-id";

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Request.ContentType = "application/json";
        }

        return context;
    }

    private static GatewayRouteDefinition CreateRoute(string status, string destinationKey) =>
        new()
        {
            RouteKey = "auth",
            PublicPattern = "/auth",
            CatchAllPattern = "/auth/{**gatewayPath}",
            ServiceOwner = "Auth Service",
            InternalDestination = destinationKey,
            AccessPolicy = "public",
            Status = status,
            Methods = ["GET", "POST"]
        };

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed record GatewayProblem(string Code, string? CorrelationId);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responseFactory;
        private readonly Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        public string? GetHeader(string name) =>
            headers.TryGetValue(name, out var values) ? values.SingleOrDefault() : null;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            foreach (var header in request.Headers)
            {
                headers[header.Key] = header.Value.ToArray();
            }

            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = header.Value.ToArray();
                }

                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return responseFactory(request);
        }
    }
}
