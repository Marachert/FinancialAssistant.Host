using System.Security.Cryptography;
using FinancialAssistant.Shared.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FinancialAssistant.Shared.Hosting.Tests;

public sealed class NativeConfigurationBootstrapTests
{
    [Fact]
    public void InstalledConfigurationDiscardsAllPreviousSourcesAndUsesOnlyProtectedSecrets()
    {
        const string key = "Monitoring:Gateway:SharedSecret";
        var plan = NativeServiceConfigurationPlan.Create(NativeServiceConfigurationTests.Document("monitoring"));
        var inputs = NativeConfigurationSourceTests.Inputs(plan);
        try
        {
            using var package = NativeConfigurationPackage.Create(plan, inputs, new NativeConfigurationSourceTests.SyntheticProtector());
            using var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [key] = "synthetic-command-line-secret", ["unexpected-process-setting"] = "untrusted" });
            var baseline = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = "", ["Logging:LogLevel:Default"] = "Warning" }).Build();
            using var baselineLifetime = (IDisposable)baseline;
            NativeConfigurationBootstrap.Replace(configuration, baseline, new NativeConfigurationSource("monitoring", () => package,
                new NativeConfigurationSourceTests.SyntheticProtector()));
            Assert.Equal(System.Text.Encoding.UTF8.GetString(inputs[key]), configuration[key]);
            Assert.Null(configuration["unexpected-process-setting"]);
            Assert.Equal("Warning", configuration["Logging:LogLevel:Default"]);
            Assert.Equal("false", configuration["AiOrchestration:Provider:Enabled"]);
        }
        finally { foreach (var input in inputs.Values) { CryptographicOperations.ZeroMemory(input); } }
    }

    [Fact]
    public void MissingProtectedStoreFailsInsteadOfUsingAnExistingPlaintextSecret()
    {
        using var configuration = new ConfigurationManager();
        configuration["Monitoring:Gateway:SharedSecret"] = new string('x', 64);
        var baseline = new ConfigurationBuilder().Build();
        using var lifetime = (IDisposable)baseline;
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationBootstrap.Replace(configuration, baseline,
            new NativeConfigurationSource("monitoring", () => throw new IOException("synthetic-private-path"), new WindowsDpapiSecretProtector())));
        Assert.Null(configuration["Monitoring:Gateway:SharedSecret"]);
    }

    [Fact]
    public void PackagedJsonCannotBecomeAPlaintextSecretStore()
    {
        using var configuration = new ConfigurationManager();
        var baseline = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Identity:Authentication:AccessTokenSigningKey"] = new string('x', 64) }).Build();
        using var lifetime = (IDisposable)baseline;
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationBootstrap.Replace(configuration, baseline,
            new NativeConfigurationSource("identity", () => throw new IOException(), new WindowsDpapiSecretProtector())));
    }

    [Fact]
    public void ConsoleBootstrapRemainsExplicitlySeparateFromInstalledServiceLoading()
    {
        using var configuration = new ConfigurationManager();
        configuration["synthetic-fixture"] = "console-only";
        NativeConfigurationBootstrap.Apply(configuration, "monitoring", underScm: false);
        Assert.Equal("console-only", configuration["synthetic-fixture"]);
    }

    [Theory]
    [InlineData("FORWARDEDHEADERS_ENABLED")]
    [InlineData("ASPNETCORE_FORWARDEDHEADERS_ENABLED")]
    public void ProviderReplacementCannotEraseEvidenceOfAutomaticForwardingActivation(string key)
    {
        using var configuration = new ConfigurationManager(); configuration[key] = "true";
        var baseline = new ConfigurationBuilder().Build(); using var lifetime = (IDisposable)baseline;
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationBootstrap.Replace(configuration, baseline,
            new NativeConfigurationSource("gateway", () => throw new IOException(), new WindowsDpapiSecretProtector())));
    }
}
