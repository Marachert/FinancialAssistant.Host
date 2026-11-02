using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using FinancialAssistant.Shared.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class NativeHttpsTransportTests
{
    [Fact]
    public async Task ActualTlsEnforcesHostForwardingBodyAndPrivateEndpointBoundaries()
    {
        using var certs = new NativeHttpsTestCertificates();
        var port = Port();
        await using var app = Build(certs, port);
        await app.StartAsync();
        using var client = Client(certs.Root, port);
        using var ok = await client.GetAsync("/visible");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("synthetic-ok", await ok.Content.ReadAsStringAsync());
        Assert.True(ok.Headers.Contains("Strict-Transport-Security"));
        foreach (var name in new[] { "Forwarded", "X-Forwarded-For", "X-Forwarded-Proto", "X-Original-Host", "X-ARR-ClientCert" })
        {
            using var forged = new HttpRequestMessage(HttpMethod.Get, "/visible");
            forged.Headers.TryAddWithoutValidation(name, "synthetic-forgery");
            using var rejected = await client.SendAsync(forged);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        // HttpClient uses Host as SNI too; raw TLS keeps correct SNI while testing HTTP Host independently.
        Assert.StartsWith("HTTP/1.1 400", await RequestWithWrongHost(certs.Root, port));
        foreach (var path in new[] { "/", "/health", "/health/ready", "/gateway/info", "/gateway/status", "/metrics" })
        { using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        using var oversized = await client.PostAsync("/visible", new ByteArrayContent(new byte[65537]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.True(oversized.Headers.ConnectionClose);
        using var acceptedBody = await client.PostAsync("/visible", new ByteArrayContent(new byte[65536]));
        Assert.Equal(HttpStatusCode.OK, acceptedBody.StatusCode);
        using var chunked = await client.PostAsync("/visible", new UnknownLengthContent());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, chunked.StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task ActualTlsRejectsUntrustedRootAndUnknownServerName()
    {
        using var certs = new NativeHttpsTestCertificates();
        var port = Port();
        await using var app = Build(certs, port);
        await app.StartAsync();
        using var untrusted = Client(null, port);
        await Assert.ThrowsAsync<HttpRequestException>(() => untrusted.GetAsync("/visible"));
        using var wrongName = Client(certs.Root, port, "foreign.example.invalid");
        await Assert.ThrowsAsync<HttpRequestException>(() => wrongName.GetAsync("/visible"));
        await app.StopAsync();
    }

    [Fact]
    public async Task ActualTlsRenewalUsesAValidatedNewCertificateAfterOwnedHostRestart()
    {
        using var first = new NativeHttpsTestCertificates();
        using var next = new NativeHttpsTestCertificates(issuer: first.Root);
        var port = Port();
        await using (var app = Build(first, port))
        {
            await app.StartAsync();
            using var client = Client(first.Root, port);
            using var response = await client.GetAsync("/visible");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await app.StopAsync();
        }
        await using (var app = Build(next, port))
        {
            await app.StartAsync();
            using var client = Client(first.Root, port);
            using var response = await client.GetAsync("/visible");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(first.Plan().CertificateSha256, next.Plan().CertificateSha256);
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ActualTlsProxiedChunkedUploadReturns413WithoutCommittingDownstream()
    {
        var downstreamPort = Port();
        var downstreamBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        downstreamBuilder.Logging.ClearProviders();
        downstreamBuilder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, downstreamPort));
        await using var downstream = downstreamBuilder.Build();
        var completedUploads = 0;
        downstream.MapPost("/proxied", async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);
            Interlocked.Increment(ref completedUploads);
            return "synthetic-complete";
        });
        await downstream.StartAsync();
        using var certs = new NativeHttpsTestCertificates();
        var port = Port();
        await using var app = Build(certs, port, $"http://127.0.0.1:{downstreamPort}");
        await app.StartAsync();
        using var client = Client(certs.Root, port);
        using var oversized = await client.PostAsync("/proxied", new UnknownLengthContent());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.True(oversized.Headers.ConnectionClose);
        var problem = await oversized.Content.ReadAsStringAsync();
        Assert.Contains("request_body_too_large", problem);
        Assert.DoesNotContain("127.0.0.1", problem);
        Assert.Equal(0, Volatile.Read(ref completedUploads));
        using var accepted = await client.PostAsync("/proxied", new ByteArrayContent(new byte[65536]));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("synthetic-complete", await accepted.Content.ReadAsStringAsync());
        Assert.Equal(1, Volatile.Read(ref completedUploads));
        await app.StopAsync();
        await downstream.StopAsync();
    }

    private static WebApplication Build(NativeHttpsTestCertificates certs, int port, string? downstreamAddress = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<INativeHttpsCertificateSource>(new SyntheticHttpsCertificateSource(certs.Leaf));
        NativeHttpsPerimeter.Configure(builder, certs.Plan(port, 65536));
        if (downstreamAddress is not null)
        {
            builder.Services.AddSingleton(new GatewayDestinationCatalog(Options.Create(new GatewayDestinationMapOptions
            {
                Destinations = [new() { DestinationKey = "synthetic-service", BaseAddress = downstreamAddress, Enabled = true }]
            })));
            builder.Services.AddHttpClient<GatewayRequestDispatcher>()
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        }
        var app = builder.Build();
        app.UseNativeHttpsPerimeter();
        app.UseMiddleware<GatewayExceptionMiddleware>();
        app.MapGet("/visible", () => "synthetic-ok");
        app.MapPost("/visible", async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);
            return "synthetic-post";
        });
        app.MapGet("/health/ready", () => "synthetic-private");
        app.MapGet("/gateway/status", () => "synthetic-private");
        if (downstreamAddress is not null)
        {
            app.MapPost("/proxied", (HttpContext context, GatewayRequestDispatcher dispatcher) => dispatcher.DispatchAsync(context,
                new GatewayRouteDefinition
                {
                    RouteKey = "synthetic-proxy",
                    PublicPattern = "/proxied",
                    ServiceOwner = "Synthetic Service",
                    InternalDestination = "synthetic-service",
                    AccessPolicy = "public",
                    Status = GatewayRouteStatuses.Active,
                    Methods = ["POST"]
                }));
        }
        return app;
    }

    private static HttpClient Client(X509Certificate2? root, int port, string host = "localhost")
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(IPAddress.Loopback, port, token); return new NetworkStream(socket, ownsSocket: true); }
                catch { socket.Dispose(); throw; }
            }
        };
        handler.SslOptions = new SslClientAuthenticationOptions
        {
            CertificateChainPolicy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck,
                DisableCertificateDownloads = true
            }
        };
        if (root is not null) { handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(root); }
        return new HttpClient(handler) { BaseAddress = new Uri($"https://{host}:{port}"), Timeout = TimeSpan.FromSeconds(10) };
    }

    private static int Port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static async Task<string?> RequestWithWrongHost(X509Certificate2 root, int port)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var socket = new TcpClient(AddressFamily.InterNetwork);
        await socket.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await using var tls = new SslStream(socket.GetStream());
        var policy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
            DisableCertificateDownloads = true
        };
        policy.CustomTrustStore.Add(root);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        { TargetHost = "localhost", CertificateChainPolicy = policy }, timeout.Token);
        await tls.WriteAsync(Encoding.ASCII.GetBytes($"GET /visible HTTP/1.1\r\nHost: foreign.example.invalid:{port}\r\nConnection: close\r\n\r\n"), timeout.Token);
        using var reader = new StreamReader(tls, leaveOpen: true);
        return await reader.ReadLineAsync(timeout.Token);
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(new byte[65537]).AsTask();
    }
}
