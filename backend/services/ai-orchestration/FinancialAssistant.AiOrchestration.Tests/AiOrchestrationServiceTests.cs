using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;
using FinancialAssistant.AiOrchestration.Infrastructure.Routing;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiOrchestrationServiceTests
{
    private const string Schema = """
        {
          "type": "object",
          "required": ["type"],
          "additionalProperties": false,
          "properties": { "type": { "type": "string" } }
        }
        """;

    [Fact]
    public async Task Execute_RoutesValidatesAndPersistsOnlySafeMetadata()
    {
        var provider = new StubProvider("synthetic-provider", """{"type":"expense"}""", 17, 5);
        var metadataStore = new InMemoryAiCallMetadataStore();
        var service = CreateService(provider, metadataStore);

        var result = await service.ExecuteAsync(
            new AiCapabilityRequest(
                "transaction.parse",
                "transaction.parse",
                "synthetic private input"),
            CancellationToken.None);

        var metadata = Assert.Single(metadataStore.Records);
        Assert.Equal("expense", result.StructuredOutput.GetProperty("type").GetString());
        Assert.True(result.Review.RequiresReview);
        Assert.Null(result.Review.Confidence);
        Assert.Contains("unverified_ai_output", result.Review.Ambiguities);
        Assert.Equal("model-a", provider.LastRequest!.Model);
        Assert.Equal(AiCallStatus.Succeeded, metadata.Status);
        Assert.Equal(22, metadata.TokenUsage!.TotalTokens);
        Assert.DoesNotContain(
            metadata.GetType().GetProperties(),
            property => property.Name.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("PromptTemplate", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Output", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_WhenOutputFailsSchema_RecordsFailureAndDoesNotReturnOutput()
    {
        var provider = new StubProvider("synthetic-provider", """{"unexpected":true}""", 4, 2);
        var metadataStore = new InMemoryAiCallMetadataStore();
        var service = CreateService(provider, metadataStore);

        await Assert.ThrowsAsync<StructuredOutputValidationException>(() =>
            service.ExecuteAsync(
                new AiCapabilityRequest("transaction.parse", "transaction.parse", "synthetic input"),
                CancellationToken.None));

        var metadata = Assert.Single(metadataStore.Records);
        Assert.Equal(AiCallStatus.ValidationFailed, metadata.Status);
        Assert.Equal(6, metadata.TokenUsage!.TotalTokens);
    }

    [Fact]
    public async Task Execute_WhenProviderFails_RecordsProviderFailureWithoutRawException()
    {
        var provider = new StubProvider(
            "synthetic-provider",
            new InvalidOperationException("synthetic provider failure"));
        var metadataStore = new InMemoryAiCallMetadataStore();
        var service = CreateService(provider, metadataStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(
                new AiCapabilityRequest("transaction.parse", "transaction.parse", "synthetic input"),
                CancellationToken.None));

        var metadata = Assert.Single(metadataStore.Records);
        Assert.Equal(AiCallStatus.ProviderFailed, metadata.Status);
        Assert.Null(metadata.TokenUsage);
        Assert.DoesNotContain(
            metadata.GetType().GetProperties(),
            property => property.Name.Contains("Error", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Execute_WhenProviderReportsNegativeUsage_RejectsAndRecordsFailure()
    {
        var provider = new StubProvider("synthetic-provider", "{}", -1, 0);
        var metadataStore = new InMemoryAiCallMetadataStore();
        var service = CreateService(provider, metadataStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(
                new AiCapabilityRequest("transaction.parse", "transaction.parse", "synthetic input"),
                CancellationToken.None));

        Assert.Equal(AiCallStatus.ProviderFailed, Assert.Single(metadataStore.Records).Status);
    }

    [Fact]
    public async Task Execute_WhenRegisteredSchemaIsInvalid_RecordsValidationFailure()
    {
        var provider = new StubProvider("synthetic-provider", "{}", 3, 1);
        var metadataStore = new InMemoryAiCallMetadataStore();
        var service = CreateService(provider, metadataStore, "not-json");

        await Assert.ThrowsAsync<InvalidJsonSchemaException>(() =>
            service.ExecuteAsync(
                new AiCapabilityRequest("transaction.parse", "transaction.parse", "synthetic input"),
                CancellationToken.None));

        var metadata = Assert.Single(metadataStore.Records);
        Assert.Equal(AiCallStatus.ValidationFailed, metadata.Status);
        Assert.Equal(4, metadata.TokenUsage!.TotalTokens);
    }

    private static AiOrchestrationService CreateService(
        ILlmProvider provider,
        InMemoryAiCallMetadataStore metadataStore,
        string schema = Schema) =>
        new(
            new StaticModelRouter(
                new[] { new AiModelRoute("transaction.parse", "synthetic-provider", "model-a") }),
            new InMemoryPromptRegistry(
                new[] { new PromptDefinition("transaction.parse", 1, "Parse input", schema) }),
            new RegisteredLlmProviderResolver(new[] { provider }),
            new JsonSchemaStructuredOutputValidator(),
            metadataStore,
            new FixedClock(),
            new FixedCallIdGenerator());

    private sealed class StubProvider : ILlmProvider
    {
        private readonly LlmProviderResponse? response;
        private readonly Exception? exception;

        public StubProvider(
            string name,
            string output,
            int inputTokens,
            int outputTokens)
        {
            Name = name;
            response = new LlmProviderResponse(output, inputTokens, outputTokens);
        }

        public StubProvider(string name, Exception exception)
        {
            Name = name;
            this.exception = exception;
        }

        public string Name { get; }

        public LlmProviderRequest? LastRequest { get; private set; }

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return exception is null
                ? Task.FromResult(response!)
                : Task.FromException<LlmProviderResponse>(exception);
        }
    }

    private sealed class FixedClock : IAiOrchestrationClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 19, 20, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixedCallIdGenerator : IAiCallIdGenerator
    {
        public string CreateCallId() => "aicall_synthetic_001";
    }
}
