using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FinancialAssistant.Shared.Observability.Tests;

public sealed class CorrelationBaselineTests
{
    [Fact]
    public async Task Middleware_preserves_safe_correlation_and_adds_canonical_scope()
    {
        var logger = new CapturingLogger<FinancialAssistantCorrelationMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers[ObservabilityHeaders.CorrelationId] = "synthetic-correlation-001";

        var middleware = new FinancialAssistantCorrelationMiddleware(
            async current => await current.Response.StartAsync(),
            logger,
            new ObservabilityRuntimeIdentity("synthetic-service", "Testing"));

        await middleware.InvokeAsync(context);

        Assert.Equal("synthetic-correlation-001", context.TraceIdentifier);
        Assert.Equal(
            "synthetic-correlation-001",
            context.Response.Headers[ObservabilityHeaders.CorrelationId]);
        Assert.Equal(
            "synthetic-correlation-001",
            context.Response.Headers[ObservabilityHeaders.CompatibilityCorrelationId]);
        Assert.Equal(32, context.Response.Headers[ObservabilityHeaders.TraceId].ToString().Length);
        Assert.Equal("synthetic-service", logger.Scope["ServiceName"]);
        Assert.Equal("Testing", logger.Scope["Environment"]);
        Assert.Equal("POST", logger.Scope["RequestMethod"]);
    }

    [Fact]
    public async Task Middleware_replaces_unsafe_correlation_before_application_code_runs()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ObservabilityHeaders.CorrelationId] = new string('x', 129);
        string? observedRequestHeader = null;

        var middleware = new FinancialAssistantCorrelationMiddleware(
            async current =>
            {
                observedRequestHeader =
                    current.Request.Headers[ObservabilityHeaders.CorrelationId];
                await current.Response.StartAsync();
            },
            new CapturingLogger<FinancialAssistantCorrelationMiddleware>(),
            new ObservabilityRuntimeIdentity("synthetic-service", "Testing"));

        await middleware.InvokeAsync(context);

        Assert.NotNull(observedRequestHeader);
        Assert.Equal(32, observedRequestHeader!.Length);
        Assert.DoesNotContain(new string('x', 129), context.Response.Headers.ToString());
    }

    [Fact]
    public async Task Handler_propagates_correlation_and_trace_without_business_payload()
    {
        var context = new DefaultHttpContext();
        context.Items[ObservabilityHeaders.ContextItemKey] = "synthetic-correlation-002";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var capture = new CapturingHttpHandler();
        var handler = new CorrelationPropagationHandler(accessor)
        {
            InnerHandler = capture
        };
        using var client = new HttpClient(handler);
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();

        await client.GetAsync("https://synthetic.invalid/health");

        Assert.Equal(
            "synthetic-correlation-002",
            capture.Request!.Headers.GetValues(ObservabilityHeaders.CorrelationId).Single());
        Assert.Equal(
            activity.TraceId.ToString(),
            capture.Request.Headers.GetValues(ObservabilityHeaders.TraceId).Single());
    }

    [Fact]
    public void Safe_error_fields_expose_type_only()
    {
        var fields = SafeErrorFields.From(
            new InvalidOperationException("synthetic secret must not be logged"));

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["FailureType"] = nameof(InvalidOperationException)
            },
            fields);
        Assert.DoesNotContain("synthetic secret", string.Join(' ', fields.Values));
    }

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public IReadOnlyDictionary<string, object?> Scope { get; private set; } =
            new Dictionary<string, object?>();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            Scope = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(state);
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
