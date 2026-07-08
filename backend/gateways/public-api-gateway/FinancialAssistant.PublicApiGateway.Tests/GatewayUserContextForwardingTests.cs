using System.Net;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayUserContextForwardingTests
{
    [Fact]
    public async Task Dispatch_OverwritesSpoofedGatewayHeadersWithValidatedContext()
    {
        var handler = new HeaderRecordingHandler();
        var dispatcher = CreateDispatcher(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/users/me";
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationHeaders.ContextItemKey] = "synthetic-correlation-id";
        context.Items[GatewayUserContext.ContextItemKey] = new GatewayUserContext(
            "trusted-user-id",
            "trusted-session-id",
            ["admin", "user"]);
        context.Request.Headers[GatewayUserContextHeaders.UserId] = "spoofed-user-id";
        context.Request.Headers[GatewayUserContextHeaders.SessionId] = "spoofed-session-id";
        context.Request.Headers[GatewayUserContextHeaders.Roles] = "super-admin";
        context.Request.Headers[GatewayUserContextHeaders.LegacyAdminScope] = "admin";
        context.Request.Headers.Authorization = "Bearer synthetic-access-token";

        await dispatcher.DispatchAsync(context, CreateRoute());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal("trusted-user-id", handler.GetHeader(GatewayUserContextHeaders.UserId));
        Assert.Equal("trusted-session-id", handler.GetHeader(GatewayUserContextHeaders.SessionId));
        Assert.Equal("admin,user", handler.GetHeader(GatewayUserContextHeaders.Roles));
        Assert.Null(handler.GetHeader(GatewayUserContextHeaders.LegacyAdminScope));
        Assert.Equal("Bearer synthetic-access-token", handler.GetHeader("Authorization"));
        Assert.Equal("profile-me", handler.GetHeader("X-Gateway-Route-Key"));
    }

    [Fact]
    public async Task Dispatch_WithoutValidatedContext_DropsClientGatewayIdentityHeaders()
    {
        var handler = new HeaderRecordingHandler();
        var dispatcher = CreateDispatcher(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/users/me";
        context.Response.Body = new MemoryStream();
        context.Request.Headers[GatewayUserContextHeaders.UserId] = "spoofed-user-id";
        context.Request.Headers[GatewayUserContextHeaders.SessionId] = "spoofed-session-id";
        context.Request.Headers[GatewayUserContextHeaders.Roles] = "admin";

        await dispatcher.DispatchAsync(context, CreateRoute());

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Null(handler.GetHeader(GatewayUserContextHeaders.UserId));
        Assert.Null(handler.GetHeader(GatewayUserContextHeaders.SessionId));
        Assert.Null(handler.GetHeader(GatewayUserContextHeaders.Roles));
    }

    private static GatewayRequestDispatcher CreateDispatcher(HttpMessageHandler handler)
    {
        var catalog = new GatewayDestinationCatalog(
            Options.Create(new GatewayDestinationMapOptions
            {
                Destinations =
                [
                    new GatewayDestinationDefinition
                    {
                        DestinationKey = "profile-service",
                        BaseAddress = "http://profile.internal",
                        Enabled = true,
                        RequestTimeoutSeconds = 10
                    }
                ]
            }));

        return new GatewayRequestDispatcher(
            new HttpClient(handler),
            catalog,
            NullLogger<GatewayRequestDispatcher>.Instance);
    }

    private static GatewayRouteDefinition CreateRoute() =>
        new()
        {
            RouteKey = "profile-me",
            PublicPattern = "/users/me",
            ServiceOwner = "Profile Service",
            InternalDestination = "profile-service",
            AccessPolicy = GatewayAccessPolicies.Authenticated,
            Status = GatewayRouteStatuses.Active,
            Methods = [HttpMethods.Get]
        };

    private sealed class HeaderRecordingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string[]> headers = new(StringComparer.OrdinalIgnoreCase);

        public string? GetHeader(string name) =>
            headers.TryGetValue(name, out var values) ? values.SingleOrDefault() : null;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            foreach (var header in request.Headers)
            {
                headers[header.Key] = header.Value.ToArray();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
