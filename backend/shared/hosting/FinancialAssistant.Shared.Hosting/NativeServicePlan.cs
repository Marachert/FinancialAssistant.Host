using System.Text.RegularExpressions;

namespace FinancialAssistant.Shared.Hosting;

public sealed record ExistingNativeService(string ServiceName, string InstallationId, string ExecutablePath);

public sealed record NativeServicePlan(
    string InstallationId,
    string ServiceName,
    string ServiceAccount,
    string ExecutablePath,
    string Arguments,
    string StartMode,
    IReadOnlyList<int> RecoveryRestartDelaysSeconds,
    string RecoveryAfterRepeatedFailures,
    int RecoveryResetSeconds,
    int GracefulStopSeconds,
    IReadOnlyList<string> RequiredComponents,
    bool UninstallRetainsData)
{
    public static NativeServicePlan Create(
        string serviceId,
        string releaseId,
        string installationId,
        string executableName,
        IReadOnlyList<ExistingNativeService> existingServices)
    {
        var service = NativeServiceCatalog.Get(serviceId);
        if (!Regex.IsMatch(releaseId, "\\A[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant)
            || Regex.IsMatch(releaseId, "\\A(?:con|prn|aux|nul|com[1-9]|lpt[1-9])\\z", RegexOptions.CultureInvariant)
            || !Guid.TryParseExact(installationId, "D", out var installationGuid) || installationGuid == Guid.Empty
            || !string.Equals(executableName, service.ExecutableName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Native service plan requires a bounded release ID, installation GUID and product executable basename.");
        }

        const string root = @"C:\Program Files\FinancialAssistant\releases";
        var ownedPathPattern = $"\\A{Regex.Escape(root)}\\\\[a-z0-9][a-z0-9-]{{0,63}}\\\\services\\\\{Regex.Escape(service.Id)}\\\\{Regex.Escape(executableName)}\\z";
        var matches = existingServices.Where(existing =>
            string.Equals(existing.ServiceName, service.ServiceName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1 || matches.Any(existing =>
            !string.Equals(existing.InstallationId, installationId, StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(existing.ExecutablePath, ownedPathPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)))
        {
            throw new InvalidOperationException("Conflicting service registration must not be replaced or removed.");
        }

        return new NativeServicePlan(
            installationId, service.ServiceName, service.ServiceAccount,
            $@"{root}\{releaseId}\services\{service.Id}\{executableName}",
            "--environment WindowsNative", "AutomaticDelayedStart",
            Array.AsReadOnly(new[] { 30, 60 }), "None", 86400, 30, service.RequiredComponents, true);
    }
}
