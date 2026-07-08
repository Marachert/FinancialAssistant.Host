using System.Text;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayExceptionMiddlewareTests
{
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

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed record GatewayProblem(string Code, string? CorrelationId);
}
