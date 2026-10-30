using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Infrastructure;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Infrastructure;
using FinancialAssistant.Expense.Infrastructure.Persistence;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Infrastructure;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Profile.Api.Endpoints;
using FinancialAssistant.Profile.Api.Security;
using FinancialAssistant.Profile.Application;
using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Profile.Infrastructure;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Infrastructure;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static partial class LifecycleFixture
{
    private const string Correlation = "synthetic-native-lifecycle";
    private static readonly string Owner = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(User))).ToLowerInvariant();
    private static readonly Dictionary<string, string> Prefixes = new(StringComparer.Ordinal)
    {
        ["profile"] = "Profile",
        ["transaction-intake"] = "TransactionIntake",
        ["income"] = "Income",
        ["expense"] = "Expense",
        ["analytics"] = "Analytics",
        ["financial-score"] = "FinancialScore",
        ["recommendations-notifications"] = "RecommendationsNotifications",
        ["audit"] = "Audit"
    };

    private static async Task RunAsync(string root, Credentials credentials, string mode, CancellationToken token)
    {
        var port = int.Parse(Setting("FA_PERSISTENCE_TEST_PORT"), System.Globalization.CultureInfo.InvariantCulture);
        var profileConfig = Configuration(credentials, "profile", port, null);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = FinancialAssistantHost.NativeEnvironment });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddConfiguration(profileConfig);
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(NativeServiceCatalog.Get("profile"));
        builder.Services.AddProfileApplication().AddProfileInfrastructure(builder.Configuration, true);
        builder.Services.AddSingleton<ProfileFinancialReaderAuthenticator>();
        await using var app = builder.Build();
        app.UseWindowsNativeRequestBoundary(app.Services.GetRequiredService<ProfileFinancialReaderAuthenticator>().IsAuthenticated);
        app.UseOwnedStorageFailureBoundary(); app.MapProfileEndpoints(); app.MapNativeProfileFinancialSettings();
        await app.StartAsync(token);
        var address = app.Urls.Single();
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
        { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };
        Stage = "Profile scoped HTTP authorization";
        using (var denied = await http.PostAsJsonAsync(ProfileFinancialSettingsApi.Path, new { ownerHash = Owner }, token))
        { Check(denied.StatusCode == HttpStatusCode.Unauthorized); }
        http.DefaultRequestHeaders.Add("X-Gateway-Authentication", credentials.GatewaySecret);
        http.DefaultRequestHeaders.Add(ProfileGatewayHeaders.UserId, User);
        if (mode == "initial")
        {
            using var created = await http.PostAsJsonAsync(ProfileApiRoutes.UserRegisteredEvent,
                new UserRegisteredProfileEvent(User, DateTimeOffset.UtcNow, Correlation, Correlation), token);
            Check(created.IsSuccessStatusCode);
            using var changed = await http.PutAsJsonAsync(ProfileApiRoutes.CurrentProfilePreferences,
                new UpdateUserPreferencesRequest("en-US", "UTC", "USD", "strict", false, MonthlyBudgetAmount: 2000m,
                    ProfileOnboardingCompleted: true, PreferencesOnboardingCompleted: true), token);
            Check(changed.IsSuccessStatusCode);
        }
        using (var current = await http.GetAsync(ProfileApiRoutes.CurrentProfile, token)) { Check(current.IsSuccessStatusCode); }
        var services = new Dictionary<string, ServiceProvider>(StringComparer.Ordinal);
        var workers = new Dictionary<string, IHostedService[]>(StringComparer.Ordinal);
        try
        {
            foreach (var role in Roles.Where(role => role != "profile"))
            {
                Stage = "native dependency graph " + role;
                var collection = new ServiceCollection(); collection.AddLogging(logging => logging.ClearProviders());
                var configuration = Configuration(credentials, role, port, address);
                switch (role)
                {
                    case "transaction-intake": collection.AddTransactionIntakeApplication().AddTransactionIntakeInfrastructure(configuration, true); break;
                    case "income": collection.AddIncomeApplication().AddIncomeInfrastructure(configuration, true); break;
                    case "expense": collection.AddExpenseApplication().AddExpenseInfrastructure(configuration, true); break;
                    case "analytics": collection.AddAnalyticsApplication().AddAnalyticsInfrastructure(configuration, true); break;
                    case "financial-score": collection.AddFinancialScoreApplication().AddFinancialScoreInfrastructure(configuration, true); break;
                    case "recommendations-notifications": collection.AddRecommendationNotificationApplication().AddRecommendationNotificationInfrastructure(configuration, true); break;
                    case "audit": collection.AddAuditInfrastructure(configuration, true); break;
                }
                var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
                services.Add(role, provider); workers.Add(role, provider.GetServices<IHostedService>().ToArray());
            }
            var insights = services["recommendations-notifications"];
            if (mode == "initial")
            {
                // This is an explicit synthetic user preference command, not a projector or provider invocation.
                await insights.GetRequiredService<NativeInsightProfileSettingsLoader>().RefreshAsync(Owner, "USD", token);
                await insights.GetRequiredService<PostgresNotificationPreferenceProvider>().UpdateAsync(Owner, new(false, true), token);
            }
            foreach (var role in workers.Keys) { await StartAsync(workers[role], token); }
            var intake = services["transaction-intake"];
            if (mode == "initial")
            {
                Stage = "initial automatic financial convergence";
                var firstIncome = await ConfirmAsync(intake, "initial-income", "Received $1000 salary today", token);
                await ConfirmAsync(intake, "initial-expense", "Paid $100 for food today", token);
                await WaitAsync(() => ConvergedAsync(services, 1000m, 100m, 2, token), token);
                Stage = "durable broker backlog before process loss";
                foreach (var role in new[] { "analytics", "financial-score", "recommendations-notifications", "audit" })
                { await StopAsync(workers[role], token); workers[role] = []; }
                await ConfirmAsync(intake, "queued-expense", "Paid $13.37 for food today", token);
                await WaitAsync(async () => (await services["expense"].GetRequiredService<PostgresExpenseRecordStore>()
                    .ListAsync(User, DateOnly.MinValue, DateOnly.MaxValue, true, token)).Count == 2 &&
                    (await services["expense"].GetRequiredService<PostgresExpenseRecordEventPublisher>().ReadPendingAsync(token)).Count == 0, token);
                Stage = "durable Intake outbox before process loss";
                await StopAsync(workers["transaction-intake"], token); workers["transaction-intake"] = [];
                var queued = await ConfirmAsync(intake, "queued-income", "Received $250 salary today", token);
                // The legacy confirmation flag means durable outbox admission, not broker acknowledgement.
                Check((await intake.GetRequiredService<ITransactionConfirmationStore>().GetAsync(User, queued, token)) is { Published: true });
                Check((await intake.GetRequiredService<PostgresIntakeOutbox>().ReadPendingAsync(token))
                    .Any(message => message.Kind == "confirmed" && message.Confirmation?.DraftId == queued));
                WriteNew(root, "financial-scenario.json", new Scenario(firstIncome, queued));
                WriteNew(root, "financial-initial.json", new
                {
                    passed = true,
                    syntheticOnly = true,
                    initialConverged = true,
                    durableBrokerBacklog = true,
                    pendingIntakeOutbox = true,
                    installed = false,
                    releaseReady = false
                });
                // Simulate loss of all application processes without graceful outbox/consumer shutdown.
                Environment.Exit(0);
            }
            var state = JsonSerializer.Deserialize<Scenario>(File.ReadAllBytes(Path.Combine(root, "financial-scenario.json")), Json) ?? throw new FixtureFailure();
            Stage = "automatic convergence after PostgreSQL crash and application restart";
            await WaitAsync(() => ConvergedAsync(services, 1250m, 113.37m, 4, token), token);
            Check((await intake.GetRequiredService<ITransactionConfirmationStore>().GetAsync(User, state.QueuedIncomeDraft, token)) is { Published: true });
            await WaitAsync(async () => !(await intake.GetRequiredService<PostgresIntakeOutbox>().ReadPendingAsync(token))
                .Any(message => message.Kind == "confirmed" && message.Confirmation?.DraftId == state.QueuedIncomeDraft), token);
            Stage = "actual broker duplicate delivery";
            var original = (await intake.GetRequiredService<ITransactionConfirmationStore>().GetAsync(User, state.FirstIncomeDraft, token))!.IntegrationEvent;
            var receiver = services["income"].GetRequiredService<NativeRabbitConsumer>();
            var acknowledgements = receiver.AcknowledgedDeliveries;
            await using (var publisher = new NativeRabbitPublisher(new NativeBrokerSettings("transaction-intake", Connection(credentials, "transaction-intake"))))
            {
                await publisher.PublishAsync(new NativePublication(NativePublication.CommandsExchange, "income.transaction-confirmed.v1",
                    TransactionConfirmedIntegrationEvent.Name, original.EventId, original.ConfirmedAtUtc, JsonSerializer.SerializeToUtf8Bytes(original, Json)), token);
            }
            await WaitAsync(() => Task.FromResult(receiver.AcknowledgedDeliveries > acknowledgements), token);
            Check(await ConvergedAsync(services, 1250m, 113.37m, 4, token));
            WriteNew(root, "financial-restart.json", new
            {
                passed = true,
                syntheticOnly = true,
                financialServiceWiringVerified = true,
                postgresConvergenceVerified = true,
                actualReplayAcknowledged = true,
                privateProfileHttpVerified = true,
                manualProjectorCalls = 0,
                providerInvocations = 0,
                installed = false,
                releaseReady = false
            });
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var role in workers.Keys.Reverse()) { await StopAsync(workers[role], cleanup.Token); }
            foreach (var provider in services.Values.Reverse()) { await provider.DisposeAsync(); }
            await app.StopAsync(cleanup.Token);
        }
    }

    private static IConfiguration Configuration(Credentials credentials, string role, int port, string? profileAddress)
    {
        var prefix = Prefixes[role]; var settings = Settings(credentials, role, port);
        var values = new Dictionary<string, string?>
        {
            [prefix + ":Storage:Postgres:Database"] = settings.Database,
            [prefix + ":Storage:Postgres:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [prefix + ":Storage:Postgres:Password"] = settings.Password,
            [prefix + ":Storage:Postgres:IndexKey"] = settings.IndexKey,
            [prefix + ":Storage:Postgres:ActiveEncryptionKeyId"] = "v1",
            [prefix + ":Storage:Postgres:EncryptionKeys:v1"] = settings.EncryptionKeys["v1"]
        };
        if (role != "profile")
        { values[prefix + ":Messaging:Enabled"] = "true"; values[prefix + ":Messaging:ConnectionString"] = Connection(credentials, role); }
        if (credentials.ProfileReaders.TryGetValue(role, out var secret))
        { values[prefix + ":Profile:SharedSecret"] = secret; values[prefix + ":Profile:BaseAddress"] = profileAddress; }
        if (role == "profile")
        {
            values["Profile:Gateway:SharedSecret"] = credentials.GatewaySecret;
            values["Profile:FinancialReaders:Enabled"] = "true";
            foreach (var reader in credentials.ProfileReaders) { values["Profile:FinancialReaders:" + reader.Key + ":SharedSecret"] = reader.Value; }
        }
        if (role == "transaction-intake") { values["TransactionIntake:Storage:SourcePayloadRetentionHours"] = "1"; }
        if (role == "audit")
        {
            values["Audit:AllowedProducers:0"] = "income-service"; values["Audit:AllowedProducers:1"] = "expense-service";
            foreach (var retention in new[] { "security", "regulatory", "operational" }) { values["Audit:RetentionDays:" + retention] = "1"; }
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static async Task<string> ConfirmAsync(ServiceProvider intake, string key, string text, CancellationToken token)
    {
        var draft = await intake.GetRequiredService<ITransactionIntakeService>().CreateDraftAsync(User, key, new TransactionIntakeRequest(text), token);
        await intake.GetRequiredService<ITransactionConfirmationService>().ConfirmAsync(User, draft.Draft.Id, Correlation, token);
        return draft.Draft.Id;
    }

    private static async Task<bool> ConvergedAsync(Dictionary<string, ServiceProvider> services, decimal income, decimal expense, int count, CancellationToken token)
    {
        var incomes = await services["income"].GetRequiredService<PostgresIncomeRecordStore>().ListAsync(User, DateOnly.MinValue, DateOnly.MaxValue, true, token);
        var expenses = await services["expense"].GetRequiredService<PostgresExpenseRecordStore>().ListAsync(User, DateOnly.MinValue, DateOnly.MaxValue, true, token);
        if (incomes.Sum(r => r.Amount) != income || expenses.Sum(r => r.Amount) != expense || incomes.Count + expenses.Count != count) { return false; }
        var analytics = await services["analytics"].GetRequiredService<PostgresAnalyticsReadModelStore>().GetAsync(Owner, "USD", token);
        if (analytics.DailyTotals.Values.Sum(t => t.Income) != income || analytics.DailyTotals.Values.Sum(t => t.Expense) != expense) { return false; }
        var scores = services["financial-score"].GetRequiredService<PostgresFinancialScoreStore>();
        var scoreFacts = await scores.GetSnapshotAsync(Owner, "USD", token); var score = await scores.GetCurrentAsync(Owner, "USD", token);
        if (scoreFacts.Records.Count != count || score is null || score.Score is < 0 or > 100) { return false; }
        var insights = services["recommendations-notifications"].GetRequiredService<PostgresRecommendationNotificationStore>();
        var snapshot = await insights.GetSnapshotAsync(Owner, "USD", token);
        if (snapshot?.Analytics?.MonthlyIncomeTotal != income || snapshot.Analytics.MonthlyExpenseTotal != expense ||
            snapshot.Score?.Score != score.Score || snapshot.Score.CalculatedAtUtc != score.CalculatedAtUtc) { return false; }
        var recommendations = await insights.GetRecommendationsAsync(Owner, "USD", token);
        var notifications = await insights.GetNotificationsAsync(Owner, "USD", token);
        Check(notifications.All(n => n.Channel == "web" && n.DeliveryStatus == NotificationDeliveryStatuses.Prepared));
        if (!recommendations.Any(r => r.Status == RecommendationStatuses.Active) || notifications.Count == 0) { return false; }
        return (await services["audit"].GetRequiredService<AuditEventService>().FindByCorrelationAsync(Correlation, token)).Count == count;
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, CancellationToken token)
    { while (!await condition()) { await Task.Delay(200, token); } }
    private static async Task StartAsync(IHostedService[] workers, CancellationToken token)
    { foreach (var worker in workers) { await worker.StartAsync(token); } }
    private static async Task StopAsync(IHostedService[] workers, CancellationToken token)
    { foreach (var worker in workers.Reverse()) { await worker.StopAsync(token); } }
    private sealed record Scenario(string FirstIncomeDraft, string QueuedIncomeDraft);
}
