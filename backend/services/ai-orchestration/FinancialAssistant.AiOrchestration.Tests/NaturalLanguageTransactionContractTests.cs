using System.Text.Json;
using FinancialAssistant.AiOrchestration.Contracts;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class NaturalLanguageTransactionContractTests
{
    [Fact]
    public void ExpenseExample_RepresentsAmbiguityAndMissingFieldsExplicitly()
    {
        var response = new NaturalLanguageTransactionParseResponse(
            "aicall_synthetic_expense",
            new NaturalLanguageTransactionSuggestion(
                "expense",
                42.50m,
                "USD",
                new DateOnly(2026, 7, 24),
                "Synthetic Market",
                null,
                "Weekly groceries"),
            new NaturalLanguageTransactionConfidenceScores(
                Overall: 0.78m,
                Type: 0.99m,
                Amount: 0.98m,
                Currency: 0.90m,
                Date: 0.82m,
                Merchant: 0.74m,
                Category: null,
                Note: 0.88m),
            new[]
            {
                new NaturalLanguageTransactionAmbiguity(
                    "category_multiple_candidates",
                    NaturalLanguageTransactionFields.Category,
                    new[] { "expense.groceries", "expense.household" })
            },
            new[] { NaturalLanguageTransactionFields.Category },
            "The category needs your review before this draft can be confirmed.");

        Assert.Equal(AiOutputAuthority.Suggestion, response.OutputAuthority);
        Assert.True(response.RequiresReview);
        Assert.Equal("expense", response.Suggestion.Type);
        Assert.Equal(42.50m, response.Suggestion.Amount);
        Assert.Contains(NaturalLanguageTransactionFields.Category, response.MissingFields);
        Assert.Single(response.Ambiguities);
    }

    [Fact]
    public void IncomeExample_ContainsEverySuggestedFieldWithoutBecomingConfirmed()
    {
        var response = new NaturalLanguageTransactionParseResponse(
            "aicall_synthetic_income",
            new NaturalLanguageTransactionSuggestion(
                "income",
                2500m,
                "EUR",
                new DateOnly(2026, 7, 24),
                "Synthetic Employer",
                "income.salary",
                "Monthly salary"),
            new NaturalLanguageTransactionConfidenceScores(
                Overall: 0.96m,
                Type: 0.99m,
                Amount: 0.99m,
                Currency: 0.98m,
                Date: 0.94m,
                Merchant: 0.93m,
                Category: 0.97m,
                Note: 0.91m),
            Array.Empty<NaturalLanguageTransactionAmbiguity>(),
            Array.Empty<string>(),
            "Review the salary suggestion before confirmation.");

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"outputAuthority\":\"suggestion\"", json, StringComparison.Ordinal);
        Assert.Contains("\"requiresReview\":true", json, StringComparison.Ordinal);
        Assert.Empty(response.Ambiguities);
        Assert.Empty(response.MissingFields);
        Assert.Equal("income", response.Suggestion.Type);
    }

    [Fact]
    public void ParseResponse_CannotCarryAuthoritativeFinancialState()
    {
        var properties = typeof(NaturalLanguageTransactionParseResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(
                typeof(NaturalLanguageTransactionSuggestion)
                    .GetProperties()
                    .Select(property => property.Name))
            .ToArray();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Confirmed", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Persisted", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("TransactionId", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Balance", StringComparison.OrdinalIgnoreCase));
    }
}
