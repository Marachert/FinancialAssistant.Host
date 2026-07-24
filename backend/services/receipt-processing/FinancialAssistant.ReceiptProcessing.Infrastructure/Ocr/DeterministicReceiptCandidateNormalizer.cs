using System.Globalization;
using System.Text.RegularExpressions;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;

public sealed partial class DeterministicReceiptCandidateNormalizer : IOcrCandidateNormalizer
{
    private const int MaximumExtractedTextLength = 20_000;
    private const int MaximumLineItems = 100;

    public NormalizedReceiptCandidate Normalize(OcrExtractionResult extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        if (extraction.ExtractedText is null ||
            extraction.ExtractedText.Length > MaximumExtractedTextLength ||
            extraction.Ambiguities is null)
        {
            throw new InvalidOperationException("OCR output text is invalid.");
        }

        var ambiguities = NormalizeAmbiguities(extraction);
        var confidence = NormalizeConfidence(extraction.Confidence, ambiguities);
        var normalizedText = NormalizeText(extraction.ExtractedText);
        var monetaryCandidates = ParseMoneyMatches(MoneyPattern().Matches(normalizedText));
        var totalCandidates = ParseMoneyMatches(TotalPattern().Matches(normalizedText));
        var selectedTotal = SelectSingleMoneyCandidate(
            totalCandidates.Count > 0 ? totalCandidates : monetaryCandidates,
            "total_ambiguous",
            ambiguities);
        var currency = SelectCurrency(selectedTotal, monetaryCandidates, ambiguities);
        var taxAmount = SelectSingleMoneyCandidate(
            ParseMoneyMatches(TaxPattern().Matches(normalizedText)),
            "tax_ambiguous",
            ambiguities)?.Amount;
        var date = SelectDate(normalizedText, ambiguities);
        var merchant = SelectMerchant(normalizedText, ambiguities);
        var lineItems = ParseLineItems(normalizedText, confidence, ambiguities);

        if (selectedTotal is null)
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

        if (taxAmount > selectedTotal?.Amount)
        {
            ambiguities.Add("tax_exceeds_total");
        }

        if (confidence < 0.75m)
        {
            ambiguities.Add("low_confidence");
        }

        return new NormalizedReceiptCandidate(
            "expense",
            selectedTotal?.Amount,
            currency,
            "expense.other",
            merchant,
            date,
            taxAmount,
            lineItems,
            confidence,
            ambiguities.ToArray());
    }

    private static SortedSet<string> NormalizeAmbiguities(OcrExtractionResult extraction)
    {
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

        return ambiguities;
    }

    private static decimal NormalizeConfidence(
        decimal providerConfidence,
        ISet<string> ambiguities)
    {
        if (providerConfidence is >= 0 and <= 1)
        {
            return providerConfidence;
        }

        ambiguities.Add("invalid_confidence");
        return 0;
    }

    private static string NormalizeText(string extractedText)
    {
        var lineNormalized = extractedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return string.Join(
            '\n',
            lineNormalized
                .Split('\n')
                .Select(line => HorizontalWhitespacePattern().Replace(line.Trim(), " "))
                .Where(line => line.Length > 0));
    }

    private static List<MoneyCandidate> ParseMoneyMatches(MatchCollection matches) =>
        matches
            .Cast<Match>()
            .Select(match => TryParseMoney(match, out var candidate) ? candidate : null)
            .Where(candidate => candidate is not null)
            .Cast<MoneyCandidate>()
            .Distinct()
            .ToList();

