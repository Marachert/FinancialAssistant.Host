using System.Diagnostics;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ResilientOcrProviderTests
{
    private static readonly byte[] SyntheticReceipt = [0x01, 0x02, 0x03];

    [Fact]
    public async Task ExtractAsync_RetriesAnExplicitTransientFailure()
    {
        var client = new RecordingClient((attempt, _, _, _) =>
            attempt == 1
                ? Task.FromException<OcrExtractionResult>(
                    new OcrProviderException(
                        OcrProviderErrorCodes.ProviderUnavailable,
                        isTransient: true))
                : Task.FromResult(CreateExtraction()));
        var provider = CreateProvider(client, maximumAttempts: 2);

        var result = await provider.ExtractAsync(
            new MemoryStream(SyntheticReceipt),
            "image/png",
            CancellationToken.None);

        Assert.Equal(2, client.Attempts);
        Assert.Equal(CreateExtraction(), result);
        Assert.All(client.ReceivedContent, content => Assert.Equal(SyntheticReceipt, content));
    }

    [Fact]
    public async Task ExtractAsync_EnforcesTimeoutWhenClientIgnoresCancellation()
    {
        var neverCompletes = new TaskCompletionSource<OcrExtractionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingClient((_, _, _, _) => neverCompletes.Task);
        var provider = new ResilientOcrProvider(
            client,
            new OcrProviderResilienceOptions(
                TimeSpan.FromMilliseconds(20),
                maximumAttempts: 2,
                TimeSpan.Zero));
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<OcrProviderException>(() =>
            provider.ExtractAsync(
                new MemoryStream(SyntheticReceipt),
                "image/png",
                CancellationToken.None));

        stopwatch.Stop();
        Assert.Equal(OcrProviderErrorCodes.ProviderTimeout, exception.ErrorCode);
        Assert.True(exception.IsTransient);
        Assert.Equal(2, client.Attempts);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExtractAsync_MapsUnknownFailureWithoutLeakingProviderDetails()
    {
        var client = new RecordingClient((_, _, _, _) =>
            Task.FromException<OcrExtractionResult>(
                new InvalidOperationException("secret provider response body")));
        var provider = CreateProvider(client, maximumAttempts: 3);

        var exception = await Assert.ThrowsAsync<OcrProviderException>(() =>
            provider.ExtractAsync(
                new MemoryStream(SyntheticReceipt),
                "image/png",
                CancellationToken.None));

        Assert.Equal(OcrProviderErrorCodes.ProviderFailure, exception.ErrorCode);
        Assert.False(exception.IsTransient);
        Assert.Equal(1, client.Attempts);
        Assert.DoesNotContain("secret", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_DoesNotRetryClientOriginatedCancellation()
    {
        using var providerCancellation = new CancellationTokenSource();
        providerCancellation.Cancel();
        var client = new RecordingClient((_, _, _, _) =>
            Task.FromCanceled<OcrExtractionResult>(providerCancellation.Token));
        var provider = CreateProvider(client, maximumAttempts: 3);

        var exception = await Assert.ThrowsAsync<OcrProviderException>(() =>
            provider.ExtractAsync(
                new MemoryStream(SyntheticReceipt),
                "image/png",
                CancellationToken.None));

        Assert.Equal(OcrProviderErrorCodes.ProviderFailure, exception.ErrorCode);
        Assert.False(exception.IsTransient);
        Assert.Equal(1, client.Attempts);
    }

    [Fact]
    public async Task ExtractAsync_PreservesCallerCancellationWithoutRetry()
    {
        var client = new RecordingClient(async (_, _, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateExtraction();
        });
        var provider = CreateProvider(client, maximumAttempts: 3);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.ExtractAsync(
                new MemoryStream(SyntheticReceipt),
                "image/png",
                cancellation.Token));

        Assert.Equal(1, client.Attempts);
    }

    [Fact]
    public void FromConfiguration_BindsEnvironmentCompatibleResilienceSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ReceiptProcessing:Ocr:RequestTimeoutSeconds"] = "45",
                    ["ReceiptProcessing:Ocr:MaximumAttempts"] = "3",
                    ["ReceiptProcessing:Ocr:RetryDelayMilliseconds"] = "250"
                })
            .Build();

        var options = OcrProviderResilienceOptions.FromConfiguration(configuration);

        Assert.Equal(TimeSpan.FromSeconds(45), options.RequestTimeout);
        Assert.Equal(3, options.MaximumAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.RetryDelay);
    }

    [Theory]
    [InlineData("RequestTimeoutSeconds", "0")]
    [InlineData("MaximumAttempts", "4")]
    [InlineData("RetryDelayMilliseconds", "5001")]
    [InlineData("MaximumAttempts", "not-a-number")]
    public void FromConfiguration_RejectsInvalidResilienceSettings(
        string settingName,
        string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ReceiptProcessing:Ocr:{settingName}"] = value
                })
            .Build();

        Assert.ThrowsAny<Exception>(() =>
            OcrProviderResilienceOptions.FromConfiguration(configuration));
    }

    private static ResilientOcrProvider CreateProvider(
        IOcrProviderClient client,
        int maximumAttempts) =>
        new(
            client,
            new OcrProviderResilienceOptions(
                TimeSpan.FromSeconds(1),
                maximumAttempts,
                TimeSpan.Zero));

    private static OcrExtractionResult CreateExtraction() =>
        new("10.00 USD", 0.9m, Array.Empty<string>());

    private sealed class RecordingClient : IOcrProviderClient
    {
        private readonly Func<
            int,
            ReadOnlyMemory<byte>,
            string,
            CancellationToken,
            Task<OcrExtractionResult>> handler;

        public RecordingClient(
            Func<
                int,
                ReadOnlyMemory<byte>,
                string,
                CancellationToken,
                Task<OcrExtractionResult>> handler)
        {
            this.handler = handler;
        }

        public int Attempts { get; private set; }

        public List<byte[]> ReceivedContent { get; } = [];

        public Task<OcrExtractionResult> ExtractAsync(
            ReadOnlyMemory<byte> receiptImage,
            string contentType,
            CancellationToken cancellationToken)
        {
            Attempts++;
            ReceivedContent.Add(receiptImage.ToArray());
            return handler(Attempts, receiptImage, contentType, cancellationToken);
        }
    }
}
