using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed class ResilientOcrProvider : IOcrProvider
{
    private readonly IOcrProviderClient client;
    private readonly OcrProviderResilienceOptions options;

    public ResilientOcrProvider(
        IOcrProviderClient client,
        OcrProviderResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        this.client = client;
        this.options = options;
    }

    public async Task<OcrExtractionResult> ExtractAsync(
        Stream receiptImage,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receiptImage);
        if (!receiptImage.CanRead)
        {
            throw new ArgumentException("Receipt image stream must be readable.", nameof(receiptImage));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Receipt content type is required.", nameof(contentType));
        }

        var content = await ReadReceiptAsync(receiptImage, cancellationToken);
        for (var attempt = 1; attempt <= options.MaximumAttempts; attempt++)
        {
            var failure = await TryExtractAsync(content, contentType, cancellationToken);
            if (failure.Extraction is not null)
            {
                return failure.Extraction;
            }

            if (!failure.Exception!.IsTransient || attempt == options.MaximumAttempts)
            {
                throw failure.Exception;
            }

            if (options.RetryDelay > TimeSpan.Zero)
            {
                await Task.Delay(options.RetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException("OCR provider retry loop ended unexpectedly.");
    }

    private static async Task<ReadOnlyMemory<byte>> ReadReceiptAsync(
        Stream receiptImage,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await receiptImage.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > ReceiptProcessingService.MaximumReceiptSizeBytes)
            {
                throw new OcrProviderException(
                    OcrProviderErrorCodes.InvalidReceiptContent,
                    isTransient: false);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            throw new OcrProviderException(
                OcrProviderErrorCodes.InvalidReceiptContent,
                isTransient: false);
        }

        return buffer.ToArray();
    }

    private async Task<ProviderAttemptResult> TryExtractAsync(
        ReadOnlyMemory<byte> receiptImage,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);

        Task<OcrExtractionResult>? providerTask = null;
        try
        {
            providerTask = client.ExtractAsync(receiptImage, contentType, timeout.Token);
            var extraction = await providerTask.WaitAsync(timeout.Token);
            return extraction is null
                ? ProviderAttemptResult.Failed(
                    new OcrProviderException(
                        OcrProviderErrorCodes.InvalidProviderResponse,
                        isTransient: false))
                : ProviderAttemptResult.Succeeded(extraction);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveFailure(providerTask);
            throw;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            ObserveFailure(providerTask);
            return ProviderAttemptResult.Failed(
                new OcrProviderException(
                    OcrProviderErrorCodes.ProviderTimeout,
                    isTransient: true));
        }
        catch (OcrProviderException exception)
        {
            return ProviderAttemptResult.Failed(exception);
        }
        catch
        {
            return ProviderAttemptResult.Failed(
                new OcrProviderException(
                    OcrProviderErrorCodes.ProviderFailure,
                    isTransient: false));
        }
    }

    private static void ObserveFailure(Task? providerTask)
    {
        if (providerTask is null)
        {
            return;
        }

        _ = providerTask.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed record ProviderAttemptResult(
        OcrExtractionResult? Extraction,
        OcrProviderException? Exception)
    {
        public static ProviderAttemptResult Succeeded(OcrExtractionResult extraction) =>
            new(extraction, null);

        public static ProviderAttemptResult Failed(OcrProviderException exception) =>
            new(null, exception);
    }
}
