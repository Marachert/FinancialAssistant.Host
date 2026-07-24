using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Providers;

public sealed class ResilientLlmProvider : ILlmProvider
{
    private readonly ILlmProvider inner;
    private readonly LlmProviderResilienceOptions options;

    public ResilientLlmProvider(
        ILlmProvider inner,
        LlmProviderResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(inner.Name))
        {
            throw new ArgumentException("Provider name is required.", nameof(inner));
        }

        this.inner = inner;
        this.options = options;
    }

    public string Name => inner.Name;

    public async Task<LlmProviderResponse> CompleteAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var attempt = 1; attempt <= options.MaximumAttempts; attempt++)
        {
            var failure = await TryCompleteAsync(request, cancellationToken);
            if (failure.Response is not null)
            {
                return failure.Response;
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

        throw new InvalidOperationException("Provider retry loop ended unexpectedly.");
    }

    private async Task<ProviderAttemptResult> TryCompleteAsync(
        LlmProviderRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RequestTimeout);

        try
        {
            var response = await inner
                .CompleteAsync(request, timeout.Token)
                .WaitAsync(timeout.Token);
            return response is null
                ? ProviderAttemptResult.Failed(
                    new LlmProviderException(Name, "invalid_provider_response", isTransient: false))
                : ProviderAttemptResult.Succeeded(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ProviderAttemptResult.Failed(
                new LlmProviderException(Name, "provider_timeout", isTransient: true));
        }
        catch (LlmProviderException exception)
        {
            return ProviderAttemptResult.Failed(exception);
        }
        catch
        {
            return ProviderAttemptResult.Failed(
                new LlmProviderException(Name, "provider_failure", isTransient: false));
        }
    }

    private sealed record ProviderAttemptResult(
        LlmProviderResponse? Response,
        LlmProviderException? Exception)
    {
        public static ProviderAttemptResult Succeeded(LlmProviderResponse response) =>
            new(response, null);

        public static ProviderAttemptResult Failed(LlmProviderException exception) =>
            new(null, exception);
    }
}
