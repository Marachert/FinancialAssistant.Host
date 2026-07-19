using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionDraftValidatorTests
{
    [Fact]
    public void Validate_DoesNotPromoteInvalidParserOutputIntoDraftFields()
    {
        var validator = new TransactionDraftValidator();
        var createdAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var candidate = new ParsedTransactionCandidate(
            "expense",
            -100,
            "BTC",
            "income.salary",
            new string('x', 121),
            new DateOnly(2040, 1, 1),
            5);

        var draft = validator.Validate(
            "draft_synthetic_invalid",
            "synthetic-validator-user",
            "SYNTHETICFINGERPRINT",
            candidate,
            createdAt);

        Assert.Equal("expense", draft.Type);
        Assert.Null(draft.Amount);
        Assert.Null(draft.Currency);
        Assert.Null(draft.CategoryId);
        Assert.Null(draft.Merchant);
        Assert.Null(draft.Date);
        Assert.Equal(0, draft.Confidence);
        Assert.True(draft.RequiresReview);
        Assert.Contains("amount", draft.Ambiguities);
        Assert.Contains("currency", draft.Ambiguities);
        Assert.Contains("category", draft.Ambiguities);
        Assert.Contains("merchant", draft.Ambiguities);
        Assert.Contains("date", draft.Ambiguities);
    }

    [Fact]
    public void Validate_RejectsPositiveAmountThatRoundsToZero()
    {
        var validator = new TransactionDraftValidator();
        var createdAt = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        var candidate = new ParsedTransactionCandidate(
            "expense",
            0.001m,
            "USD",
            "expense.food",
            "Synthetic Merchant",
            new DateOnly(2026, 7, 19),
            0.99m);

        var draft = validator.Validate(
            "draft_synthetic_subcent",
            "synthetic-validator-user",
            "SYNTHETICFINGERPRINT",
            candidate,
            createdAt);

        Assert.Null(draft.Amount);
        Assert.Contains("amount", draft.Ambiguities);
        Assert.True(draft.RequiresReview);
    }
}
