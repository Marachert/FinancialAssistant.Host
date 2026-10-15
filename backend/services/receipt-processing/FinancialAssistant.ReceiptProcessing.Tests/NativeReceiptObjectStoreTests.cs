using System.Net;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Infrastructure;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.Shared.Hosting;
using FinancialAssistant.Shared.NativeFiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class NativeReceiptObjectStoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void RetentionMustBeExplicitAndBounded(int hours) => Assert.Throws<InvalidOperationException>(() => Options("synthetic", "synthetic", hours).Validate());

    [Fact]
    public async Task RoundTripRestartImmutableReplayAndDurableDeletion()
    {
        if (!NativeAvailable()) { return; }
        using var fixture = new Root();
        var options = fixture.Options();
        var clock = new Clock();
        using (var store = new NativeReceiptObjectStore(options, clock))
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.StoreAsync("receipt_roundtrip", "synthetic-image"u8.ToArray(), default)));
            Assert.Equal("synthetic-image", await Read(store, "receipt_roundtrip"));
            await Assert.ThrowsAsync<NativeReceiptStorageException>(() => store.StoreAsync("receipt_roundtrip", "different-image"u8.ToArray(), default));
        }
        using (var restarted = new NativeReceiptObjectStore(options, clock))
        {
            Assert.Equal("synthetic-image", await Read(restarted, "receipt_roundtrip"));
            var info = await restarted.GetInfoAsync("receipt_roundtrip");
            Assert.NotNull(info); Assert.Equal(15, info.SizeBytes);
            await restarted.DeleteAsync("receipt_roundtrip");
        }
        using var deleted = new NativeReceiptObjectStore(options, clock);
        Assert.Null(await deleted.OpenReadAsync("receipt_roundtrip", default));
        Assert.True((await deleted.GetInfoAsync("receipt_roundtrip"))!.DeletedOrExpired);
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => deleted.StoreAsync("receipt_roundtrip", "synthetic-image"u8.ToArray(), default));
    }

    [Fact]
    public async Task EncryptedPortableRestoreCannotOverrideExistingDeletion()
    {
        if (!NativeAvailable()) { return; }
        using var sourceRoot = new Root(); using var destinationRoot = new Root();
        var clock = new Clock();
        using var source = new NativeReceiptObjectStore(sourceRoot.Options(), clock);
        await source.StoreAsync("receipt_restore", "synthetic-image"u8.ToArray(), default);
        var backup = await source.ExportEncryptedAsync("receipt_restore");
        Assert.DoesNotContain("synthetic-image", System.Text.Encoding.UTF8.GetString(backup));
        using var destination = new NativeReceiptObjectStore(destinationRoot.Options(), clock);
        await destination.RestoreEncryptedAsync("receipt_restore", backup);
        Assert.Equal("synthetic-image", await Read(destination, "receipt_restore"));
        await destination.DeleteAsync("receipt_restore");
        await destination.RestoreEncryptedAsync("receipt_restore", backup);
        Assert.Null(await destination.OpenReadAsync("receipt_restore", default));
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => destination.RestoreEncryptedAsync("receipt_other", backup));
    }

    [Fact]
    public async Task ExpiryCleanupAndConfigurationIncreaseNeverRestoreSource()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        byte[] backup;
        using (var store = new NativeReceiptObjectStore(root.Options(1), clock))
        {
            await store.StoreAsync("receipt_expiry", "synthetic-image"u8.ToArray(), default);
            backup = await store.ExportEncryptedAsync("receipt_expiry");
        }
        clock.Now = clock.Now.AddHours(1);
        using var restarted = new NativeReceiptObjectStore(root.Options(24), clock);
        Assert.Null(await restarted.OpenReadAsync("receipt_expiry", default));
        Assert.Equal(1, await restarted.CleanupBatchAsync());
        Assert.Equal(0, await restarted.CleanupBatchAsync());
        await restarted.RestoreEncryptedAsync("receipt_expiry", backup);
        Assert.Null(await restarted.OpenReadAsync("receipt_expiry", default));
        var tombstone = await restarted.ExportEncryptedAsync("receipt_expiry");
        Assert.True(tombstone.Length < backup.Length);
    }

    [Fact]
    public async Task LoweredRootRetentionSurvivesRestartWithoutReexposure()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using (var original = new NativeReceiptObjectStore(root.Options(24), clock))
        { await original.StoreAsync("receipt_policy", new byte[] { 1 }, default); }
        clock.Now = clock.Now.AddMinutes(30);
        using (var shorter = new NativeReceiptObjectStore(root.Options(1), clock))
        { Assert.NotNull(await shorter.GetInfoAsync("receipt_policy")); }
        clock.Now = clock.Now.AddHours(1);
        using var raised = new NativeReceiptObjectStore(root.Options(24), clock);
        Assert.Null(await raised.OpenReadAsync("receipt_policy", default));
    }

    [Fact]
    public async Task EncryptedExportCarriesTheShortenedRootPolicyToFreshRestore()
    {
        if (!NativeAvailable()) { return; }
        using var source = new Root(); using var destination = new Root(); var clock = new Clock();
        using (var original = new NativeReceiptObjectStore(source.Options(24), clock))
        { await original.StoreAsync("receipt_short_export", new byte[] { 1 }, default); }
        clock.Now = clock.Now.AddMinutes(30);
        byte[] backup;
        using (var shorter = new NativeReceiptObjectStore(source.Options(1), clock))
        { backup = await shorter.ExportEncryptedAsync("receipt_short_export"); }
        using var restored = new NativeReceiptObjectStore(destination.Options(24), clock);
        await restored.RestoreEncryptedAsync("receipt_short_export", backup);
        clock.Now = clock.Now.AddHours(1);
        Assert.Null(await restored.OpenReadAsync("receipt_short_export", default));
    }

    [Fact]
    public void RootRetentionMarkerIsAuthenticatedAgainstPolicyTampering()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using (var initial = new NativeReceiptObjectStore(root.Options(), clock)) { }
        var markerPath = Assert.Single(Directory.GetFiles(root.Path, "*.object"));
        var marker = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(markerPath))!.AsObject();
        marker["RetentionHours"] = 24; File.WriteAllText(markerPath, marker.ToJsonString());
        Assert.Throws<NativeReceiptStorageException>(() => new NativeReceiptObjectStore(root.Options(24), clock));
    }

    [Fact]
    public async Task KeyIdentityRotationAndMissingHistoricalKeysFailClosed()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); var clock = new Clock();
        using (var first = new NativeReceiptObjectStore(root.Options(), clock))
        { await first.StoreAsync("receipt_key", "synthetic-image"u8.ToArray(), default); }
        var rotated = root.Options(active: "v2");
        using (var second = new NativeReceiptObjectStore(rotated, clock))
        {
            Assert.Equal("synthetic-image", await Read(second, "receipt_key"));
            await second.StoreAsync("receipt_new_key", "synthetic-image"u8.ToArray(), default);
        }
        Assert.Throws<NativeReceiptStorageException>(() => new NativeReceiptObjectStore(root.Options(index: 4), clock));
        Assert.Throws<NativeReceiptStorageException>(() => new NativeReceiptObjectStore(root.Options(), clock));
        using var third = new NativeReceiptObjectStore(rotated, clock);
        Assert.Equal("synthetic-image", await Read(third, "receipt_new_key"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("receipt/escape")]
    [InlineData("receipt\\escape")]
    [InlineData("receipt:stream")]
    [InlineData("receipt\0bad")]
    public async Task InvalidObjectIdentityCannotReachFileSystem(string id)
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); using var store = new NativeReceiptObjectStore(root.Options(), new Clock());
        await Assert.ThrowsAsync<ArgumentException>(() => store.StoreAsync(id, new byte[] { 1 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync(id, default));
    }

    [Fact]
    public async Task CorruptCiphertextIsNotReturnedAndFileNamesRevealNoReceiptId()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); using var store = new NativeReceiptObjectStore(root.Options(), new Clock());
        await store.StoreAsync("receipt_private_reference", "synthetic-image"u8.ToArray(), default);
        var paths = Directory.GetFiles(root.Path, "*.object");
        Assert.Equal(2, paths.Length);
        Assert.All(paths, path => Assert.DoesNotContain("receipt_private_reference", path));
        var payload = Assert.Single(paths, path => File.ReadAllBytes(path)[0] == 1);
        var bytes = File.ReadAllBytes(payload); bytes[^1] ^= 1; File.WriteAllBytes(payload, bytes);
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => store.OpenReadAsync("receipt_private_reference", default));
    }

    [Fact]
    public void RootMustAlreadyBeProtectedAndCannotHaveTwoWriters()
    {
        if (!OperatingSystem.IsWindows()) { Assert.False(NativeAvailable()); return; }
        using var root = new Root();
        using (var first = new NativeReceiptObjectStore(root.Options(), new Clock()))
        { Assert.Throws<InvalidOperationException>(() => new NativeReceiptObjectStore(root.Options(), new Clock())); }
        var security = new DirectoryInfo(root.Path).GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(root.Path).SetAccessControl(security);
        Assert.Throws<InvalidOperationException>(() => new NativeReceiptObjectStore(root.Options(), new Clock()));
    }

    [Fact]
    public void MissingNativeConfigurationCannotChooseMemory()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddReceiptProcessingInfrastructure(windowsNative: true));
        Assert.Contains("ReceiptProcessing:Storage:Files:IndexKey", NativeServiceCatalog.Get("receipt-processing").RequiredSecretKeys);
        using var development = new ServiceCollection().AddReceiptProcessingInfrastructure().BuildServiceProvider();
        Assert.IsType<EncryptedInMemoryReceiptObjectStore>(development.GetRequiredService<IReceiptObjectStore>());
    }

    [Fact]
    public async Task NativeMetadataCannotAcceptProcessLocalWorkflow()
    {
        var configuration = Configuration(Options("synthetic", "synthetic", 1));
        await using var provider = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration)
            .AddReceiptProcessingApplication().AddReceiptProcessingInfrastructure(configuration, true).BuildServiceProvider();
        Assert.Null(provider.GetService<InMemoryReceiptMetadataStore>());
        Assert.Null(provider.GetService<EncryptedInMemoryReceiptObjectStore>());
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => provider.GetRequiredService<IReceiptMetadataStore>()
            .GetByIdempotencyKeyAsync("synthetic-owner", "synthetic-key", default));
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => provider.GetRequiredService<IOcrProcessingStore>()
            .GetAsync("synthetic-owner", "receipt_synthetic", default));
    }

    [Fact]
    public async Task WindowsNativeEndpointFails503NotSuccessfulMemoryMetadata()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(FinancialAssistantHost.NativeEnvironment);
            foreach (var pair in Configuration(root.Options()).AsEnumerable())
            { if (pair.Value is not null) { builder.UseSetting(pair.Key, pair.Value); } }
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddConfiguration(Configuration(root.Options())));
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/receipts/receipt_synthetic_native");
        request.Headers.Add(ReceiptProcessingHeaders.GatewayAuthentication, ReceiptProcessingWebApplicationFactory.GatewaySecret);
        request.Headers.Add(ReceiptProcessingHeaders.GatewayUserId, "synthetic-owner");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("receipt_storage_unavailable", problem.GetProperty("code").GetString());
        using var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
    }

    [Fact]
    public async Task CancellationAndCapacityDoNotCreatePartialOrResurrectableObjects()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root();
        using var store = new NativeReceiptObjectStore(Options(root.Path, root.Options().ServiceSid, 1, maximum: 1), new Clock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.StoreAsync("receipt_cancelled", new byte[] { 1 }, new CancellationToken(true)));
        Assert.Null(await store.GetInfoAsync("receipt_cancelled"));
        await store.StoreAsync("receipt_capacity", new byte[] { 1 }, default);
        await store.DeleteAsync("receipt_capacity");
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => store.StoreAsync("receipt_overflow", new byte[] { 1 }, default));
        Assert.Empty(Directory.GetFiles(root.Path, "*.tmp"));
    }

    [Fact]
    public async Task HardLinkedObjectsAreRejectedInsteadOfReadingAliasedContent()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root(); using var store = new NativeReceiptObjectStore(root.Options(), new Clock());
        await store.StoreAsync("receipt_hardlink", new byte[] { 1 }, default);
        var original = Assert.Single(Directory.GetFiles(root.Path, "*.object"), path => File.ReadAllBytes(path)[0] == 1);
        Assert.True(CreateHardLinkW(System.IO.Path.Combine(root.Path, new string('A', 64) + ".object"), original, IntPtr.Zero));
        await Assert.ThrowsAsync<NativeReceiptStorageException>(() => store.OpenReadAsync("receipt_hardlink", default));
    }

    [Fact]
    public void NativeRootCannotContainApplicationFiles()
    {
        if (!OperatingSystem.IsWindows()) { Assert.False(NativeAvailable()); return; }
        using var identity = WindowsIdentity.GetCurrent();
        var serviceSid = identity.User!.Value;
        Assert.Throws<ArgumentException>(() => new ProtectedDirectory(AppContext.BaseDirectory, serviceSid));
        var parent = Directory.GetParent(System.IO.Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.FullName;
        Assert.Throws<ArgumentException>(() => new ProtectedDirectory(parent, serviceSid));
    }

    [Fact]
    public void UnknownRootContentsAreRejectedBeforeCreatingWriterLease()
    {
        if (!NativeAvailable()) { return; }
        using var root = new Root();
        var canary = System.IO.Path.Combine(root.Path, "unrelated.txt"); File.WriteAllText(canary, "synthetic-unrelated-data");
        Assert.Throws<InvalidOperationException>(() => new NativeReceiptObjectStore(root.Options(), new Clock()));
        Assert.False(File.Exists(System.IO.Path.Combine(root.Path, ".lease")));
        Assert.Equal("synthetic-unrelated-data", File.ReadAllText(canary));
    }

    private static IConfiguration Configuration(NativeReceiptObjectOptions options) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ReceiptProcessing:Storage:Files:RootPath"] = options.RootPath,
        ["ReceiptProcessing:Storage:Files:ServiceSid"] = options.ServiceSid,
        ["ReceiptProcessing:Storage:Files:RetentionHours"] = options.RetentionHours.ToString(),
        ["ReceiptProcessing:Storage:Files:IndexKey"] = options.IndexKey,
        ["ReceiptProcessing:Storage:Files:ActiveEncryptionKeyId"] = options.ActiveEncryptionKeyId,
        ["ReceiptProcessing:Storage:Files:EncryptionKeys:v1"] = options.EncryptionKeys["v1"],
        ["ReceiptProcessing:Gateway:SharedSecret"] = ReceiptProcessingWebApplicationFactory.GatewaySecret,
        ["ReceiptProcessing:Events:SharedSecret"] = "synthetic-receipt-event-secret-for-native-tests",
        ["ReceiptProcessing:Ocr:Enabled"] = "false",
        ["ReceiptProcessing:TransactionIntake:BaseAddress"] = "http://127.0.0.1:5104"
    }).Build();

    private static bool NativeAvailable()
    {
        if (OperatingSystem.IsWindows()) { return true; }
        Assert.Throws<PlatformNotSupportedException>(() => new NativeReceiptObjectStore(Options("synthetic", "synthetic", 1), new Clock()));
        return false;
    }
    private static async Task<string> Read(NativeReceiptObjectStore store, string id)
    {
        await using var stream = await store.OpenReadAsync(id, default); Assert.NotNull(stream);
        using var reader = new StreamReader(stream); return await reader.ReadToEndAsync();
    }
    private static NativeReceiptObjectOptions Options(string path, string sid, int hours, string active = "v1", byte index = 1, int maximum = 1000) => new()
    {
        RootPath = path,
        ServiceSid = sid,
        RetentionHours = hours,
        MaximumObjects = maximum,
        IndexKey = Convert.ToBase64String(Enumerable.Repeat(index, 32).ToArray()),
        ActiveEncryptionKeyId = active,
        EncryptionKeys = active == "v1" ? new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
            : new() { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()), ["v2"] = Convert.ToBase64String(Enumerable.Repeat((byte)3, 32).ToArray()) }
    };
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Root : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fa-native-test-" + Guid.NewGuid().ToString("N"));
        private readonly string sid;
        public Root()
        {
            if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException(); }
            using var identity = WindowsIdentity.GetCurrent(); sid = identity.User!.Value;
            var security = new DirectorySecurity(); security.SetOwner(identity.User); security.SetAccessRuleProtection(true, false);
            foreach (var value in new[] { sid, "S-1-5-18", "S-1-5-32-544" }.Distinct())
            {
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(value), FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            }
            new DirectoryInfo(Path).Create(security);
        }
        public NativeReceiptObjectOptions Options(int hours = 1, string active = "v1", byte index = 1) => NativeReceiptObjectStoreTests.Options(Path, sid, hours, active, index);
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            if (!resolved.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(resolved).StartsWith("fa-native-test-", StringComparison.Ordinal)) { throw new IOException("Unsafe fixture cleanup."); }
            Directory.Delete(resolved, recursive: true);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);
}
