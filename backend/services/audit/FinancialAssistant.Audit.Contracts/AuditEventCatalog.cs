namespace FinancialAssistant.Audit.Contracts;

public static class AuditActorTypes
{
    public const string Anonymous = "anonymous";
    public const string User = "user";
    public const string Admin = "admin";
    public const string Service = "service";
    public const string System = "system";
}

public static class AuditOutcomes
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Denied = "denied";
    public const string Accepted = "accepted";
}

public static class AuditRetentionClasses
{
    public const string Standard = "standard";
    public const string Security = "security";
    public const string Regulatory = "regulatory";
}

public static class AuditResourceTypes
{
    public const string Profile = "profile";
    public const string Income = "income";
    public const string Expense = "expense";
    public const string TransactionDraft = "transaction-draft";
    public const string AuthenticationAttempt = "authentication-attempt";
    public const string Session = "session";
    public const string AuditTrail = "audit-trail";
    public const string MonitoringDashboard = "monitoring-dashboard";
    public const string AdminOperation = "admin-operation";
    public const string McpTool = "mcp-tool";
}

public static class AuditActions
{
    public const string ProfileUpdated = "profile.updated";
    public const string ProfilePreferencesUpdated = "profile.preferences.updated";
    public const string ProfileConsentUpdated = "profile.consent.updated";
    public const string IncomeCreated = "income.created";
    public const string IncomeUpdated = "income.updated";
    public const string IncomeArchived = "income.archived";
    public const string IncomeRestored = "income.restored";
    public const string ExpenseCreated = "expense.created";
    public const string ExpenseUpdated = "expense.updated";
    public const string ExpenseArchived = "expense.archived";
    public const string ExpenseRestored = "expense.restored";
    public const string DraftConfirmed = "draft.confirmed";
    public const string AuthenticationSucceeded = "authentication.succeeded";
    public const string AuthenticationFailed = "authentication.failed";
    public const string SessionCreated = "session.created";
    public const string SessionRefreshed = "session.refreshed";
    public const string SessionRevoked = "session.revoked";
    public const string AdminAuditViewed = "admin.audit.viewed";
    public const string AdminMonitoringViewed = "admin.monitoring.viewed";
    public const string AdminActionExecuted = "admin.action.executed";
}

public sealed record AuditEventDefinition(
    string Action,
    string Domain,
    string ResourceType,
    string RetentionClass,
    IReadOnlyList<string> Producers,
    IReadOnlyList<string> ActorTypes);

public static class AuditEventCatalog
{
    private static readonly IReadOnlyList<AuditEventDefinition> DefinitionItems =
        Array.AsReadOnly(CreateDefinitions().ToArray());
    private static readonly IReadOnlyDictionary<string, AuditEventDefinition> DefinitionsByAction =
        DefinitionItems.ToDictionary(item => item.Action, StringComparer.Ordinal);

    public static IReadOnlyCollection<AuditEventDefinition> Definitions =>
        DefinitionItems;

    public static bool TryGet(string action, out AuditEventDefinition? definition) =>
        DefinitionsByAction.TryGetValue(action, out definition);

    private static IEnumerable<AuditEventDefinition> CreateDefinitions()
    {
        var userOrAdmin = ReadOnly(AuditActorTypes.User, AuditActorTypes.Admin);
        var userAdminOrSystem = ReadOnly(
            AuditActorTypes.User,
            AuditActorTypes.Admin,
            AuditActorTypes.System);
        var sessionActors = ReadOnly(
            AuditActorTypes.User,
            AuditActorTypes.Admin,
            AuditActorTypes.System,
            AuditActorTypes.Service);

        yield return Define(AuditActions.ProfileUpdated, AuditDomains.Business,
            AuditResourceTypes.Profile, AuditRetentionClasses.Standard, "profile-service", userOrAdmin);
        yield return Define(AuditActions.ProfilePreferencesUpdated, AuditDomains.Business,
            AuditResourceTypes.Profile, AuditRetentionClasses.Standard, "profile-service", userOrAdmin);
        yield return Define(AuditActions.ProfileConsentUpdated, AuditDomains.Business,
            AuditResourceTypes.Profile, AuditRetentionClasses.Security, "profile-service", userOrAdmin);

        foreach (var action in new[]
        {
            AuditActions.IncomeCreated,
            AuditActions.IncomeUpdated,
            AuditActions.IncomeArchived,
            AuditActions.IncomeRestored
        })
        {
            yield return Define(action, AuditDomains.Business, AuditResourceTypes.Income,
                AuditRetentionClasses.Regulatory, "income-service", userAdminOrSystem);
        }

        foreach (var action in new[]
        {
            AuditActions.ExpenseCreated,
            AuditActions.ExpenseUpdated,
            AuditActions.ExpenseArchived,
            AuditActions.ExpenseRestored
        })
        {
            yield return Define(action, AuditDomains.Business, AuditResourceTypes.Expense,
                AuditRetentionClasses.Regulatory, "expense-service", userAdminOrSystem);
        }

        yield return Define(AuditActions.DraftConfirmed, AuditDomains.Business,
            AuditResourceTypes.TransactionDraft, AuditRetentionClasses.Regulatory,
            "transaction-intake-service", [AuditActorTypes.User]);
        yield return Define(AuditActions.AuthenticationSucceeded, AuditDomains.Security,
            AuditResourceTypes.AuthenticationAttempt, AuditRetentionClasses.Security,
            "identity-service", [AuditActorTypes.User, AuditActorTypes.Service]);
        yield return Define(AuditActions.AuthenticationFailed, AuditDomains.Security,
            AuditResourceTypes.AuthenticationAttempt, AuditRetentionClasses.Security,
            "identity-service", [AuditActorTypes.Anonymous, AuditActorTypes.User, AuditActorTypes.Service]);

        foreach (var action in new[]
        {
            AuditActions.SessionCreated,
            AuditActions.SessionRefreshed,
            AuditActions.SessionRevoked
        })
        {
            yield return Define(action, AuditDomains.Security, AuditResourceTypes.Session,
                AuditRetentionClasses.Security, "identity-service", sessionActors);
        }

        yield return Define(AuditActions.AdminAuditViewed, AuditDomains.Admin,
            AuditResourceTypes.AuditTrail, AuditRetentionClasses.Security,
            "audit-service", [AuditActorTypes.Admin]);
        yield return Define(AuditActions.AdminMonitoringViewed, AuditDomains.Admin,
            AuditResourceTypes.MonitoringDashboard, AuditRetentionClasses.Security,
            "monitoring-service", [AuditActorTypes.Admin]);
        yield return new AuditEventDefinition(
            AuditActions.AdminActionExecuted,
            AuditDomains.Admin,
            AuditResourceTypes.AdminOperation,
            AuditRetentionClasses.Security,
            ReadOnly("audit-service", "monitoring-service", "mcp-service"),
            ReadOnly(AuditActorTypes.Admin));
    }

    private static AuditEventDefinition Define(
        string action,
        string domain,
        string resourceType,
        string retentionClass,
        string producer,
        IReadOnlyList<string> actorTypes) =>
        new(
            action,
            domain,
            resourceType,
            retentionClass,
            ReadOnly(producer),
            Array.AsReadOnly(actorTypes.ToArray()));

    private static IReadOnlyList<string> ReadOnly(params string[] values) =>
        Array.AsReadOnly(values);
}
