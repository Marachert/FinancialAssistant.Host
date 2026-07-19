using System.Globalization;
using System.Text.RegularExpressions;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Parsing;

public sealed partial class DeterministicTransactionInputParser : ITransactionInputParser
{
    public Task<ParsedTransactionCandidate> ParseAsync(
        string normalizedInput,
        DateOnly currentDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var type = DetectType(normalizedInput);
        var (amount, currency) = DetectAmountAndCurrency(normalizedInput);
        var date = DetectDate(normalizedInput, currentDate);
        var merchant = DetectMerchant(normalizedInput);
        var categoryId = DetectCategory(normalizedInput, type);
        var confidence = CalculateConfidence(type, amount, currency, date, categoryId, merchant);

        return Task.FromResult(
            new ParsedTransactionCandidate(
                type,
                amount,
                currency,
                categoryId,
                merchant,
                date,
                confidence));
    }

    private static string DetectType(string input)
    {
        if (TransferPattern().IsMatch(input))
        {
            return TransactionTypes.Transfer;
        }

        if (IncomePattern().IsMatch(input))
        {
            return TransactionTypes.Income;
        }

        if (ExpensePattern().IsMatch(input))
        {
            return TransactionTypes.Expense;
        }

        return TransactionTypes.Unknown;
    }

    private static (decimal? Amount, string? Currency) DetectAmountAndCurrency(string input)
    {
        var match = AmountPattern().Match(input);
        if (!match.Success)
        {
            return (null, null);
        }

        var rawAmount = match.Groups["amount"].Value.Replace(',', '.');
        if (!decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return (null, null);
        }

        var code = match.Groups["code"].Value.ToUpperInvariant();
        var symbol = match.Groups["symbol"].Value;
        var currency = code.Length > 0
            ? code
            : symbol switch
            {
                "$" => "USD",
                "\u20AC" => "EUR",
                "\u00A3" => "GBP",
                "\u20B4" => "UAH",
                _ => null
            };

        return (amount, currency);
    }

    private static DateOnly? DetectDate(string input, DateOnly currentDate)
    {
        if (TodayPattern().IsMatch(input))
        {
            return currentDate;
        }

        if (YesterdayPattern().IsMatch(input))
        {
            return currentDate.AddDays(-1);
        }

        var match = IsoDatePattern().Match(input);
        return match.Success &&
            DateOnly.TryParseExact(
                match.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
            ? date
            : null;
    }

    private static string? DetectMerchant(string input)
    {
        var match = MerchantPattern().Match(input);
        return match.Success ? match.Groups["merchant"].Value.Trim() : null;
    }

    private static string? DetectCategory(string input, string type)
    {
        if (type == TransactionTypes.Income)
        {
            return SalaryPattern().IsMatch(input) ? "income.salary" : "income.other";
        }

        if (type != TransactionTypes.Expense)
        {
            return null;
        }

        if (FoodPattern().IsMatch(input))
        {
            return "expense.food";
        }

        if (HousingPattern().IsMatch(input))
        {
            return "expense.housing";
        }

        if (TransportPattern().IsMatch(input))
        {
            return "expense.transportation";
        }

        return "expense.other";
    }

    private static decimal CalculateConfidence(
        string type,
        decimal? amount,
        string? currency,
        DateOnly? date,
        string? categoryId,
        string? merchant)
    {
        var confidence = 0.15m;
        confidence += type == TransactionTypes.Unknown ? 0 : 0.25m;
        confidence += amount is null ? 0 : 0.25m;
        confidence += currency is null ? 0 : 0.10m;
        confidence += date is null ? 0 : 0.10m;
        confidence += categoryId is null ? 0 : 0.05m;
        confidence += merchant is null ? 0 : 0.05m;
        return Math.Min(confidence, 0.95m);
    }

    [GeneratedRegex(@"\b(?:transfer|transferred|move|moved)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransferPattern();

    [GeneratedRegex(@"\b(?:income|salary|paycheck|received|earned|refund)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncomePattern();

    [GeneratedRegex(@"\b(?:expense|spent|paid|bought|purchase|cost)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpensePattern();

    [GeneratedRegex(@"(?<![\w-])(?:(?<symbol>\$|\u20AC|\u00A3|\u20B4)\s*)?(?<amount>\d{1,12}(?:[.,]\d{1,2})?)(?![\d-])(?:\s*(?<code>USD|EUR|GBP|UAH))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\btoday\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TodayPattern();

    [GeneratedRegex(@"\byesterday\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YesterdayPattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDatePattern();

    [GeneratedRegex(@"\bat\s+(?<merchant>[\p{L}\p{N}][\p{L}\p{N} .&'-]{0,79}?)(?=\s+(?:today|yesterday|on\s+\d{4}-\d{2}-\d{2})\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantPattern();

    [GeneratedRegex(@"\b(?:salary|paycheck|wages)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SalaryPattern();

    [GeneratedRegex(@"\b(?:food|grocery|groceries|coffee|cafe|restaurant)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FoodPattern();

    [GeneratedRegex(@"\b(?:rent|mortgage|housing)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HousingPattern();

    [GeneratedRegex(@"\b(?:fuel|taxi|transport|bus|train)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransportPattern();
}
