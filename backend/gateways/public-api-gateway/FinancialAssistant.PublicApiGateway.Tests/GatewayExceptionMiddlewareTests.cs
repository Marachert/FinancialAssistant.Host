using System.Text;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayExceptionMiddlewareTests
{
    [Theory]
    [InlineData(false, "HTTP/1.1")]
    [InlineData(true, "HTTP/1.1")]
    [InlineData(true, "HTTP/2")]
    public async Task BodyLimitError_DirectOrWrapped_RemainsSafe413(bool wrapped, string protocol)
    {
        const string sensitive = "synthetic-private-body-and-token";
        var limit = new BadHttpRequestException(sensitive, StatusCodes.Status413PayloadTooLarge);
        Exception failure = wrapped ? new HttpRequestException(sensitive, new IOException(sensitive, limit)) : limit;
        var middleware = new GatewayExceptionMiddleware(_ => throw failure, NullLogger<GatewayExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Protocol = protocol;
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationHeaders.ContextItemKey] = "synthetic-correlation-id";
        await middleware.InvokeAsync(context);
        var body = await ReadBodyAsync(context);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        if (protocol == "HTTP/1.1") { Assert.Equal("close", context.Response.Headers.Connection); }
        else { Assert.False(context.Response.Headers.ContainsKey("Connection")); }
        Assert.Contains("request_body_too_large", body);
        Assert.Contains("synthetic-correlation-id", body);
        Assert.DoesNotContain(sensitive, body);
    }

    [Fact]
    public async Task UnhandledException_ReturnsSafeProblemWithoutSensitiveValues()
    {
        const string sensitiveMessage = "password=synthetic-secret receipt=raw-text amount=999.99";
        var middleware = new GatewayExceptionMiddleware(
            _ => throw new InvalidOperationException(sensitiveMessage),
            NullLogger<GatewayExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationHeaders.ContextItemKey] = "synthetic-correlation-id";

        await middleware.InvokeAsync(context);
        var body = await ReadBodyAsync(context);
        var problem = JsonSerializer.Deserialize<GatewayProblem>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.NotNull(problem);
        Assert.Equal("internal_error", problem.Code);
        Assert.Equal("synthetic-correlation-id", problem.CorrelationId);
        Assert.DoesNotContain("synthetic-secret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-text", body, StringComparison.Ordinal);
        Assert.DoesNotContain("999.99", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnhandledException_AfterResponseStarted_AbortsWithoutRethrowingSensitiveException(bool bodyLimit)
    {
        const string sensitiveMessage = "provider-token=synthetic-secret raw-note=private-text";
        var responseFeature = new StartedResponseFeature();
        var lifetimeFeature = new RecordingRequestLifetimeFeature();
        var features = new FeatureCollection();
        features.Set<IHttpResponseFeature>(responseFeature);
        features.Set<IHttpRequestLifetimeFeature>(lifetimeFeature);
        var context = new DefaultHttpContext(features);
        var middleware = new GatewayExceptionMiddleware(
            _ => throw (bodyLimit ? new BadHttpRequestException(sensitiveMessage, StatusCodes.Status413PayloadTooLarge) : new InvalidOperationException(sensitiveMessage)),
            NullLogger<GatewayExceptionMiddleware>.Instance);

        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.Null(exception);
        Assert.True(lifetimeFeature.Aborted);
        Assert.Equal(StatusCodes.Status200OK, responseFeature.StatusCode);
        Assert.Empty(responseFeature.Headers);
        Assert.Equal(0, responseFeature.Body.Length);
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed record GatewayProblem(string Code, string? CorrelationId);

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = new MemoryStream();

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }

    private sealed class RecordingRequestLifetimeFeature : IHttpRequestLifetimeFeature
    {
        private readonly CancellationTokenSource cancellation = new();

        public bool Aborted { get; private set; }

        public CancellationToken RequestAborted
        {
            get => cancellation.Token;
            set
            {
            }
        }

        public void Abort()
        {
            Aborted = true;
            cancellation.Cancel();
        }
    }
}
