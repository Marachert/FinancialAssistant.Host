namespace FinancialAssistant.Shared.Hosting;

public sealed record NativeServiceDefinition(
    string Id,
    int Port,
    IReadOnlyList<string> RequiredSecretKeys)
{
    public string ServiceName => $"FinancialAssistant.{Id}";

    public string BaseAddress => $"http://127.0.0.1:{Port}";

    public string ServiceAccount => $"NT SERVICE\\{ServiceName}";

    public IReadOnlyList<string> RequiredComponents => Array.AsReadOnly(Id switch
    {
        "gateway" => new[] { "aspnet-runtime", "identity", "profile", "category", "transaction-intake", "receipt-processing",
            "income", "expense", "analytics", "financial-score", "recommendations-notifications", "monitoring", "audit" },
        "mcp" => new[] { "aspnet-runtime", "monitoring", "audit" },
        _ => new[] { "aspnet-runtime", "postgresql", "rabbitmq" }
    });

    public string ExecutableName => Id switch
    {
        "gateway" => "FinancialAssistant.PublicApiGateway.exe",
        "identity" => "FinancialAssistant.Identity.Api.exe",
        "profile" => "FinancialAssistant.Profile.Api.exe",
        "category" => "FinancialAssistant.Category.Api.exe",
        "transaction-intake" => "FinancialAssistant.TransactionIntake.Api.exe",
        "receipt-processing" => "FinancialAssistant.ReceiptProcessing.Api.exe",
        "analytics" => "FinancialAssistant.Analytics.Api.exe",
        "financial-score" => "FinancialAssistant.FinancialScore.Api.exe",
        "recommendations-notifications" => "FinancialAssistant.RecommendationsNotifications.Api.exe",
        "monitoring" => "FinancialAssistant.Monitoring.Api.exe",
        "income" => "FinancialAssistant.Income.Api.exe",
        "expense" => "FinancialAssistant.Expense.Api.exe",
        "audit" => "FinancialAssistant.Audit.Api.exe",
        "ai-orchestration" => "FinancialAssistant.AiOrchestration.Api.exe",
        "mcp" => "FinancialAssistant.Mcp.Api.exe",
        _ => throw new InvalidOperationException("Unknown native executable identity.")
    };
}

public static class NativeServiceCatalog
{
    public static IReadOnlyList<NativeServiceDefinition> Services { get; } = Array.AsReadOnly(
        new NativeServiceDefinition[]
        {
            Define("gateway", 5100, "Gateway:Security:AccessTokenSigningKey", "Gateway:DownstreamAuthentication:SharedSecret"),
            Define("identity", 5101, "Identity:Authentication:AccessTokenSigningKey", "Identity:Authentication:LookupHmacKey",
                "Identity:Authentication:RefreshTokenHashKey", "Identity:Providers:IdentifierHmacKey", "Identity:Events:UserIdHmacKey",
                "Identity:Storage:Postgres:Password", "Identity:Storage:Postgres:IndexKey"),
            Define("profile", 5102, "Profile:Gateway:SharedSecret", "Profile:Storage:Postgres:Password", "Profile:Storage:Postgres:IndexKey"),
            Define("category", 5103, "Category:Gateway:SharedSecret", "Category:Storage:Postgres:Password", "Category:Storage:Postgres:IndexKey"),
            Define("transaction-intake", 5104, "TransactionIntake:Gateway:SharedSecret", "ReceiptProcessing:Events:SharedSecret"),
            Define("receipt-processing", 5105, "ReceiptProcessing:Gateway:SharedSecret", "ReceiptProcessing:Events:SharedSecret"),
            Define("analytics", 5106, "Analytics:Gateway:SharedSecret"),
            Define("financial-score", 5107, "FinancialScore:Gateway:SharedSecret"),
            Define("recommendations-notifications", 5108, "RecommendationsNotifications:Gateway:SharedSecret"),
            Define("monitoring", 5110, "Monitoring:Gateway:SharedSecret", "Monitoring:Signals:SharedSecret"),
            Define("income", 5111, "Income:Gateway:SharedSecret", "Income:Storage:Postgres:Password", "Income:Storage:Postgres:IndexKey"),
            Define("expense", 5112, "Expense:Gateway:SharedSecret", "Expense:Storage:Postgres:Password", "Expense:Storage:Postgres:IndexKey"),
            Define("audit", 5113, "Audit:Gateway:SharedSecret", "Audit:Services:SharedSecret"),
            Define("ai-orchestration", 5114, "AiOrchestration:Gateway:SharedSecret"),
            Define("mcp", 5115, "Mcp:Authentication:SharedSecret", "Mcp:Monitoring:SharedSecret", "Mcp:Audit:SharedSecret")
        });

    public static NativeServiceDefinition Get(string id) =>
        Services.SingleOrDefault(service => string.Equals(service.Id, id, StringComparison.Ordinal))
        ?? throw new ArgumentException("Unknown native service identity.", nameof(id));

    private static NativeServiceDefinition Define(string id, int port, params string[] secrets) =>
        new(id, port, Array.AsReadOnly(secrets));
}
