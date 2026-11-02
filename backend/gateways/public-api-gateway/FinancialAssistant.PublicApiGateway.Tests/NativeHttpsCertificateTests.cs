using FinancialAssistant.PublicApiGateway.Security;
using FinancialAssistant.Shared.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class NativeHttpsCertificateTests
{
    [Fact]
    public void IssuedSanCertificateIsUsableButUnknownSniHasNoFallback()
    {
        using var certs = new NativeHttpsTestCertificates();
        using var runtime = new NativeHttpsRuntime(certs.Plan(), new SyntheticHttpsCertificateSource(certs.Leaf), TimeProvider.System);
        Assert.True(runtime.IsUsable(true));
        Assert.NotNull(runtime.Select("localhost"));
        Assert.NotNull(runtime.Select("LOCALHOST"));
        Assert.Null(runtime.Select(null));
        Assert.Null(runtime.Select("foreign.example.invalid"));
    }

    [Theory]
    [InlineData(false, true, false, 2048)]
    [InlineData(true, false, false, 2048)]
    [InlineData(true, true, true, 2048)]
    [InlineData(true, true, false, 1024)]
    public void MissingSanWrongEkuSelfSignedAndWeakKeysFailBeforeListening(bool san, bool serverUsage, bool selfSigned, int keySize)
    {
        using var certs = new NativeHttpsTestCertificates(san: san, serverUsage: serverUsage, selfSigned: selfSigned, keySize: keySize);
        Assert.Throws<InvalidOperationException>(() => new NativeHttpsRuntime(certs.Plan(), new SyntheticHttpsCertificateSource(certs.Leaf), TimeProvider.System));
    }

    [Fact]
    public void ExpiredOrNotYetValidCertificatesFailBeforeListening()
    {
        var now = DateTimeOffset.UtcNow;
        using var expired = new NativeHttpsTestCertificates(notBefore: now.AddDays(-30), notAfter: now.AddDays(-1));
        using var future = new NativeHttpsTestCertificates(notBefore: now.AddDays(1), notAfter: now.AddDays(31));
        Assert.Throws<InvalidOperationException>(() => new NativeHttpsRuntime(expired.Plan(), new SyntheticHttpsCertificateSource(expired.Leaf), TimeProvider.System));
        Assert.Throws<InvalidOperationException>(() => new NativeHttpsRuntime(future.Plan(), new SyntheticHttpsCertificateSource(future.Leaf), TimeProvider.System));
    }

    [Fact]
    public void WrongFingerprintOrHostnameCannotBecomeReady()
    {
        using var first = new NativeHttpsTestCertificates();
        using var second = new NativeHttpsTestCertificates();
        using var foreign = new NativeHttpsTestCertificates(host: "foreign.example.invalid");
        Assert.Throws<InvalidOperationException>(() => new NativeHttpsRuntime(first.Plan(), new SyntheticHttpsCertificateSource(second.Leaf), TimeProvider.System));
        Assert.Throws<InvalidOperationException>(() => new NativeHttpsRuntime(foreign.Plan(), new SyntheticHttpsCertificateSource(foreign.Leaf), TimeProvider.System));
    }

    [Fact]
    public async Task RenewalWarningAndExpiryAreCheckedAfterStartup()
    {
        using var certs = new NativeHttpsTestCertificates();
        var clock = new Clock(DateTimeOffset.UtcNow);
        using var runtime = new NativeHttpsRuntime(certs.Plan(), new SyntheticHttpsCertificateSource(certs.Leaf), clock);
        clock.Now = clock.Now.AddDays(24);
        Assert.True(runtime.IsUsable(false));
        Assert.False(runtime.IsUsable(true));
        Assert.Equal(HealthStatus.Unhealthy, (await new NativeHttpsReadinessHealthCheck(runtime).CheckHealthAsync(new())).Status);
        clock.Now = clock.Now.AddDays(7);
        Assert.False(runtime.IsUsable(false));
        Assert.Null(runtime.Select("localhost"));
        Assert.Equal(HealthStatus.Unhealthy, (await new NativeHttpsReadinessHealthCheck().CheckHealthAsync(new())).Status);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
