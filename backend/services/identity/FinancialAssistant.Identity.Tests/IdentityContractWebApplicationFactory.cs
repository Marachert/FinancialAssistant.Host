using FinancialAssistant.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityContractWebApplicationFactory : WebApplicationFactory<Program>
{
    public CapturingIdentityEventPublisher EventPublisher { get; } = new();
    public StubGoogleIdentityTokenValidator GoogleTokenValidator { get; } = new();
    public StubAppleIdentityTokenValidator AppleTokenValidator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IIdentityEventPublisher>();
            services.AddSingleton<IIdentityEventPublisher>(EventPublisher);
            services.RemoveAll<IGoogleIdentityTokenValidator>();
            services.AddSingleton<IGoogleIdentityTokenValidator>(GoogleTokenValidator);
            services.RemoveAll<IAppleIdentityTokenValidator>();
            services.AddSingleton<IAppleIdentityTokenValidator>(AppleTokenValidator);
        });
    }
}
