using System.Text.RegularExpressions;

namespace FinancialAssistant.Identity.Application.Phone;

internal static partial class PhoneNumberNormalizer
{
    [GeneratedRegex("^\\+[1-9][0-9]{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164Regex();

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (!E164Regex().IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static string Mask(string normalized)
    {
        var visible = Math.Min(4, normalized.Length - 2);
        var prefix = normalized[..2];
        var suffix = normalized[^visible..];
        return $"{prefix}{new string('*', normalized.Length - prefix.Length - suffix.Length)}{suffix}";
    }
}
