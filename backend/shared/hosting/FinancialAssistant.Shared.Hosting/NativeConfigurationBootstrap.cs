using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Shared.Hosting;

internal static class NativeConfigurationBootstrap
{
    internal static void Apply(ConfigurationManager configuration, string serviceId, bool underScm)
    {
        if (!underScm) { return; }
        try
        {
            // Only the packaged, non-secret baseline survives; process environment and CLI cannot supply runtime secrets.
            var baseline = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
            using var lifetime = (IDisposable)baseline;
            Replace(configuration, baseline, new NativeConfigurationSource(serviceId,
                () => WindowsNativeConfigurationArtifacts.Open(serviceId), new WindowsDpapiSecretProtector()));
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or
            UnauthorizedAccessException or FormatException or System.Security.SecurityException)
        { throw new InvalidOperationException("Installed native service configuration cannot be loaded safely."); }
    }

    internal static void Replace(ConfigurationManager configuration, IConfiguration baseline, IConfigurationSource protectedSource)
    {
        foreach (var key in new[] { "FORWARDEDHEADERS_ENABLED", "ASPNETCORE_FORWARDEDHEADERS_ENABLED" })
        {
            if (configuration[key] is { } value && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            { throw new InvalidOperationException("Installed native services cannot enable automatic forwarded headers."); }
        }
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in baseline.AsEnumerable())
        {
            if (NativeServiceCatalog.Services.Any(service => NativeSecretBinding.IsOwnedSecretKey(service.Id, pair.Key)))
            {
                if (!string.IsNullOrEmpty(pair.Value))
                { throw new InvalidOperationException("Packaged native configuration must not contain runtime secrets."); }
                continue;
            }
            values.Add(pair.Key, pair.Value);
        }
        configuration.Sources.Clear();
        configuration.AddInMemoryCollection(values);
        ((IConfigurationBuilder)configuration).Add(protectedSource);
    }
}
