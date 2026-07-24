using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class ResilientLlmProviderTests
{
    private static readonly LlmProviderRequest Request = new(
        "transaction.parse",
        "model-a",
        "Synthetic prompt",
        "Synthetic input",
        "{}");

    [Fact]
    public async Task Complete_WhenTransientFailureOccurs_RetriesAndReturnsResponse()
    {
        var provider = new SequenceProvider(
            _ => Task.FromException<LlmProviderResponse>(
                new LlmProviderException("synthetic-provider", "rate_limited", isTransient: true)),
            _ => Task.FromResult(new LlmProviderResponse("{}", 3, 2)));
        var client = CreateClient(provider, maximumAttempts: 2);

        var response = await client.CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("{}", response.StructuredOutputJson);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Complete_WhenRequestTimesOut_RetriesOnlyToConfiguredLimit()
    {
        var provider = new SequenceProvider(
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new LlmProviderResponse("{}", 0, 0);
            },
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new LlmProviderResponse("{}", 0, 0);
            });
        var client = CreateClient(
            provider,
            maximumAttempts: 2,
            requestTimeout: TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.CompleteAsync(Request, CancellationToken.None));

        Assert.Equal("provider_timeout", exception.Code);
        Assert.True(exception.IsTransient);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Complete_WhenAdapterIgnoresCancellation_StillEnforcesTimeout()
    {
        var incompleteResponse = new TaskCompletionSource<LlmProviderResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new SequenceProvider(_ => incompleteResponse.Task);
        var client = CreateClient(
            provider,
            maximumAttempts: 1,
            requestTimeout: TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.CompleteAsync(Request, CancellationToken.None));

        Assert.Equal("provider_timeout", exception.Code);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Complete_WhenProviderThrowsUnknownError_MapsSafeFailureWithoutRetry()
    {
        var provider = new SequenceProvider(
            _ => Task.FromException<LlmProviderResponse>(
                new InvalidOperationException("raw synthetic provider detail")));
        var client = CreateClient(provider, maximumAttempts: 3);

        var exception = await Assert.ThrowsAsync<LlmProviderException>(() =>
            client.CompleteAsync(Request, CancellationToken.None));

        Assert.Equal("provider_failure", exception.Code);
        Assert.False(exception.IsTransient);
        Assert.DoesNotContain("raw synthetic provider detail", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Complete_WhenCallerCancels_DoesNotMapOrRetry()
    {
        var provider = new SequenceProvider(
            cancellationToken => Task.FromCanceled<LlmProviderResponse>(cancellationToken));
        var client = CreateClient(provider, maximumAttempts: 3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CompleteAsync(Request, cancellation.Token));

        Assert.Equal(1, provider.CallCount);
    }

    private static ResilientLlmProvider CreateClient(
        ILlmProvider provider,
        int maximumAttempts,
        TimeSpan? requestTimeout = null) =>
        new(
            provider,
            new LlmProviderResilienceOptions(
                requestTimeout ?? TimeSpan.FromSeconds(1),
                maximumAttempts,
                retryDelay: TimeSpan.Zero));

    private sealed class SequenceProvider : ILlmProvider
    {
        private readonly Queue<Func<CancellationToken, Task<LlmProviderResponse>>> attempts;

        public SequenceProvider(
            params Func<CancellationToken, Task<LlmProviderResponse>>[] attempts)
        {
            this.attempts = new Queue<Func<CancellationToken, Task<LlmProviderResponse>>>(attempts);
        }

        public string Name => "synthetic-provider";

        public int CallCount { get; private set; }

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return attempts.Dequeue()(cancellationToken);
        }
    }
}
