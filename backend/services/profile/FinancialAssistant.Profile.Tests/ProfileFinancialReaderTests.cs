using FinancialAssistant.Profile.Api.Security;
using FinancialAssistant.Profile.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FinancialAssistant.Profile.Tests;

public sealed class ProfileFinancialReaderTests
{
    private static Dictionary<string, string?> Settings() => new()
    {
        ["Profile:FinancialReaders:Enabled"] = "true",
        ["Profile:Gateway:SharedSecret"] = new('g', 48),
        ["Profile:FinancialReaders:analytics:SharedSecret"] = new('a', 48),
        ["Profile:FinancialReaders:financial-score:SharedSecret"] = new('s', 48),
        ["Profile:FinancialReaders:recommendations-notifications:SharedSecret"] = new('r', 48)
    };
    private static ProfileFinancialReaderAuthenticator Authentication(Dictionary<string, string?>? settings = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings ?? Settings()).Build());
    private static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext(); context.Request.Method = "POST"; context.Request.Path = ProfileFinancialSettingsApi.Path;
        context.Request.Headers[ProfileFinancialSettingsApi.ReaderHeader] = "analytics";
        context.Request.Headers[ProfileFinancialSettingsApi.AuthenticationHeader] = new string('a', 48);
        return context;
    }

    [Fact]
    public void ScopedReaderAuthenticatesOnlyItsExactInternalReadPath()
    {
        var authentication = Authentication(); var context = Request();
        Assert.True(authentication.IsAuthenticated(context));
        context.Request.Path = "/api/v1/profile/me/preferences";
        Assert.False(authentication.IsAuthenticated(context));
        context.Request.Path = ProfileFinancialSettingsApi.Path; context.Request.Method = "GET";
        Assert.False(authentication.IsAuthenticated(context));
    }

    [Fact]
    public void ForeignRoleWrongSecretAndRepeatedHeadersAreDenied()
    {
        var authentication = Authentication(); var context = Request();
        context.Request.Headers[ProfileFinancialSettingsApi.ReaderHeader] = "financial-score";
        Assert.False(authentication.IsAuthenticated(context));
        context.Request.Headers[ProfileFinancialSettingsApi.ReaderHeader] = "gateway";
        Assert.False(authentication.IsAuthenticated(context));
        context = Request(); context.Request.Headers[ProfileFinancialSettingsApi.AuthenticationHeader] = new Microsoft.Extensions.Primitives.StringValues(new[] { new string('a', 48), new string('a', 48) });
        Assert.False(authentication.IsAuthenticated(context));
        context = Request(); context.Request.Headers[ProfileFinancialSettingsApi.ReaderHeader] = new Microsoft.Extensions.Primitives.StringValues(new[] { "analytics", "analytics" });
        Assert.False(authentication.IsAuthenticated(context));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("gateway")]
    [InlineData("peer")]
    public void MissingWeakGatewaySharedAndPeerSharedCredentialsFailClosed(string? value)
    {
        var settings = Settings();
        settings["Profile:FinancialReaders:analytics:SharedSecret"] = value switch
        { "gateway" => new string('g', 48), "peer" => new string('s', 48), _ => value };
        Assert.Throws<InvalidOperationException>(() => Authentication(settings));
    }

    [Fact]
    public void DisabledReadersNeverFallBackToGatewayAuthentication()
    {
        var settings = Settings(); settings["Profile:FinancialReaders:Enabled"] = "false";
        Assert.False(Authentication(settings).IsAuthenticated(Request()));
    }
}
