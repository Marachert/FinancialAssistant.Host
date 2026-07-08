using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.RateLimiting;

public sealed class GatewayRateLimitCatalog
{
    private readonly GatewayRateLimitOptions options;
    private readonly IReadOnlyDictionary<string, GatewayRateLimitPolicyOptions> policies;

    public GatewayRateLimitCatalog(IOptions<GatewayRateLimitOptions> options)
    {
        this.options = options.Value;
        policies = this.options.Policies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        Validate();
    }

    public bool Enabled => options.Enabled;

    public int PolicyCount => policies.Count;

    public int RuleCount => options.Rules.Count;

    public GatewayRateLimitDecision Classify(HttpContext context)
    {
        if (!options.Enabled || IsExcluded(context.Request.Path))
        {
            return GatewayRateLimitDecision.Exempt;
        }

        var path = NormalizePath(context.Request.Path.Value);
        var method = context.Request.Method;
        foreach (var rule in options.Rules)
        {
            if (!MethodMatches(rule, method) || !PathMatches(rule, path))
            {
                continue;
            }

            return CreateDecision(rule.RuleKey, rule.Policy);
        }

        return CreateDecision("default", options.DefaultPolicy);
    }

    private GatewayRateLimitDecision CreateDecision(string ruleKey, string policyName)
    {
        if (!policies.TryGetValue(policyName, out var policy))
        {
            throw new InvalidOperationException($"Gateway rate limit policy '{policyName}' is not configured.");
        }

        return new GatewayRateLimitDecision(true, ruleKey, policyName, policy);
    }

    private bool IsExcluded(PathString requestPath)
    {
        var normalized = NormalizePath(requestPath.Value);
        return options.ExcludedPathPrefixes.Any(prefix =>
            PrefixMatches(normalized, NormalizePath(prefix)));
    }

    private static bool MethodMatches(GatewayRateLimitRuleOptions rule, string method) =>
        rule.Methods.Length == 0
        || rule.Methods.Any(candidate => string.Equals(candidate, method, StringComparison.OrdinalIgnoreCase));

    private static bool PathMatches(GatewayRateLimitRuleOptions rule, string path)
    {
        var configured = NormalizePath(rule.Path);
        return rule.MatchPrefix
            ? PrefixMatches(path, configured)
            : string.Equals(path, configured, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PrefixMatches(string path, string prefix)
    {
        if (string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private void Validate()
    {
        if (!options.Enabled)
        {
            return;
        }

        if (policies.Count == 0
            || !policies.ContainsKey(options.DefaultPolicy)
            || options.MaximumPartitionCount < 1
            || options.PartitionIdleExpirationSeconds < 1)
        {
            throw new InvalidOperationException("Gateway rate limiting requires valid default and partition-cache configuration.");
        }

        foreach (var (name, policy) in policies)
        {
            if (string.IsNullOrWhiteSpace(name)
                || policy.PermitLimit < 1
                || policy.WindowSeconds < 1)
            {
                throw new InvalidOperationException("Gateway rate limit policy configuration is invalid.");
            }
        }

        foreach (var rule in options.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleKey)
                || string.IsNullOrWhiteSpace(rule.Path)
                || !rule.Path.StartsWith("/", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(rule.Policy)
                || !policies.ContainsKey(rule.Policy))
            {
                throw new InvalidOperationException("Gateway rate limit rule configuration is invalid.");
            }
        }
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        var normalized = value.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }
}

public sealed record GatewayRateLimitDecision(
    bool IsLimited,
    string RuleKey,
    string PolicyName,
    GatewayRateLimitPolicyOptions Policy)
{
    public static GatewayRateLimitDecision Exempt { get; } =
        new(false, "exempt", "exempt", new GatewayRateLimitPolicyOptions());
}
