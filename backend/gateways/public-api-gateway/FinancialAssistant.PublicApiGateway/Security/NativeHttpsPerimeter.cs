using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.Shared.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancialAssistant.PublicApiGateway.Security;

public sealed class NativeHttpsRuntime : IDisposable
{
    private readonly X509Certificate2 certificate;
    private readonly TimeProvider clock;
    public NativeHttpsPlan Plan { get; }

    public NativeHttpsRuntime(NativeHttpsPlan plan, INativeHttpsCertificateSource source, TimeProvider clock)
    {
        Plan = plan; this.clock = clock;
        certificate = source.Load(plan);
        try { NativeHttpsCertificatePolicy.Validate(certificate, plan, clock.GetUtcNow(), requireRenewalWindow: true); }
        catch { certificate.Dispose(); throw; }
    }

    public bool IsUsable(bool requireRenewalWindow)
    {
        try { NativeHttpsCertificatePolicy.Validate(certificate, Plan, clock.GetUtcNow(), requireRenewalWindow); return true; }
        catch (Exception exception) when (exception is InvalidOperationException or CryptographicException or ArgumentException)
        { return false; }
    }

    public X509Certificate2? Select(string? serverName) =>
        (string.Equals(serverName, Plan.HostName, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(serverName) && Plan.PublicOrigin.HostNameType == UriHostNameType.IPv4) &&
        IsUsable(requireRenewalWindow: false) ? certificate : null;

    public void Dispose() => certificate.Dispose();
}

public sealed class NativeHttpsReadinessHealthCheck(NativeHttpsRuntime? runtime = null) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(runtime?.IsUsable(requireRenewalWindow: true) == true
            ? HealthCheckResult.Healthy("Native HTTPS certificate is available.")
            : HealthCheckResult.Unhealthy("Native HTTPS is unavailable or certificate renewal is required."));
}

public static class NativeHttpsPerimeter
{
    public static WebApplicationBuilder AddNativeHttpsPerimeter(this WebApplicationBuilder builder)
    {
        var plan = NativeHttpsPlan.Read(builder.Configuration);
        if (plan is null)
        {
            builder.Services.AddHealthChecks().AddCheck<NativeHttpsReadinessHealthCheck>("native_https", tags: ["ready"]);
            return builder;
        }
        return Configure(builder, plan);
    }

    public static WebApplicationBuilder Configure(WebApplicationBuilder builder, NativeHttpsPlan plan)
    {
        builder.Services.AddSingleton(plan);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<INativeHttpsCertificateSource, WindowsNativeHttpsCertificateSource>();
        builder.Services.AddSingleton<NativeHttpsRuntime>();
        builder.Services.AddHealthChecks().AddCheck<NativeHttpsReadinessHealthCheck>("native_https", tags: ["ready"]);
        builder.WebHost.ConfigureKestrel(options =>
        {
            var runtime = options.ApplicationServices.GetRequiredService<NativeHttpsRuntime>();
            options.Limits.MaxRequestBodySize = plan.MaximumRequestBodyBytes;
            options.Limits.MaxRequestHeadersTotalSize = plan.MaximumRequestHeadersBytes;
            options.Limits.MaxRequestLineSize = 8192;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(plan.RequestHeadersTimeoutSeconds);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
            options.Limits.MaxConcurrentConnections = 128;
            options.Limits.MaxConcurrentUpgradedConnections = 0;
            options.Listen(plan.BindAddress, plan.Port, listen =>
            {
                listen.Protocols = HttpProtocols.Http1AndHttp2;
                listen.UseHttps(https =>
                {
                    https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                    https.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                    https.HandshakeTimeout = TimeSpan.FromSeconds(10);
                    https.ServerCertificate = null;
                    https.ServerCertificateSelector = (_, name) => runtime.Select(name);
                });
            });
        });
        return builder;
    }

    public static WebApplication UseNativeHttpsPerimeter(this WebApplication app)
    {
        var runtime = app.Services.GetService<NativeHttpsRuntime>();
        if (runtime is null) { return app; }
        var plan = runtime.Plan;
        var internalPort = NativeServiceCatalog.Get("gateway").Port;
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.Keys.Any(name => name.Equals("Forwarded", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("X-ARR-", StringComparison.OrdinalIgnoreCase)))
            { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }

            if (context.Connection.LocalPort != plan.Port)
            {
                if (context.Connection.LocalPort != internalPort || context.Request.IsHttps ||
                    context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
                await next(context); return;
            }
            if (!context.Request.IsHttps || !string.Equals(context.Request.Host.Host, plan.HostName, StringComparison.OrdinalIgnoreCase) ||
                (context.Request.Host.Port ?? 443) != plan.Port)
            { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            if (!runtime.IsUsable(requireRenewalWindow: false))
            { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; return; }
            if (context.Request.Path == "/" || context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/gateway", StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            if (context.Request.ContentLength > plan.MaximumRequestBodyBytes)
            {
                await GatewayRequestBodyErrors.RejectTooLargeAsync(context);
                return;
            }
            var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) { limit.MaxRequestBodySize = Math.Min(limit.MaxRequestBodySize ?? long.MaxValue, plan.MaximumRequestBodyBytes); }
            context.Response.Headers.StrictTransportSecurity = "max-age=604800";
            await next(context);
        });
        return app;
    }
}
