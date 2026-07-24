using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ReceiptCandidateNormalizerTests
{
    [Fact]
    public void Normalize_ExtractsLabeledReceiptCandidatesAndLineItemPlaceholders()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            """
              merchant:   Synthetic Market
            date: 2026-07-20
            total: 21.50 USD
            tax: 1.50 USD
            item: Coffee | qty: 2 | unit: 5.00 | total: 10.00 USD
            item: Snack | qty: 1 | unit: 11.50 | total: 11.50 USD
            """,
            0.93m,
            Array.Empty<string>());

        var candidate = normalizer.Normalize(extraction);

        Assert.Equal("expense", candidate.TransactionType);
        Assert.Equal(21.50m, candidate.Amount);
        Assert.Equal("USD", candidate.Currency);
        Assert.Equal("Synthetic Market", candidate.Merchant);
        Assert.Equal(new DateOnly(2026, 7, 20), candidate.Date);
        Assert.Equal(1.50m, candidate.TaxAmount);
        Assert.Equal(0.93m, candidate.Confidence);
        Assert.Equal(ReceiptCandidateAuthority.Suggestion, candidate.OutputAuthority);
        Assert.True(candidate.RequiresReview);
        Assert.Empty(candidate.Ambiguities);
        Assert.Collection(
            candidate.LineItems,
            item =>
            {
                Assert.Equal("Coffee", item.Description);
                Assert.Equal(2m, item.Quantity);
                Assert.Equal(5.00m, item.UnitPrice);
                Assert.Equal(10.00m, item.TotalAmount);
                Assert.Equal("USD", item.Currency);
                Assert.Equal(0.93m, item.Confidence);
                Assert.Empty(item.Ambiguities);
            },
            item =>
            {
                Assert.Equal("Snack", item.Description);
                Assert.Equal(1m, item.Quantity);
                Assert.Equal(11.50m, item.UnitPrice);
                Assert.Equal(11.50m, item.TotalAmount);
                Assert.Equal("USD", item.Currency);
                Assert.Empty(item.Ambiguities);
            });
    }

    [Fact]
    public void Normalize_RepresentsMultipleTotalsAsExplicitAmbiguity()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            """
            total: 20.00 USD
            total: 21.00 USD
            date: 2026-07-20
            """,
            0.88m,
            Array.Empty<string>());

        var candidate = normalizer.Normalize(extraction);

        Assert.Null(candidate.Amount);
        Assert.Equal("USD", candidate.Currency);
        Assert.Contains("total_ambiguous", candidate.Ambiguities);
        Assert.Contains("amount", candidate.Ambiguities);
    }

    [Fact]
    public void Normalize_RepeatedUnlabeledAmountsRemainAmbiguous()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            """
            item: Coffee | total: 5.00 USD
            item: Snack | total: 5.00 USD
            date: 2026-07-20
            """,
            0.88m,
            Array.Empty<string>());

        var candidate = normalizer.Normalize(extraction);

        Assert.Null(candidate.Amount);
        Assert.Equal("USD", candidate.Currency);
        Assert.Contains("total_ambiguous", candidate.Ambiguities);
        Assert.Contains("amount", candidate.Ambiguities);
    }

    [Fact]
    public void Normalize_RejectsImpossibleCalendarDateAsCandidate()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            "total: 10.00 USD date: 2026-02-30",
            0.9m,
            Array.Empty<string>());

        var candidate = normalizer.Normalize(extraction);

        Assert.Null(candidate.Date);
        Assert.Contains("date_invalid", candidate.Ambiguities);
        Assert.Contains("date", candidate.Ambiguities);
    }

    [Fact]
    public void Normalize_LineItemPlaceholderKeepsMissingAndMismatchedFieldsExplicit()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            """
            total: 10.00 USD
            date: 2026-07-20
            item: Coffee | qty: 2 | unit: 3.00 | total: 10.00 USD
            item: Unknown
            """,
            0.8m,
            Array.Empty<string>());

        var candidate = normalizer.Normalize(extraction);

        Assert.Contains(
            "line_item_total_mismatch",
            candidate.LineItems[0].Ambiguities);
        Assert.Contains(
            "line_item_quantity_missing",
            candidate.LineItems[1].Ambiguities);
        Assert.Contains(
            "line_item_unit_price_missing",
            candidate.LineItems[1].Ambiguities);
        Assert.Contains(
            "line_item_total_missing",
            candidate.LineItems[1].Ambiguities);
    }

    [Fact]
    public void Normalize_DoesNotCopyUnlabeledRawTextIntoCandidateFields()
    {
        const string rawProviderText = "unstructured sensitive receipt wording";
        var normalizer = new DeterministicReceiptCandidateNormalizer();

        var candidate = normalizer.Normalize(
            new OcrExtractionResult(rawProviderText, 0.2m, Array.Empty<string>()));

        Assert.Null(candidate.Merchant);
        Assert.Empty(candidate.LineItems);
        Assert.DoesNotContain(
            candidate.GetType().GetProperties(),
            property => property.Name.Contains("Text", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            rawProviderText,
            string.Join('|', candidate.Ambiguities),
            StringComparison.Ordinal);
    }
}
