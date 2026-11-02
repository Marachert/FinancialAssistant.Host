using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinancialAssistant.Profile.Client;
using FinancialAssistant.Profile.Contracts;
using Xunit;

namespace FinancialAssistant.Profile.Tests;

public sealed class NativeProfileSettingsClientTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Owner = new('a', 64);
    private static readonly string Secret = new('s', 48);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static ProfileFinancialSettingsSnapshot Snapshot() => new(Owner, "USD", 425.25m, "strict", false,
        false, false, true, true, "profile-7", 7, At);
    private static HttpResponseMessage Response(ProfileFinancialSettingsSnapshot? value = null) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(value ?? Snapshot(), options: Json) };
    private static NativeProfileSettingsClient Client(Handler handler) => new("financial-score", "http://127.0.0.1:5102", Secret, new Clock(), handler);

    [Fact]
    public async Task ScopedLoopbackReadUsesHashedBodyAndReturnsAuthoritativeSettings()
    {
        var handler = new Handler(async (request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("127.0.0.1", request.RequestUri!.Host);
            Assert.Equal(ProfileFinancialSettingsApi.Path, request.RequestUri.AbsolutePath);
            Assert.DoesNotContain(Owner, request.RequestUri.ToString());
            Assert.Equal(Secret, Assert.Single(request.Headers.GetValues(ProfileFinancialSettingsApi.AuthenticationHeader)));
            Assert.Equal("financial-score", Assert.Single(request.Headers.GetValues(ProfileFinancialSettingsApi.ReaderHeader)));
            var query = await request.Content!.ReadFromJsonAsync<ProfileFinancialSettingsQuery>(Json);
            Assert.Equal(Owner, query!.OwnerHash); return Response();
        });
        using var client = Client(handler); var settings = await client.ReadAsync(Owner);
        Assert.Equal(Snapshot(), settings); Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(Secret, client.ToString()); Assert.DoesNotContain(Secret, JsonSerializer.Serialize(client));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://127.0.0.1:5102/path")]
    [InlineData("http://user:secret@127.0.0.1:5102")]
    [InlineData("http://127.0.0.1:5102/?query=private")]
    [InlineData("file:///local")]
    public void NonlocalCredentialBearingAndAmbiguousEndpointsAreRejectedBeforeIo(string endpoint)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response()));
        var failure = Assert.Throws<ArgumentException>(() => new NativeProfileSettingsClient("financial-score", endpoint, Secret, new Clock(), handler));
        Assert.Equal("Native Profile reader configuration is invalid.", failure.Message);
        Assert.Equal(0, handler.Calls); Assert.True(handler.Disposed);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task RedirectsAndUnavailableSourcesNeverBecomeDefaultBudgets(int status)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent("synthetic-private-response"), Headers = { Location = new Uri("https://example.com") } }));
        using var client = Client(handler);
        var failure = await Assert.ThrowsAsync<ProfileSettingsUnavailableException>(() => client.ReadAsync(Owner));
        Assert.Equal(1, handler.Calls); Assert.Null(failure.InnerException);
        Assert.DoesNotContain("synthetic-private-response", failure.ToString()); Assert.DoesNotContain(Secret, failure.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ForeignStaleFutureConflictingAndMissingSourceFieldsAreRejected(int change)
    {
        var value = Snapshot();
        value = change switch
        {
            0 => value with { OwnerHash = new string('b', 64) },
            1 => value with { ObservedAtUtc = At.AddHours(-3) },
            2 => value with { ObservedAtUtc = At.AddSeconds(1) },
            3 => value with { SourceVersion = "profile-8" },
            4 => value with { MonthlyBudgetAmount = -1m },
            _ => value
        };
        var json = JsonSerializer.SerializeToNode(value, Json)!.AsObject();
        if (change == 5) { json.Remove("monthlyBudgetAmount"); }
        using var client = Client(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json") })));
        await Assert.ThrowsAsync<ProfileSettingsUnavailableException>(() => client.ReadAsync(Owner));
    }

    [Fact]
    public async Task DuplicateFieldsAndOversizedRepliesAreNotSilentlyNormalized()
    {
        var json = JsonSerializer.Serialize(Snapshot(), Json);
        foreach (var body in new[] { json.Insert(1, "\"monthlyBudgetAmount\":0,"), new string('x', 16385) })
        {
            using var client = Client(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") })));
            await Assert.ThrowsAsync<ProfileSettingsUnavailableException>(() => client.ReadAsync(Owner));
        }
    }

    [Fact]
    public async Task CallerShutdownAndUnrelatedDeadlinesRemainDistinctAndSanitized()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = Client(new Handler(async (_, token) =>
        { cancellation.Cancel(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return Response(); }));
        var stopped = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ReadAsync(Owner, cancellation.Token));
        Assert.Equal(cancellation.Token, stopped.CancellationToken);
        using var timeout = Client(new Handler((_, _) => throw new OperationCanceledException("synthetic-private-deadline")));
        var failure = await Assert.ThrowsAsync<ProfileSettingsUnavailableException>(() => timeout.ReadAsync(Owner));
        Assert.Null(failure.InnerException); Assert.DoesNotContain("synthetic-private-deadline", failure.ToString());
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => At; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        internal bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return send(request, token); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
