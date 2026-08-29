using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MobileAccessibilityLocalizationTests
{
    [Fact]
    public void LocalizationCatalogs_HaveTypedEnglishAndUkrainianKeyParity()
    {
        var catalogs = ReadRequiredFile("mobile/app-react-native/src/localization/catalogs.ts");
        var english = Between(catalogs, "englishMessages = {", "} as const;");
        var ukrainian = Between(catalogs, "ukrainianMessages: Record<MessageKey, string> = {", "};");

        var englishKeys = MessageKeys(english);
        var ukrainianKeys = MessageKeys(ukrainian);

        Assert.NotEmpty(englishKeys);
        Assert.Equal(englishKeys, ukrainianKeys);
        Assert.Contains("home.overview", englishKeys);
        Assert.Contains("notifications.itemLabel", englishKeys);
    }

    [Fact]
    public void KeyScreens_UseCatalogsAndProfileAwareFinancialFormatting()
    {
        var auth = ReadRequiredFile("mobile/app-react-native/src/features/auth/AuthForm.tsx");
        var home = ReadRequiredFile("mobile/app-react-native/src/app/(app)/home.tsx");
        var analytics = ReadRequiredFile("mobile/app-react-native/src/app/(app)/analytics.tsx");
        var notifications = ReadRequiredFile("mobile/app-react-native/src/app/(app)/notifications.tsx");
        var score = ReadRequiredFile("mobile/app-react-native/src/app/(app)/score.tsx");
        var localization = ReadRequiredFile("mobile/app-react-native/src/localization/localization.ts");

        foreach (var screen in new[] { auth, home, analytics, notifications })
        {
            Assert.Contains("useLocalization(", screen, StringComparison.Ordinal);
        }

        Assert.Contains("formatCurrency(summary.expenseTotal, dashboard.currency, locale)", home, StringComparison.Ordinal);
        Assert.Contains("formatDateOnly(breakdown.periodStart, locale)", analytics, StringComparison.Ordinal);
        Assert.Contains("formatDateTime(item.preparedAtUtc, locale)", notifications, StringComparison.Ordinal);
        Assert.Contains("formatDateTime(item.calculatedAtUtc, locale", score, StringComparison.Ordinal);
        Assert.Contains("Intl.getCanonicalLocales", localization, StringComparison.Ordinal);
        Assert.DoesNotContain("Intl.NumberFormat(undefined", home + analytics, StringComparison.Ordinal);
        Assert.DoesNotContain("Intl.DateTimeFormat(undefined", notifications + score, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedControls_ExposeLabelsStatesAndMinimumTouchTargets()
    {
        var ui = ReadRequiredFile("mobile/app-react-native/src/shared/ui.tsx");

        Assert.True(Regex.Matches(ui, "accessibilityLabel=\\{label\\}").Count >= 4);
        Assert.Contains("accessibilityState={{ busy: loading, disabled: unavailable }}", ui, StringComparison.Ordinal);
        Assert.Contains("accessibilityRole=\"radiogroup\"", ui, StringComparison.Ordinal);
        Assert.Contains("accessibilityRole=\"radio\"", ui, StringComparison.Ordinal);
        Assert.Contains("accessibilityLiveRegion=\"polite\"", ui, StringComparison.Ordinal);
        Assert.Contains("linkButton: { minHeight: 44", ui, StringComparison.Ordinal);
        Assert.Contains("primaryButton: { minHeight: 48", ui, StringComparison.Ordinal);
        Assert.Contains("secondaryButton: { minHeight: 48", ui, StringComparison.Ordinal);
        Assert.Contains("segment: { minHeight: 46", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeForegroundPairs_MeetNormalTextContrastBaseline()
    {
        var theme = ReadRequiredFile("mobile/app-react-native/src/app/theme.ts");
        var colors = Regex.Matches(theme, "(?<name>[a-zA-Z]+): '#(?<hex>[0-9A-Fa-f]{6})'")
            .ToDictionary(
                match => match.Groups["name"].Value,
                match => match.Groups["hex"].Value,
                StringComparer.Ordinal);

        AssertContrast(colors, "textPrimary", "canvas", 4.5);
        AssertContrast(colors, "textPrimary", "surface", 4.5);
        AssertContrast(colors, "textSecondary", "surface", 4.5);
        AssertContrast(colors, "action", "surface", 4.5);
        AssertContrast(colors, "onAction", "action", 4.5);
        AssertContrast(colors, "info", "surface", 4.5);
        AssertContrast(colors, "positive", "surface", 4.5);
        AssertContrast(colors, "warning", "surface", 4.5);
        AssertContrast(colors, "critical", "surface", 4.5);
    }

    private static void AssertContrast(
        IReadOnlyDictionary<string, string> colors,
        string foreground,
        string background,
        double minimum)
    {
        var light = Math.Max(Luminance(colors[foreground]), Luminance(colors[background]));
        var dark = Math.Min(Luminance(colors[foreground]), Luminance(colors[background]));
        var ratio = (light + 0.05) / (dark + 0.05);
        Assert.True(
            ratio >= minimum,
            $"{foreground} on {background} contrast {ratio:F2}:1 is below {minimum:F1}:1.");
    }

    private static double Luminance(string hex)
    {
        var channels = Enumerable.Range(0, 3)
            .Select(index => int.Parse(
                hex.AsSpan(index * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture) / 255d)
            .Select(value => value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4))
            .ToArray();
        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
    }

    private static string[] MessageKeys(string catalog) => Regex
        .Matches(catalog, "'(?<key>[a-zA-Z0-9.]+)':")
        .Select(match => match.Groups["key"].Value)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string Between(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing catalog marker: {start}");
        startIndex += start.Length;
        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex >= 0, $"Missing catalog marker: {end}");
        return value[startIndex..endIndex];
    }

    private static string ReadRequiredFile(string path)
    {
        var root = FindRepositoryRoot();
        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-187 file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