    private static bool TryParseMoney(Match match, out MoneyCandidate? candidate)
    {
        candidate = null;
        if (!decimal.TryParse(
                match.Groups["amount"].Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount < 0)
        {
            return false;
        }

        var currency = match.Groups["currency"].Success
            ? match.Groups["currency"].Value.ToUpperInvariant()
            : null;
        candidate = new MoneyCandidate(amount, currency);
        return true;
    }

    private static MoneyCandidate? SelectSingleMoneyCandidate(
        IReadOnlyCollection<MoneyCandidate> candidates,
        string ambiguityCode,
        ISet<string> ambiguities)
    {
        if (candidates.Count == 1)
        {
            return candidates.First();
        }

        if (candidates.Count > 1)
        {
            ambiguities.Add(ambiguityCode);
        }

        return null;
    }

    private static string? SelectCurrency(
        MoneyCandidate? selectedTotal,
        IReadOnlyCollection<MoneyCandidate> monetaryCandidates,
        ISet<string> ambiguities)
    {
        if (selectedTotal?.Currency is not null)
        {
            return selectedTotal.Currency;
        }

        var currencies = monetaryCandidates
            .Select(candidate => candidate.Currency)
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (currencies.Length == 1)
        {
            return currencies[0];
        }

        if (currencies.Length > 1)
        {
            ambiguities.Add("currency_ambiguous");
        }

        return null;
    }

    private static DateOnly? SelectDate(string normalizedText, ISet<string> ambiguities)
    {
        var matches = DatePattern().Matches(normalizedText).Cast<Match>().ToArray();
        var parsedDates = matches
            .Select(match =>
                DateOnly.TryParseExact(
                    match.Value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed)
                    ? parsed
                    : (DateOnly?)null)
            .ToArray();
        var validDates = parsedDates
            .Where(date => date is not null)
            .Cast<DateOnly>()
            .Distinct()
            .ToArray();
        if (parsedDates.Any(date => date is null))
        {
            ambiguities.Add("date_invalid");
        }

        if (validDates.Length == 1)
        {
            return validDates[0];
        }

        if (validDates.Length > 1)
        {
            ambiguities.Add("date_ambiguous");
        }

        return null;
    }

    private static string? SelectMerchant(string normalizedText, ISet<string> ambiguities)
    {
        var merchants = MerchantPattern()
            .Matches(normalizedText)
            .Cast<Match>()
            .Select(match => match.Groups["merchant"].Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (merchants.Length == 1)
        {
            return merchants[0];
        }

        if (merchants.Length > 1)
        {
            ambiguities.Add("merchant_ambiguous");
        }

        return null;
    }

    private static IReadOnlyList<ReceiptLineItemCandidate> ParseLineItems(
        string normalizedText,
        decimal confidence,
        ISet<string> receiptAmbiguities)
    {
        var matches = LineItemPattern().Matches(normalizedText).Cast<Match>().ToArray();
        if (matches.Length > MaximumLineItems)
        {
            receiptAmbiguities.Add("line_items_truncated");
        }

        return matches
            .Take(MaximumLineItems)
            .Select(match => CreateLineItem(match, confidence))
            .ToArray();
    }

    private static ReceiptLineItemCandidate CreateLineItem(
        Match match,
        decimal confidence)
    {
        var ambiguities = new SortedSet<string>(StringComparer.Ordinal);
        var quantity = ParseOptionalDecimal(match.Groups["quantity"], "line_item_quantity_invalid", ambiguities);
        var unitPrice = ParseOptionalDecimal(match.Groups["unit"], "line_item_unit_price_invalid", ambiguities);
        var totalAmount = ParseOptionalDecimal(match.Groups["total"], "line_item_total_invalid", ambiguities);
        var currency = match.Groups["currency"].Success
            ? match.Groups["currency"].Value.ToUpperInvariant()
            : null;

        if (quantity is null)
        {
            ambiguities.Add("line_item_quantity_missing");
        }

        if (unitPrice is null)
        {
            ambiguities.Add("line_item_unit_price_missing");
        }

        if (totalAmount is null)
        {
            ambiguities.Add("line_item_total_missing");
        }

        if (quantity is not null &&
            unitPrice is not null &&
            totalAmount is not null &&
            decimal.Round(quantity.Value * unitPrice.Value, 2) != totalAmount.Value)
        {
            ambiguities.Add("line_item_total_mismatch");
        }

        return new ReceiptLineItemCandidate(
            match.Groups["description"].Value.Trim(),
            quantity,
            unitPrice,
            totalAmount,
            currency,
            confidence,
            ambiguities.ToArray());
    }

    private static decimal? ParseOptionalDecimal(
        Group group,
        string invalidCode,
        ISet<string> ambiguities)
    {
        if (!group.Success)
        {
            return null;
        }

        if (decimal.TryParse(
                group.Value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            parsed >= 0)
        {
            return parsed;
        }

        ambiguities.Add(invalidCode);
        return null;
    }

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalWhitespacePattern();

    [GeneratedRegex(@"(?<amount>\d{1,12}(?:[.,]\d{1,2})?)\s*(?<currency>USD|EUR|GBP|UAH)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(@"^\s*(?:total|amount)\s*:\s*(?<amount>\d{1,12}(?:[.,]\d{1,2})?)\s*(?<currency>USD|EUR|GBP|UAH)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex TotalPattern();

    [GeneratedRegex(@"^\s*(?:tax|vat)\s*:\s*(?<amount>\d{1,12}(?:[.,]\d{1,2})?)\s*(?<currency>USD|EUR|GBP|UAH)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex TaxPattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\bmerchant:\s*(?<merchant>[^\r\n|]{1,120})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantPattern();

    [GeneratedRegex(@"^\s*item\s*:\s*(?<description>[^|\r\n]{1,120})(?:\s*\|\s*qty\s*:\s*(?<quantity>\d{1,6}(?:[.,]\d{1,3})?))?(?:\s*\|\s*unit\s*:\s*(?<unit>\d{1,12}(?:[.,]\d{1,2})?))?(?:\s*\|\s*total\s*:\s*(?<total>\d{1,12}(?:[.,]\d{1,2})?)(?:\s*(?<currency>USD|EUR|GBP|UAH))?)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex LineItemPattern();

    private sealed record MoneyCandidate(decimal Amount, string? Currency);
}

public sealed class DisabledOcrProviderClient : IOcrProviderClient
{
    public Task<OcrExtractionResult> ExtractAsync(
        ReadOnlyMemory<byte> receiptImage,
        string contentType,
        CancellationToken cancellationToken) =>
        Task.FromException<OcrExtractionResult>(
            new OcrProviderException(
                OcrProviderErrorCodes.ProviderUnavailable,
                isTransient: false));
}
