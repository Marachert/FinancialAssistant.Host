using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimitPartitioner
{
    private const int MaximumClientInstanceLength = 128;
    private readonly GatewayRateLimitOptions options;

    public GatewayRateLimitPartitioner(IOptions<GatewayRateLimitOptions> options)
    {
        this.options = options.Value;
    }

    public string CreatePartitionKey(HttpContext context, GatewayRateLimitDecision decision)
    {
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var clientInstance = ReadClientInstance(context);
        var material = $"{decision.PolicyName}|{remoteAddress}|{clientInstance}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"{decision.PolicyName}:{Convert.ToHexString(digest)}";
    }

    private string ReadClientInstance(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(options.ClientInstanceHeaderName))
        {
            return "none";
        }

        var value = context.Request.Headers[options.ClientInstanceHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < 8
            || value.Length > MaximumClientInstanceLength
            || value.Any(character => char.IsControl(character)))
        {
            return "none";
        }

        return value;
    }
}
