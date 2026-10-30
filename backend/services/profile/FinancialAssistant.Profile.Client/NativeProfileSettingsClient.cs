using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Profile.Contracts;

namespace FinancialAssistant.Profile.Client;

public sealed class ProfileSettingsUnavailableException() : Exception("Authorized Profile settings are unavailable.");

public sealed class NativeProfileSettingsClient : IDisposable
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 4 };
    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly string reader;
    private readonly string secret;
    private readonly TimeProvider clock;

    public NativeProfileSettingsClient(string reader, string baseAddress, string secret, TimeProvider clock)
        : this(reader, baseAddress, secret, clock, new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { }

    internal NativeProfileSettingsClient(string reader, string baseAddress, string secret, TimeProvider clock, HttpMessageHandler handler)
    {
        if (reader is not ("analytics" or "financial-score" or "recommendations-notifications") ||
            secret is not { Length: >= 32 and <= 256 } || secret.Any(c => c is < '!' or > '~') ||
            !Uri.TryCreate(baseAddress, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https") ||
            address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0 || address.AbsolutePath != "/" ||
            address.Port is < 1 or > 65535 || !(address.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)))
        { handler.Dispose(); throw new ArgumentException("Native Profile reader configuration is invalid."); }
        if (clock is null) { handler.Dispose(); throw new ArgumentNullException(nameof(clock)); }
        this.reader = reader; this.secret = secret; this.clock = clock;
        endpoint = new Uri(address, ProfileFinancialSettingsApi.Path);
        client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<ProfileFinancialSettingsSnapshot> ReadAsync(string ownerHash, CancellationToken token = default)
    {
        if (!IsHash(ownerHash)) { throw new ArgumentException("Invalid Profile settings subject."); }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            { Content = JsonContent.Create(new ProfileFinancialSettingsQuery(ownerHash), options: Json) };
            request.Headers.Add(ProfileFinancialSettingsApi.ReaderHeader, reader);
            request.Headers.Add(ProfileFinancialSettingsApi.AuthenticationHeader, secret);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentType?.MediaType != "application/json" ||
                response.Content.Headers.ContentLength is > MaximumBytes)
            { throw new ProfileSettingsUnavailableException(); }
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var bytes = new MemoryStream(); var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (bytes.Length + read > MaximumBytes) { throw new ProfileSettingsUnavailableException(); }
                bytes.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Any(p => !names.Add(p.Name))) { throw new ProfileSettingsUnavailableException(); }
            var result = document.RootElement.Deserialize<ProfileFinancialSettingsSnapshot>(Json) ?? throw new ProfileSettingsUnavailableException();
            Validate(result, ownerHash);
            return result;
        }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception) { throw new ProfileSettingsUnavailableException(); }
    }

    private void Validate(ProfileFinancialSettingsSnapshot value, string owner)
    {
        var now = clock.GetUtcNow();
        if (value.OwnerHash != owner || !IsHash(value.OwnerHash) || value.CurrencyCode is not { Length: 3 } ||
            value.CurrencyCode.Any(c => c is < 'A' or > 'Z') || value.MonthlyBudgetAmount < 0 ||
            value.PrivacyMode is not ("standard" or "strict") || value.SourceRevision is < 0 or long.MaxValue ||
            value.SourceVersion != "profile-" + value.SourceRevision.ToString(CultureInfo.InvariantCulture) ||
            value.ObservedAtUtc == default || value.ObservedAtUtc.Offset != TimeSpan.Zero ||
            value.ObservedAtUtc > now || now - value.ObservedAtUtc > TimeSpan.FromHours(2))
        { throw new ProfileSettingsUnavailableException(); }
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public void Dispose() => client.Dispose();
    public override string ToString() => "Native Profile reader (credentials and subjects omitted).";
}
