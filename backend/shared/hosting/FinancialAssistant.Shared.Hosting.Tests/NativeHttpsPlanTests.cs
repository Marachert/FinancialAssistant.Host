using System.Text.Json;
using FinancialAssistant.Shared.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeHttpsPlanTests
{
    [Fact]
    public void DefaultDoesNotOpenPublicPortOrPretendHttpsIsConfigured() =>
        Assert.Null(NativeHttpsPlan.Read(new ConfigurationBuilder().Build()));

    [Fact]
    public void PlanIsNonExecutingAndRequiresExplicitExposureApproval()
    {
        var options = Options(); options.BindAddress = "0.0.0.0";
        Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Create(options));
        options.PublicExposureApproved = true;
        var plan = NativeHttpsPlan.Create(options);
        Assert.Equal(7443, plan.Port);
        Assert.Equal("poc.example.invalid", plan.HostName);
        Assert.Equal("LocalSubnet", plan.Firewall.RemoteAddresses);
        Assert.Equal(new[] { "Domain", "Private" }, plan.Firewall.Profiles);
        options.MaximumRequestBodyBytes = 65536;
        Assert.Equal(12 * 1024 * 1024, plan.MaximumRequestBodyBytes);
    }

    [Theory]
    [InlineData("http://poc.example.invalid:7443")]
    [InlineData("https://user:synthetic@poc.example.invalid:7443")]
    [InlineData("https://poc.example.invalid:7443/path")]
    [InlineData("https://poc.example.invalid:7443?secret=synthetic")]
    [InlineData("https://poc.example.invalid:7443/#fragment")]
    [InlineData("https://poc.example.invalid:5100")]
    [InlineData("https://poc.example.invalid:5101")]
    [InlineData("https://poc.example.invalid:5432")]
    [InlineData("https://poc.example.invalid:15672")]
    [InlineData("https://poc.example.invalid.:7443")]
    public void InvalidOrConflictingOriginIsRejectedWithoutEchoingValues(string origin)
    {
        var options = Options(); options.PublicOrigin = origin;
        var error = Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Create(options));
        Assert.DoesNotContain(origin, error.Message);
        Assert.DoesNotContain("synthetic", error.Message);
    }

    [Theory]
    [InlineData("S-1-5-21-1-2-3")]
    [InlineData("S-1-5-80-1")]
    [InlineData("S-1-5-80-1-2-3-4-4294967296")]
    [InlineData("S-1-5-80-1-2-3-4-05")]
    [InlineData("S-1-5-80-1-2-3-4-")]
    public void OnlyCanonicalVirtualServiceSidShapeIsAccepted(string sid)
    {
        var options = Options(); options.ServiceSid = sid;
        Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Create(options));
    }

    [Theory]
    [InlineData("FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED", "true")]
    [InlineData("Gateway:NativeHttps:Unknown", "synthetic")]
    [InlineData("Gateway:NativeHttps:MaximumRequestBodyBytes", "synthetic-private-value")]
    public void UnknownOptionsAndAutomaticProxyTrustFailClosed(string key, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Gateway:NativeHttps:Enabled"] = "true",
            ["Gateway:NativeHttps:PublicOrigin"] = Options().PublicOrigin,
            ["Gateway:NativeHttps:CertificateSha256"] = Options().CertificateSha256,
            ["Gateway:NativeHttps:ServiceSid"] = Options().ServiceSid,
            [key] = value
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var error = Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Read(configuration));
        Assert.DoesNotContain(value, error.Message);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(65535)]
    [InlineData(12582913)]
    public void RequestLimitCannotEscapeBound(int size)
    {
        var options = Options(); options.MaximumRequestBodyBytes = size;
        Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Create(options));
    }

    [Fact]
    public void PublicPortCannotReuseAnyInventoriedComponentOrServicePort()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
        { directory = directory.Parent; }
        Assert.NotNull(directory);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "infra/windows-native/component-manifest.json")));
        var ports = manifest.RootElement.GetProperty("packages").EnumerateArray()
            .SelectMany(package => package.GetProperty("ports").EnumerateArray().Select(port => port.GetInt32()))
            .Concat(NativeServiceCatalog.Services.Select(service => service.Port)).Distinct();
        foreach (var port in ports)
        {
            var options = Options(); options.PublicOrigin = $"https://poc.example.invalid:{port}";
            Assert.Throws<InvalidOperationException>(() => NativeHttpsPlan.Create(options));
        }
    }

    private static NativeHttpsOptions Options() => new()
    {
        Enabled = true,
        PublicOrigin = "https://poc.example.invalid:7443",
        CertificateSha256 = new string('A', 64),
        ServiceSid = "S-1-5-80-1-2-3-4-5"
    };
}
