using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace FinancialAssistant.Shared.Hosting;

public static class FinancialAssistantHost
{
    public const string NativeEnvironment = "WindowsNative";

    public static WebApplicationBuilder CreateBuilder(string[] args, string serviceId)
    {
        var service = NativeServiceCatalog.Get(serviceId);
        var hostConfiguration = new ConfigurationBuilder()
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables("DOTNET_")
            .AddCommandLine(args)
            .Build();
        var nativeRequested = string.Equals(hostConfiguration["environment"], NativeEnvironment, StringComparison.OrdinalIgnoreCase);
        var underScm = WindowsServiceHelpers.IsWindowsService();
        if (underScm && !nativeRequested)
        {
            throw new InvalidOperationException("Windows services require the WindowsNative environment.");
        }

        // SCM starts in System32; published resources must not depend on the working directory.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = nativeRequested || underScm ? AppContext.BaseDirectory : null
        });
        builder.Services.AddWindowsService(options => options.ServiceName = service.ServiceName);

        if (builder.Environment.IsEnvironment(NativeEnvironment))
        {
            ConfigureNative(builder, service);
        }

        return builder;
    }

    public static void ConfigureNative(WebApplicationBuilder builder, NativeServiceDefinition service)
    {
        ValidateConfiguration(builder.Configuration, service);
        if (service.Id == "receipt-processing")
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReceiptProcessing:TransactionIntake:BaseAddress"] = NativeServiceCatalog.Get("transaction-intake").BaseAddress
            });
        }
        else if (service.Id == "mcp")
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:Monitoring:BaseAddress"] = NativeServiceCatalog.Get("monitoring").BaseAddress,
                ["Mcp:Audit:BaseAddress"] = NativeServiceCatalog.Get("audit").BaseAddress,
                ["Mcp:Audit:Mode"] = "Http"
            });
        }
        else if (service.Id == "monitoring")
        {
            var targets = NativeServiceCatalog.Services.Where(target => target.Id != "monitoring").ToArray();
            foreach (var child in builder.Configuration.GetSection("Monitoring:Services").GetChildren())
            {
                if (!int.TryParse(child.Key, out var index) || index < 0 || index >= targets.Length
                    || child.Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("Native monitoring service target indices must belong to the canonical catalog.");
                }
            }

            var overrides = new Dictionary<string, string?>();
            for (var index = 0; index < targets.Length; index++)
            {
                overrides[$"Monitoring:Services:{index}:Name"] = targets[index].Id;
                overrides[$"Monitoring:Services:{index}:BaseAddress"] = targets[index].BaseAddress;
            }

            overrides["Monitoring:RabbitMq:ManagementBaseAddress"] = "http://127.0.0.1:15672";
            overrides["Monitoring:Elasticsearch:BaseAddress"] = "http://127.0.0.1:9200";
            builder.Configuration.AddInMemoryCollection(overrides);
        }

        builder.Services.AddSingleton(service);
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, service.Port));
    }

    public static void ValidateConfiguration(IConfiguration configuration, NativeServiceDefinition service)
    {
        if (configuration.GetSection("Kestrel:Endpoints").GetChildren().Any())
        {
            throw new InvalidOperationException("Native Kestrel endpoints are owned by the hosting profile; external endpoints require FIN-207.");
        }

        var urls = configuration["urls"];
        if (!string.IsNullOrEmpty(urls) && !string.Equals(urls.TrimEnd('/'), service.BaseAddress, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Native services require their canonical loopback endpoint.");
        }

        foreach (var key in service.RequiredSecretKeys)
        {
            RequireSecret(configuration, key);
        }

        // Native POC development never opts into real provider billing through configuration.
        foreach (var key in new[] { "AiOrchestration:Provider:Enabled", "ReceiptProcessing:Ocr:Enabled", "Identity:Providers:Phone:Enabled" })
        {
            if (configuration.GetValue<bool>(key))
            {
                throw new InvalidOperationException("Live paid providers must remain disabled in the native POC development profile.");
            }
        }
    }

    public static string RequireSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (value is null || value.Length is < 32 or > 256 || value.Any(character => character is < '!' or > '~'))
        {
            throw new InvalidOperationException($"{key} requires 32-256 printable non-whitespace ASCII characters.");
        }

        return value;
    }

    public static WebApplication UseWindowsNativeRequestBoundary(this WebApplication app, Func<HttpContext, bool>? authorizeInternalProfileRequest = null)
    {
        if (!app.Environment.IsEnvironment(NativeEnvironment))
        {
            return app;
        }

        var service = app.Services.GetRequiredService<NativeServiceDefinition>();
        if (service.Id is not ("profile" or "ai-orchestration"))
        {
            return app;
        }

        var secretHash = SHA256.HashData(Encoding.ASCII.GetBytes(RequireSecret(app.Configuration, service.RequiredSecretKeys[0])));
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/health" || context.Request.Path == "/health/live" || context.Request.Path == "/health/ready")
            {
                await next(context);
                return;
            }

            if (service.Id == "profile" && authorizeInternalProfileRequest?.Invoke(context) == true)
            {
                await next(context);
                return;
            }

            var supplied = context.Request.Headers["X-Gateway-Authentication"];
            if (supplied.Count != 1 || supplied[0] is not { Length: >= 32 and <= 256 } candidate
                || candidate.Any(character => character is < '!' or > '~')
                || !CryptographicOperations.FixedTimeEquals(secretHash, SHA256.HashData(Encoding.ASCII.GetBytes(candidate))))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        });
        return app;
    }
}
