namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public string DefaultPolicy { get; set; } = "general";
    public int MaximumPartitionCount { get; set; } = 10000;
    public int PartitionIdleExpirationSeconds { get; set; } = 900;
    public Dictionary<string, GatewayRateLimitPolicyOptions> Policies { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<GatewayRateLimitRuleOptions> Rules { get; set; } = new();
    public List<string> ExcludedPathPrefixes { get; set; } = new();
}

public sealed class GatewayRateLimitPolicyOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class GatewayRateLimitRuleOptions
{
    public string RuleKey { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool MatchPrefix { get; set; }
    public string Policy { get; set; } = string.Empty;
    public string[] Methods { get; set; } = Array.Empty<string>();
}
