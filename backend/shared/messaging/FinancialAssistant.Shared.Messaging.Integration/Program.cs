using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

return await BrokerFixture.MainAsync(args);

internal static class BrokerFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly NativeBinding Income = NativeBinding.Event("income.created.v1", "income");
    private static readonly NativeBinding Unroutable = NativeBinding.Event("income.deleted.v1", "income");
    private static readonly NativeBinding Command = NativeBinding.Command("income", "transaction-confirmed", "transaction.confirmed.v1", "transaction-intake");
    private static readonly NativeSubscription Analytics = new("analytics", "financial", new[] { Income });
    private static readonly NativeSubscription Summary = new("financial-summary", "financial", new[] { Income });
    private static readonly NativeSubscription Confirmed = new("income", "confirmed", new[] { Command });
    private static string stage = "arguments";

    internal static async Task<int> MainAsync(string[] args)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("FA_NATIVE_BROKER_SYNTHETIC_ONLY") != "1" || args.Length is < 2 or > 3)
            { throw new FixtureFailure(); }
            var root = OwnedRoot(args[1]);
            if (args[0] == "prepare")
            {
                stage = "prepare";
                if (args.Length != 3 || !int.TryParse(args[2], out var port) || port is < 1024 or > 65535) { throw new FixtureFailure(); }
                Prepare(root, port);
            }
            else
            {
                if (args.Length != 2 || args[0] is not ("initial" or "restart" or "reconnect")) { throw new FixtureFailure(); }
                var credentials = JsonSerializer.Deserialize<Credentials>(File.ReadAllBytes(Path.Combine(root, "credentials.json")), Json) ?? throw new FixtureFailure();
                Check(credentials.SyntheticOnly && credentials.RunId == Path.GetFileName(root) && credentials.Port is >= 1024 and <= 65535 &&
                    credentials.Vhost == "fa-synthetic-" + credentials.RunId, "credentials");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(9));
                if (args[0] == "reconnect") { await VerifyReconnectAsync(root, credentials, timeout.Token); }
                else { await VerifyAsync(root, credentials, args[0], timeout.Token); }
            }
            Console.WriteLine("Native broker synthetic fixture passed: " + args[0]);
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Native broker synthetic fixture failed at gate: " + stage);
            return 1;
        }
    }

    private static string OwnedRoot(string input)
    {
        var path = Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar);
        var directory = new DirectoryInfo(path);
        if (!directory.Exists || directory.Name.Length != 32 || !Guid.TryParseExact(directory.Name, "N", out _) ||
            directory.Parent?.Name != "broker-proof" || directory.Parent.Parent?.Name != ".codex-runtime") { throw new FixtureFailure(); }
        for (var cursor = directory; cursor is not null; cursor = cursor.Parent)
        { if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0) { throw new FixtureFailure(); } }
        return path;
    }

    private static void Prepare(string root, int port)
    {
        var run = Path.GetFileName(root); var vhost = "fa-synthetic-" + run;
        var topology = new NativeTopology(vhost, new[] { Analytics, Summary, Confirmed }, new[] { Income, Unroutable, Command });
        var passwords = topology.BrokerUsers.ToDictionary(user => user, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), StringComparer.Ordinal);
        var observerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var definitions = JsonNode.Parse(topology.CreateDefinitions(passwords))!.AsObject();
        // A read-only observer exists only in this isolated fixture, never in the
        // application's topology. It can inspect DLQs without granting peers access.
        var observerBinding = NativeBinding.Event("fixture.observed.v1", "fixture-observer");
        var observerTopology = new NativeTopology(vhost, new[] { new NativeSubscription("fixture-observer", "observed", new[] { observerBinding }) }, new[] { observerBinding });
        var observerDefinitions = JsonNode.Parse(observerTopology.CreateDefinitions(new Dictionary<string, string> { ["fa-fixture-observer"] = observerPassword }))!;
        definitions["users"]!.AsArray().Add(observerDefinitions["users"]![0]!.DeepClone());
        definitions["permissions"]!.AsArray().Add(new JsonObject
        {
            ["user"] = "fa-fixture-observer",
            ["vhost"] = vhost,
            ["configure"] = "^$",
            ["write"] = "^$",
            ["read"] = "^(?:" + string.Join("|", new[] { Analytics, Summary, Confirmed }.SelectMany(s => new[] { s.Queue, s.DeadLetterQueue }).Select(System.Text.RegularExpressions.Regex.Escape)) + ")$"
        });
        var credentials = new Credentials { SyntheticOnly = true, RunId = run, Vhost = vhost, Port = port, Passwords = passwords, ObserverPassword = observerPassword };
        WriteNew(root, "credentials.json", JsonSerializer.SerializeToUtf8Bytes(credentials, Json));
        WriteNew(root, "definitions.json", JsonSerializer.SerializeToUtf8Bytes(definitions));
    }

    private static async Task VerifyAsync(string root, Credentials credentials, string mode, CancellationToken token)
    {
        stage = "broker-connect";
        await using var observer = await Factory(credentials, "fixture-observer").CreateConnectionAsync(token);
        await using var inspection = await observer.CreateChannelAsync(cancellationToken: token);
        Check(observer.IsOpen, "broker-connect");
        var received = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        var attempts = new ConcurrentQueue<int>();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        var analytics = new NativeRabbitConsumer(Settings(credentials, "analytics"), Analytics);
        var summary = new NativeRabbitConsumer(Settings(credentials, "financial-summary"), Summary);
        var confirmed = new NativeRabbitConsumer(Settings(credentials, "income"), Confirmed);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new List<Task>();
        Task Handle(string owner, NativeDelivery delivery, CancellationToken ct)
        {
            var payload = delivery.Deserialize<Payload>();
            delivery.EnsureWireIdentity(payload.EventId, payload.EventType, payload.PublishedAtUtc);
            Check(delivery.SourceSequence == 7, "private-source-sequence-preserved");
            if (payload.Value != 7) { throw new NativeDeliveryRejectedException(); }
            Check(delivery.PublisherUser == (owner == "income" ? "fa-transaction-intake" : "fa-income"), "broker-provenance");
            if (owner == "analytics" && delivery.EventId == "synthetic-retry" && delivery.Attempt == 0) { throw new IOException(); }
            if (owner == "analytics" && delivery.EventId == "synthetic-retry") { Check(delivery.Attempt == 1, "retry-attempt"); }
            if (owner == "analytics" && delivery.EventId == "synthetic-exhaust") { attempts.Enqueue(delivery.Attempt); throw new IOException(); }
            if (owner == "analytics" && delivery.EventId == "synthetic-unacked")
            { held.TrySetResult(); return Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            received.AddOrUpdate(owner + ":" + delivery.EventId, 1, (_, count) => count + 1);
            return Task.CompletedTask;
        }
        try
        {
            tasks.Add(analytics.RunAsync((delivery, ct) => Handle("analytics", delivery, ct), stopping.Token));
            tasks.Add(summary.RunAsync((delivery, ct) => Handle("financial-summary", delivery, ct), stopping.Token));
            tasks.Add(confirmed.RunAsync((delivery, ct) => Handle("income", delivery, ct), stopping.Token));
            await UntilAsync(() => analytics.IsConnected && summary.IsConnected && confirmed.IsConnected, token);
            await using var publisher = new NativeRabbitPublisher(Settings(credentials, "income"));
            if (mode == "restart")
            {
                stage = "crash-restart-persistence";
                await UntilAsync(() => received.GetValueOrDefault("analytics:synthetic-persist") == 1 &&
                    received.GetValueOrDefault("financial-summary:synthetic-persist") == 1 &&
                    analytics.AcknowledgedDeliveries == 1 && summary.AcknowledgedDeliveries == 1, token);
            }
            else
            {
                stage = "permissions";
                await PermissionsAsync(credentials, token);
                stage = "mandatory-return";
                await FailsAsync<NativeBrokerUnavailableException>(() => publisher.PublishAsync(Publication("synthetic-unroutable", Unroutable), token));
                stage = "owned-command";
                await using (var intake = new NativeRabbitPublisher(Settings(credentials, "transaction-intake")))
                { await intake.PublishAsync(Publication("synthetic-command", Command), token); }
                await UntilAsync(() => received.GetValueOrDefault("income:synthetic-command") == 1, token);
                Check(!received.ContainsKey("analytics:synthetic-command") && !received.ContainsKey("financial-summary:synthetic-command"), "command-isolation");
                stage = "retry-no-peer-fanout";
                await publisher.PublishAsync(Publication("synthetic-retry", Income), token);
                await UntilAsync(() => received.GetValueOrDefault("analytics:synthetic-retry") == 1 &&
                    received.GetValueOrDefault("financial-summary:synthetic-retry") == 1, token);
                stage = "poison-dead-letter";
                await publisher.PublishAsync(Publication("synthetic-invalid", Income, 99), token);
                await DeadLetterAsync(inspection, Analytics, "synthetic-invalid", token);
                await DeadLetterAsync(inspection, Summary, "synthetic-invalid", token);
                stage = "bounded-retry-dead-letter";
                await publisher.PublishAsync(Publication("synthetic-exhaust", Income), token);
                await DeadLetterAsync(inspection, Analytics, "synthetic-exhaust", token, TimeSpan.FromMinutes(7));
                Check(attempts.SequenceEqual(new[] { 0, 1, 2, 3 }), "retry-budget");
                Check(received.GetValueOrDefault("financial-summary:synthetic-exhaust") == 1, "retry-peer-isolation");
                stage = "unacked-cancellation";
                await publisher.PublishAsync(Publication("synthetic-unacked", Income), token);
                await held.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
                await UntilAsync(() => summary.AcknowledgedDeliveries >= 3, token);
            }
        }
        finally
        {
            stopping.Cancel();
            foreach (var task in tasks) { try { await task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None); } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { } }
        }
        if (mode == "initial")
        {
            stage = "unacked-redelivery";
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(token);
            var replayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var replay = new NativeRabbitConsumer(Settings(credentials, "analytics"), Analytics);
            var run = replay.RunAsync((delivery, _) =>
            {
                Check(delivery.EventId == "synthetic-unacked" && delivery.Attempt == 0 && delivery.PublisherUser == "fa-income", "unacked-redelivery");
                replayed.TrySetResult(); return Task.CompletedTask;
            }, drain.Token);
            try
            {
                await replayed.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
                await UntilAsync(() => replay.AcknowledgedDeliveries == 1, token);
            }
            finally { drain.Cancel(); try { await run.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None); } catch (OperationCanceledException) when (drain.IsCancellationRequested) { } }
            stage = "durable-publication-before-crash";
            await using var publisher = new NativeRabbitPublisher(Settings(credentials, "income"));
            await publisher.PublishAsync(Publication("synthetic-persist", Income), token);
            Check((await inspection.QueueDeclarePassiveAsync(Analytics.Queue, token)).MessageCount == 1 &&
                (await inspection.QueueDeclarePassiveAsync(Summary.Queue, token)).MessageCount == 1, "durable-pending");
        }
        WriteNew(root, mode + "-verified.json", JsonSerializer.SerializeToUtf8Bytes(new
        {
            passed = true,
            mode,
            syntheticOnly = true,
            installed = false,
            releaseReady = false,
            financialServiceWiringVerified = false,
            postgresConvergenceVerified = false,
            retryAttempts = attempts.ToArray(),
            privateSequencePreserved = true,
            verifiedAt = DateTimeOffset.UtcNow
        }, Json));
    }

    private static async Task VerifyReconnectAsync(string root, Credentials credentials, CancellationToken token)
    {
        stage = "host-worker-connect";
        var consumer = new NativeRabbitConsumer(Settings(credentials, "analytics"), Analytics);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = new NativeBrokerWorker(ct => consumer.RunAsync((delivery, _) =>
        {
            var payload = delivery.Deserialize<Payload>();
            delivery.EnsureWireIdentity(payload.EventId, payload.EventType, payload.PublishedAtUtc);
            Check(delivery.EventId == "synthetic-reconnect" && delivery.PublisherUser == "fa-income" && payload.Value == 7 && delivery.SourceSequence == 7,
                "host-worker-reconnected-delivery");
            received.TrySetResult(); return Task.CompletedTask;
        }, ct), TimeProvider.System, NullLogger<NativeBrokerWorker>.Instance);
        await worker.StartAsync(token);
        try
        {
            await UntilAsync(() => consumer.IsConnected, token);
            WriteNew(root, "reconnect-ready.json", JsonSerializer.SerializeToUtf8Bytes(new { ready = true, syntheticOnly = true }, Json));
            stage = "host-worker-await-restart";
            await UntilAsync(() => File.Exists(Path.Combine(root, "broker-restarted")), token, TimeSpan.FromMinutes(2));
            await UntilAsync(() => consumer.IsConnected, token);
            stage = "host-worker-reconnected-delivery";
            await using var publisher = new NativeRabbitPublisher(Settings(credentials, "income"));
            await publisher.PublishAsync(Publication("synthetic-reconnect", Income), token);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            await UntilAsync(() => consumer.AcknowledgedDeliveries == 1, token);
            Check(worker.ExecuteTask is { IsCompleted: false }, "host-worker-alive");
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await worker.StopAsync(shutdown.Token);
        }
        Check(worker.ExecuteTask is { IsCompletedSuccessfully: true }, "host-worker-stopped");
        WriteNew(root, "reconnect-verified.json", JsonSerializer.SerializeToUtf8Bytes(new
        {
            passed = true,
            syntheticOnly = true,
            hostWorkerReconnected = true,
            privateSequencePreserved = true,
            installed = false,
            releaseReady = false,
            financialServiceWiringVerified = false,
            postgresConvergenceVerified = false,
            verifiedAt = DateTimeOffset.UtcNow
        }, Json));
    }

    private static async Task PermissionsAsync(Credentials credentials, CancellationToken token)
    {
        await using var income = await Factory(credentials, "income").CreateConnectionAsync(token);
        await DeniedAsync(income, async channel => { await channel.QueueDeclareAsync("fa.synthetic.forbidden", true, false, false, cancellationToken: token); }, token);
        await DeniedAsync(income, async channel => { await channel.BasicGetAsync(Analytics.Queue, true, token); }, token);
        await DeniedAsync(income, async channel =>
        {
            await channel.BasicPublishAsync("fa.events", "expense.created.v1", true,
                new BasicProperties { UserId = "fa-income" }, "{}"u8.ToArray(), token);
        }, token);
        await DeniedAsync(income, async channel =>
        {
            await channel.BasicPublishAsync("fa.events", Income.RoutingKey, true,
                new BasicProperties { UserId = "fa-analytics" }, "{}"u8.ToArray(), token);
        }, token, expectedCode: 406);
        await using var analytics = await Factory(credentials, "analytics").CreateConnectionAsync(token);
        await DeniedAsync(analytics, async channel => { await channel.BasicGetAsync(Summary.Queue, true, token); }, token);
    }

    private static async Task DeniedAsync(IConnection connection, Func<IChannel, Task> action, CancellationToken token, ushort expectedCode = 403)
    {
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), token);
        var closed = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, reason) => { closed.TrySetResult(reason.ReplyCode); return Task.CompletedTask; };
        try { await action(channel); }
        catch (Exception exception) when (exception is OperationInterruptedException or PublishException) { }
        Check(await closed.Task.WaitAsync(TimeSpan.FromSeconds(15), token) == expectedCode, "permission-denied");
    }

    private static async Task DeadLetterAsync(IChannel channel, NativeSubscription subscription, string id, CancellationToken token, TimeSpan? limit = null)
    {
        var until = DateTimeOffset.UtcNow.Add(limit ?? TimeSpan.FromSeconds(20));
        while (DateTimeOffset.UtcNow < until)
        {
            var delivery = await channel.BasicGetAsync(subscription.DeadLetterQueue, false, token);
            if (delivery is not null)
            {
                Check(delivery.BasicProperties.MessageId == id, "owned-dead-letter");
                if (id == "synthetic-exhaust")
                {
                    Check(delivery.BasicProperties.UserId == subscription.BrokerUser &&
                        delivery.BasicProperties.Headers is { } headers && headers.TryGetValue("fa-retry-attempt", out var attempt) && attempt is 3,
                        "dead-letter-retry-provenance");
                }
                await channel.BasicAckAsync(delivery.DeliveryTag, false, token); return;
            }
            await Task.Delay(200, token);
        }
        throw new FixtureFailure();
    }

    private static NativePublication Publication(string id, NativeBinding binding, int value = 7) => new(
        exchange: binding.Exchange, routingKey: binding.RoutingKey, eventType: binding.EventType, eventId: id,
        occurredAtUtc: At, body: JsonSerializer.SerializeToUtf8Bytes(new Payload(id, binding.EventType, At, value), Json), sourceSequence: 7);
    private static NativeBrokerSettings Settings(Credentials credentials, string role) => new(role, Uri(credentials, role));
    private static string Uri(Credentials credentials, string role) => "amqp://fa-" + role + ":" +
        System.Uri.EscapeDataString(role == "fixture-observer" ? credentials.ObserverPassword : credentials.Passwords["fa-" + role]) +
        "@127.0.0.1:" + credentials.Port + "/" + credentials.Vhost;
    private static ConnectionFactory Factory(Credentials credentials, string role) => new()
    { Uri = new System.Uri(Uri(credentials, role)), AutomaticRecoveryEnabled = false, TopologyRecoveryEnabled = false, RequestedHeartbeat = TimeSpan.FromSeconds(10) };
    private static async Task UntilAsync(Func<bool> predicate, CancellationToken token, TimeSpan? limit = null)
    {
        var until = DateTimeOffset.UtcNow.Add(limit ?? TimeSpan.FromSeconds(30));
        while (!predicate()) { if (DateTimeOffset.UtcNow >= until) { throw new FixtureFailure(); } await Task.Delay(100, token); }
    }
    private static async Task FailsAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new FixtureFailure(); }
    private static void Check(bool condition, string gate) { if (!condition) { stage = gate; throw new FixtureFailure(); } }
    private static void WriteNew(string root, string name, byte[] bytes)
    { using var stream = new FileStream(Path.Combine(root, name), FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); }
    private sealed class FixtureFailure : Exception;
    private sealed record Payload([property: JsonRequired] string EventId, [property: JsonRequired] string EventType,
        [property: JsonRequired] DateTimeOffset PublishedAtUtc, [property: JsonRequired] int Value);
    private sealed class Credentials
    {
        public required bool SyntheticOnly { get; init; }
        public required string RunId { get; init; }
        public required string Vhost { get; init; }
        public required int Port { get; init; }
        public required Dictionary<string, string> Passwords { get; init; }
        public required string ObserverPassword { get; init; }
        public override string ToString() => "Synthetic broker credentials (redacted).";
    }
}
