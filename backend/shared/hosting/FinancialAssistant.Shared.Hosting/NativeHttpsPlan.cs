using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Shared.Hosting;

public sealed class NativeHttpsOptions
{
    public bool Enabled { get; set; }
    public string PublicOrigin { get; set; } = "";
    public string BindAddress { get; set; } = "127.0.0.1";
    public bool PublicExposureApproved { get; set; }
    public string CertificateSha256 { get; set; } = "";
    public string ServiceSid { get; set; } = "";
    public int MaximumRequestBodyBytes { get; set; } = 12 * 1024 * 1024;
    public int MaximumRequestHeadersBytes { get; set; } = 32 * 1024;
    public int RequestHeadersTimeoutSeconds { get; set; } = 15;
    public int MinimumCertificateDays { get; set; } = 7;
}

public sealed record NativeHttpsFirewallPlan(string RuleName, int TcpPort, string RemoteAddresses,
    IReadOnlyList<string> Profiles);

public sealed class NativeHttpsPlan
{
    private NativeHttpsPlan(Uri publicOrigin, IPAddress bindAddress, NativeHttpsOptions options)
    {
        PublicOrigin = publicOrigin; BindAddress = bindAddress;
        CertificateSha256 = options.CertificateSha256.ToUpperInvariant(); ServiceSid = options.ServiceSid;
        MaximumRequestBodyBytes = options.MaximumRequestBodyBytes; MaximumRequestHeadersBytes = options.MaximumRequestHeadersBytes;
        RequestHeadersTimeoutSeconds = options.RequestHeadersTimeoutSeconds; MinimumCertificateDays = options.MinimumCertificateDays;
    }

    public const string SectionName = "Gateway:NativeHttps";
    public Uri PublicOrigin { get; }
    public IPAddress BindAddress { get; }
    public string CertificateSha256 { get; }
    public string ServiceSid { get; }
    public int MaximumRequestBodyBytes { get; }
    public int MaximumRequestHeadersBytes { get; }
    public int RequestHeadersTimeoutSeconds { get; }
    public int MinimumCertificateDays { get; }
    public string HostName => PublicOrigin.IdnHost;
    public int Port => PublicOrigin.Port;
    public NativeHttpsFirewallPlan Firewall => new("FinancialAssistant.Gateway.Https", Port, "LocalSubnet", ["Domain", "Private"]);

    public static NativeHttpsPlan? Read(IConfiguration configuration)
    {
        NativeHttpsOptions options;
        try { options = configuration.GetSection(SectionName).Get<NativeHttpsOptions>(binding => binding.ErrorOnUnknownConfiguration = true) ?? new(); }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        { throw Invalid(); }
        if (!options.Enabled) { return null; }
        foreach (var key in new[] { "FORWARDEDHEADERS_ENABLED", "ASPNETCORE_FORWARDEDHEADERS_ENABLED" })
        {
            var value = configuration[key];
            if (value is not null && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) { throw Invalid(); }
        }
        return Create(options);
    }

    public static NativeHttpsPlan Create(NativeHttpsOptions options)
    {
        if (!options.Enabled || options.PublicOrigin is null || options.CertificateSha256 is null || options.ServiceSid is null || options.PublicOrigin.Length > 512 ||
            !Uri.TryCreate(options.PublicOrigin, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0 ||
            origin.IdnHost.EndsWith('.') || origin.IdnHost.Length > 253 || origin.Port is < 1 or > 65535 ||
            NativeServiceCatalog.Services.Any(service => service.Port == origin.Port) ||
            new[] { 5432, 4369, 5672, 15672, 25672, 9200, 9300, 9600, 9090, 9093, 3000, 4317, 4318, 16686, 13133 }.Contains(origin.Port) ||
            !IPAddress.TryParse(options.BindAddress, out var address) || address.AddressFamily != AddressFamily.InterNetwork ||
            address.Equals(IPAddress.Broadcast) || address.GetAddressBytes()[0] is >= 224 and <= 239 ||
            !IPAddress.IsLoopback(address) && (!options.PublicExposureApproved || origin.IsLoopback) ||
            options.CertificateSha256.Length != 64 || !options.CertificateSha256.All(char.IsAsciiHexDigit) ||
            !ValidServiceSid(options.ServiceSid) ||
            options.MaximumRequestBodyBytes is < 65536 or > 12 * 1024 * 1024 ||
            options.MaximumRequestHeadersBytes is < 8192 or > 32768 ||
            options.RequestHeadersTimeoutSeconds is < 5 or > 30 || options.MinimumCertificateDays is < 1 or > 90)
        { throw Invalid(); }
        return new(origin, address, options);
    }

    private static InvalidOperationException Invalid() => new("Gateway:NativeHttps configuration is invalid or lacks explicit perimeter approval.");

    private static bool ValidServiceSid(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 9 && parts[0] == "S" && parts[1] == "1" && parts[2] == "5" && parts[3] == "80" &&
            parts.Skip(4).All(part => uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var component) &&
                component.ToString(CultureInfo.InvariantCulture) == part);
    }
}
