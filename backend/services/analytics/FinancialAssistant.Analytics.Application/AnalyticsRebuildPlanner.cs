using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Analytics.Contracts;

namespace FinancialAssistant.Analytics.Application;

public sealed class AnalyticsRebuildPlanner
{
    private const int OwnerHashLength = 64;
    private const int MaximumPeriodDays = 3_650;
    private const int MaximumSourceVersionLength = 128;

    public AnalyticsRebuildPlanResponse Create(AnalyticsRebuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerScopeHash = NormalizeOwnerHash(request.OwnerScopeHash);
        if (request.PeriodStart == default ||
            request.PeriodEnd == default ||
            request.PeriodEnd < request.PeriodStart)
        {
            throw new ArgumentException(
                "A valid inclusive rebuild period is required.",
                nameof(request));
        }

        var periodDays = request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber + 1;
        if (periodDays > MaximumPeriodDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"A rebuild period cannot exceed {MaximumPeriodDays} days.");
        }

        var sourceSnapshotVersion = NormalizeSourceVersion(
            request.SourceSnapshotVersion);
        if (request.RequestedAtUtc == default ||
            request.RequestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The rebuild request timestamp must be initialized UTC.",
                nameof(request));
        }

        return new AnalyticsRebuildPlanResponse(
            AnalyticsRebuildContractVersions.V1,
            CreateJobKey(
                ownerScopeHash,
                request.PeriodStart,
                request.PeriodEnd,
                sourceSnapshotVersion),
            new AnalyticsRebuildScopeResponse(
                request.PeriodStart,
                request.PeriodEnd),
            sourceSnapshotVersion,
            AnalyticsRebuildStages.Ordered);
    }

    private static string NormalizeOwnerHash(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value)).ToLowerInvariant();
        if (normalized.Length != OwnerHashLength ||
            normalized.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Owner scope must be a SHA-256 hexadecimal hash.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeSourceVersion(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value));
        if (normalized.Length > MaximumSourceVersionLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Source snapshot version is outside the supported bounds.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string CreateJobKey(
        string ownerScopeHash,
        DateOnly periodStart,
        DateOnly periodEnd,
        string sourceSnapshotVersion)
    {
        var material = string.Join(
            '|',
            AnalyticsRebuildContractVersions.V1,
            ownerScopeHash,
            periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            sourceSnapshotVersion);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"analytics-rebuild-{Convert.ToHexString(hash).ToLowerInvariant()[..32]}";
    }
}
