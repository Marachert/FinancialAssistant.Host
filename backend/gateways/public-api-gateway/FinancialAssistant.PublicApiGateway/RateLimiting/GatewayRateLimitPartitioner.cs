using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimitPartitioner
{
    public string CreatePartitionKey(HttpContext context, GatewayRateLimitDecision decision)
    {
        var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var material = $"{decision.PolicyName}|{remoteAddress}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"{decision.PolicyName}:{Convert.ToHexString(digest)}";
    }
}
