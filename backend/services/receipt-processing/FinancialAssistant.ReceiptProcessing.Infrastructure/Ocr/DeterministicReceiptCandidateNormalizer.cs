using System.Globalization;
using System.Text.RegularExpressions;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed partial class DeterministicReceiptCandidateNormalizer : IOcrCandidateNormalizer
{
    private const int MaximumExtractedTextLength = 20_000;

    public NormalizedReceiptCandidate Normalize(OcrExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        if (extraction.ExtractedText is null ||
            extraction.ExtractedText.Length > MaximumExtractedTextLength ||
            extraction.Ambiguities is null)
        {
            throw new InvalidOperationException("OCR output text is invalid.");
        }

        var ambiguities = new SortedSet<string>(
            extraction.Ambiguities
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .Where(value =>
                    value.Length <= 64 &&
                    value.All(character =>
                        char.IsLower(character) ||
                        char.IsDigit(character) ||
                        character == '_'))
                .Take(50),
            StringComparer.Ordinal);
        if (extraction.Ambiguities.Count > 50 ||
            ambiguities.Count != extraction.Ambiguities.Count)
        {
            ambiguities.Add("ocr_ambiguity_invalid");
        }

        var confidence = extraction.Confidence is >= 0 and <= 1
            ? extraction.Confidence
            : 0;
        if (confidence != extraction.Confidence)
        {
            ambiguities.Add("invalid_confidence");
        }

        var amountMatch = AmountPattern().Match(extraction.ExtractedText);
        decimal? amount = amountMatch.Success &&
            decimal.TryParse(
                amountMatch.Groups["amount"].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsedAmount)
            ? parsedAmount
            : null;
        var currency = amountMatch.Groups["currency"].Success
            ? amountMatch.Groups["currency"].Value.ToUpperInvariant()
            : null;
        var dateMatch = DatePattern().Match(extraction.ExtractedText);
        DateOnly? date = dateMatch.Success &&
            DateOnly.TryParseExact(
                dateMatch.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate)
            ? parsedDate
            : null;
        var merchantMatch = MerchantPattern().Match(extraction.ExtractedText);
        var merchant = merchantMatch.Success ? merchantMatch.Groups["merchant"].Value.Trim() : null;

        if (amount is null)
        {
            ambiguities.Add("amount");
        }

        if (currency is null)
        {
            ambiguities.Add("currency");
        }

        if (date is null)
        {
            ambiguities.Add("date");
        }

        if (confidence < 0.75m)
        {
            ambiguities.Add("low_confidence");
        }

        return new NormalizedReceiptCandidate(
            "expense",
            amount,
            currency,
            "expense.other",
            merchant,
            date,
            confidence,
            ambiguities.ToArray());
    }

    [GeneratedRegex(@"(?<amount>\d{1,12}(?:[.,]\d{1,2})?)\s*(?<currency>USD|EUR|GBP|UAH)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"merchant:\s*(?<merchant>[\p{L}\p{N} .&'-]{1,120})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantPattern();
}

public sealed class DisabledOcrProvider : IOcrProvider
{
    public Task<OcrExtractionResult> ExtractAsync(
        Stream receiptImage,
        string contentType,
        CancellationToken cancellationToken) =>
        Task.FromException<OcrExtractionResult>(
            new InvalidOperationException("OCR provider is not configured."));
}
