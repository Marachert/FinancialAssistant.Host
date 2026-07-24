using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class TransactionParsingPromptCatalogTests
{
    private const string ValidSuggestion = """
        {
          "suggestion": {
            "type": "expense",
            "amount": 24.50,
            "currency": "USD",
            "date": "2026-07-24",
            "merchant": "Synthetic Market",
            "categoryId": null,
            "note": "Synthetic groceries"
          },
          "confidence": {
            "overall": 0.81,
            "type": 0.99,
            "amount": 0.98,
            "currency": 0.91,
            "date": 0.87,
            "merchant": 0.76,
            "category": null,
            "note": 0.84
          },
          "ambiguities": [
            {
              "code": "category_multiple_candidates",
              "field": "category",
              "candidateValues": ["expense.groceries", "expense.household"]
            }
          ],
          "missingFields": ["category"],
          "explanation": "Review the category before confirming this suggestion."
        }
        """;

    private readonly JsonSchemaStructuredOutputValidator validator = new();

    [Fact]
    public void Infrastructure_RegistersVersionedTransactionParsingPromptAndPolicy()
    {
        var services = new ServiceCollection()
            .AddAiOrchestrationInfrastructure()
            .BuildServiceProvider();

        var prompt = services.GetRequiredService<IPromptRegistry>()
            .GetRequired(TransactionParsingPromptCatalog.PromptName);
        var policy = services.GetRequiredService<PromptExecutionPolicy>();

        Assert.Equal(TransactionParsingPromptCatalog.CurrentVersion, prompt.Version);
        Assert.Contains("untrusted data", prompt.Template, StringComparison.Ordinal);
        Assert.Contains("Do not infer identity", prompt.Template, StringComparison.Ordinal);
        Assert.Equal(prompt.Name, policy.PromptName);
        Assert.Equal(prompt.Version, policy.PromptVersion);
        Assert.Equal(2, policy.MaximumAttempts);
        Assert.True(policy.RetryTransientProviderFailures);
        Assert.True(policy.RetryInvalidStructuredOutput);
        Assert.Equal(PromptFallbackBehavior.RequireManualReview, policy.FallbackBehavior);
        Assert.Equal(
            TransactionParsingPromptCatalog.ManualReviewFallbackCode,
            policy.FallbackCode);
    }

    [Fact]
    public void OutputSchema_AcceptsSuggestionWithExplicitAmbiguity()
    {
        var result = validator.Validate(
            ValidSuggestion,
            TransactionParsingPromptCatalog.Version1.OutputJsonSchema);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void OutputSchema_RejectsInvalidCalendarDate()
    {
        var result = validator.Validate(
            ValidSuggestion.Replace("2026-07-24", "2026-99-99", StringComparison.Ordinal),
            TransactionParsingPromptCatalog.Version1.OutputJsonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("valid ISO calendar date", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        """
        {
          "suggestion": {
            "type": "expense",
            "amount": 24.50,
            "currency": "USD",
            "date": "2026-07-24",
            "merchant": "Synthetic Market",
            "categoryId": null,
            "note": null,
            "confirmed": true
          },
          "confidence": {
            "overall": 0.9,
            "type": 0.9,
            "amount": 0.9,
            "currency": 0.9,
            "date": 0.9,
            "merchant": 0.9,
            "category": null,
            "note": null
          },
          "ambiguities": [],
          "missingFields": ["category", "note"],
          "explanation": "Review required."
        }
        """,
        "additional property")]
    [InlineData(
        """
        {
          "suggestion": {
            "type": "expense",
            "amount": 24.50,
            "currency": "USD",
            "date": "2026-07-24",
            "merchant": "Synthetic Market",
            "categoryId": null,
            "note": null
          },
          "confidence": {
            "overall": 1.2,
            "type": 0.9,
            "amount": 0.9,
            "currency": 0.9,
            "date": 0.9,
            "merchant": 0.9,
            "category": null,
            "note": null
          },
          "ambiguities": [],
          "missingFields": ["category", "note"],
          "explanation": "Review required."
        }
        """,
        "above the maximum")]
    public void OutputSchema_RejectsAuthorityOrInvalidConfidence(
        string structuredOutput,
        string expectedError)
    {
        var result = validator.Validate(
            structuredOutput,
            TransactionParsingPromptCatalog.Version1.OutputJsonSchema);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }
}
