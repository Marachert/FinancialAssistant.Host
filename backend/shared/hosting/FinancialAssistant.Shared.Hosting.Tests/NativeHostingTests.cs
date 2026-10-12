using System.Net;
using System.Text.Json;
using FinancialAssistant.Shared.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeHostingTests
{
    private const string SyntheticSecret = "synthetic-native-secret-not-a-real-credential";

    [Fact]
    public void Catalog_MatchesInventoryWithoutQualifyingIt()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRoot(), "infra/windows-native/component-manifest.json")));
        var hosts = document.RootElement.GetProperty("hosts").EnumerateArray().ToArray();
        Assert.Equal(15, NativeServiceCatalog.Services.Count);
        Assert.Equal(hosts.Length, NativeServiceCatalog.Services.Count);
        Assert.False(document.RootElement.GetProperty("releaseReady").GetBoolean());
        foreach (var host in hosts)
        {
            var service = NativeServiceCatalog.Get(host.GetProperty("id").GetString()!);
            Assert.Equal(host.GetProperty("serviceName").GetString(), service.ServiceName);
            Assert.Equal(host.GetProperty("port").GetInt32(), service.Port);
            Assert.Equal(host.GetProperty("dependsOn").EnumerateArray().Select(dependency => dependency.GetString()).Order(),
                service.RequiredComponents.Order());
            Assert.Equal($"http://127.0.0.1:{service.Port}", service.BaseAddress);
            Assert.Equal($"NT SERVICE\\{service.ServiceName}", service.ServiceAccount);
            var project = host.GetProperty("project").GetString()!;
            var projectDocument = System.Xml.Linq.XDocument.Load(Path.Combine(FindRoot(), project));
            Assert.Equal(projectDocument.Descendants("AssemblyName").Single().Value + ".exe", service.ExecutableName);
            var program = File.ReadAllText(Path.Combine(FindRoot(), Path.GetDirectoryName(project)!, "Program.cs"));
            Assert.Contains($"FinancialAssistantHost.CreateBuilder(args, \"{service.Id}\")", program);
        }
    }

    [Fact]
    public void UnknownIdentity_IsRejected() =>
        Assert.Throws<ArgumentException>(() => NativeServiceCatalog.Get("foreign-service"));

    public static IEnumerable<object[]> Hosts => NativeServiceCatalog.Services.Select(service => new object[] { service.Id });

    [Theory]
    [MemberData(nameof(Hosts))]
    public void EachHost_RequiresExplicitStableSecrets(string id)
    {
        var service = NativeServiceCatalog.Get(id);
        foreach (var missing in service.RequiredSecretKeys)
        {
            var values = Secrets(service);
            values.Remove(missing);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                FinancialAssistantHost.ValidateConfiguration(Configuration(values), service));
            Assert.Contains(missing, exception.Message);
            Assert.DoesNotContain(SyntheticSecret, exception.Message);
        }

        FinancialAssistantHost.ValidateConfiguration(Configuration(Secrets(service)), service);
    }

    [Theory]
    [InlineData("income", "Income")]
    [InlineData("expense", "Expense")]
    public void FinancialHosts_RequireStorageSecretsInCanonicalCatalog(string id, string prefix)
    {
        var service = NativeServiceCatalog.Get(id);
        foreach (var suffix in new[] { "Password", "IndexKey" })
        {
            var key = $"{prefix}:Storage:Postgres:{suffix}";
            Assert.Contains(key, service.RequiredSecretKeys);
            var values = Secrets(service);
            values.Remove(key);
            Assert.Contains(key, Assert.Throws<InvalidOperationException>(() =>
                FinancialAssistantHost.ValidateConfiguration(Configuration(values), service)).Message);
            values[key] = SyntheticSecret + "\n";
            var unsafeValue = Assert.Throws<InvalidOperationException>(() =>
                FinancialAssistantHost.ValidateConfiguration(Configuration(values), service));
            Assert.Contains(key, unsafeValue.Message);
            Assert.DoesNotContain(SyntheticSecret, unsafeValue.Message);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(257)]
    public void InvalidSecretLength_FailsWithoutDisclosingValue(int length)
    {
        var configuration = Configuration(new() { ["Synthetic:Secret"] = new string('z', length) });
        var exception = Assert.Throws<InvalidOperationException>(() => FinancialAssistantHost.RequireSecret(configuration, "Synthetic:Secret"));
        Assert.Contains("Synthetic:Secret requires", exception.Message);
        if (length >= 31) { Assert.DoesNotContain(new string('z', length), exception.Message); }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("\u00e9")]
    public void UnsafeHeaderSecret_IsRejected(string suffix) =>
        Assert.Throws<InvalidOperationException>(() => FinancialAssistantHost.RequireSecret(
            Configuration(new() { ["Synthetic:Secret"] = SyntheticSecret + suffix }), "Synthetic:Secret"));

    [Theory]
    [InlineData("urls", "http://0.0.0.0:5102")]
    [InlineData("urls", "http://localhost:5102")]
    [InlineData("urls", "http://127.0.0.1:9999")]
    [InlineData("urls", "http://127.0.0.1:5102;http://0.0.0.0:80")]
    [InlineData("Kestrel:Endpoints:Public:Url", "http://0.0.0.0:5102")]
    public void ExternalOrConflictingEndpoint_IsRejected(string key, string value)
    {
        var service = NativeServiceCatalog.Get("profile");
        var values = Secrets(service);
        values[key] = value;
        Assert.Throws<InvalidOperationException>(() => FinancialAssistantHost.ValidateConfiguration(Configuration(values), service));
    }

    [Theory]
    [InlineData("AiOrchestration:Provider:Enabled")]
    [InlineData("ReceiptProcessing:Ocr:Enabled")]
    [InlineData("Identity:Providers:Phone:Enabled")]
    public void PaidProviderActivation_IsRejected(string key)
    {
        var service = NativeServiceCatalog.Get("profile");
        var values = Secrets(service);
        values[key] = "true";
        Assert.Throws<InvalidOperationException>(() => FinancialAssistantHost.ValidateConfiguration(Configuration(values), service));
    }

    [Fact]
    public async Task ConsoleHosting_PreservesDeveloperLifetimeAndGracefulStop()
    {
        var builder = FinancialAssistantHost.CreateBuilder(["--environment", "Testing"], "profile");
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        Assert.Equal("ConsoleLifetime", app.Services.GetRequiredService<IHostLifetime>().GetType().Name);
        Assert.Null(app.Services.GetService<NativeServiceDefinition>());
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Lifetime.ApplicationStopped.Register(() => stopped.SetResult());
        app.MapGet("/health", () => "synthetic-healthy");
        await app.StartAsync();
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(new Uri(Assert.Single(app.Urls)), "/health"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await app.StopAsync();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NativeConsoleBuild_UsesPublishedContentRootAndBoundedShutdown()
    {
        var arguments = new List<string> { "--environment", "WindowsNative" };
        foreach (var pair in Secrets(NativeServiceCatalog.Get("profile")))
        {
            arguments.Add("--" + pair.Key);
            arguments.Add(pair.Value!);
        }
        var builder = FinancialAssistantHost.CreateBuilder(arguments.ToArray(), "profile");
        await using var app = builder.Build();
        Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),
            app.Environment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal(TimeSpan.FromSeconds(30), app.Services.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout);
        Assert.Equal("ConsoleLifetime", app.Services.GetRequiredService<IHostLifetime>().GetType().Name);
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("ai-orchestration")]
    public async Task NativeBoundary_RejectsForgedOrMultipleSecrets_AndKeepsHealthAvailable(string id)
    {
        var service = NativeServiceCatalog.Get(id);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "WindowsNative" });
        builder.Configuration.AddInMemoryCollection(Secrets(service));
        FinancialAssistantHost.ConfigureNative(builder, service);
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseWindowsNativeRequestBoundary();
        app.MapGet("/users/me", () => "synthetic");
        app.MapGet("/health/ready", () => "healthy");
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var missing = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        client.DefaultRequestHeaders.Add("X-Gateway-User-Id", "forged-user");
        client.DefaultRequestHeaders.Add("X-Gateway-Authentication", "synthetic-wrong-secret-that-is-long-enough");
        using var wrong = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Gateway-Authentication");
        client.DefaultRequestHeaders.Add("X-Gateway-Authentication", new[] { SyntheticSecret, SyntheticSecret });
        using var multiple = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, multiple.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Gateway-Authentication");
        using var health = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        client.DefaultRequestHeaders.Add("X-Gateway-Authentication", SyntheticSecret);
        using var valid = await client.GetAsync("/users/me");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        await app.StopAsync();
    }

    private static Dictionary<string, string?> Secrets(NativeServiceDefinition service) =>
        service.RequiredSecretKeys.ToDictionary(key => key, _ => (string?)SyntheticSecret);

    [Fact]
    public void NativeMonitoring_ReplacesLegacyTargets_WithEveryOtherCanonicalHost()
    {
        var service = NativeServiceCatalog.Get("monitoring");
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddJsonFile(Path.Combine(FindRoot(),
            "backend/services/monitoring/FinancialAssistant.Monitoring.Api/appsettings.json"));
        builder.Configuration.AddInMemoryCollection(Secrets(service));
        FinancialAssistantHost.ConfigureNative(builder, service);
        var targets = builder.Configuration.GetSection("Monitoring:Services").GetChildren().ToArray();
        Assert.Equal(14, targets.Length);
        Assert.DoesNotContain(targets, target => target["Name"] is "financial-summary" or "monitoring");
        foreach (var target in NativeServiceCatalog.Services.Where(target => target.Id != "monitoring"))
        {
            Assert.Single(targets, configured => configured["Name"] == target.Id && configured["BaseAddress"] == target.BaseAddress);
        }

        Assert.Equal("http://127.0.0.1:15672", builder.Configuration["Monitoring:RabbitMq:ManagementBaseAddress"]);
        Assert.Equal("http://127.0.0.1:9200", builder.Configuration["Monitoring:Elasticsearch:BaseAddress"]);
    }

    [Theory]
    [InlineData("14")]
    [InlineData("9999")]
    [InlineData("01")]
    [InlineData("foreign")]
    public void NativeMonitoring_RejectsExtraOrAmbiguousTargetIndices(string index)
    {
        var service = NativeServiceCatalog.Get("monitoring");
        var builder = WebApplication.CreateBuilder();
        var values = Secrets(service);
        values[$"Monitoring:Services:{index}:BaseAddress"] = "https://synthetic.invalid";
        builder.Configuration.AddInMemoryCollection(values);
        Assert.Throws<InvalidOperationException>(() => FinancialAssistantHost.ConfigureNative(builder, service));
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln"))) { return directory.FullName; }
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
