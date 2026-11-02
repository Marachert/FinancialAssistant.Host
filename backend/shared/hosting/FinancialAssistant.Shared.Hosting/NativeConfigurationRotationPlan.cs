namespace FinancialAssistant.Shared.Hosting;

public sealed class NativeConfigurationRotationPlan
{
    private NativeConfigurationRotationPlan(NativeServiceConfigurationPlan previous, NativeServiceConfigurationPlan next, string[] changedCredentials)
    { PreviousRevision = previous.Revision; NextRevision = next.Revision; ChangedCredentialKeys = Array.AsReadOnly(changedCredentials); }

    public Guid PreviousRevision { get; }
    public Guid NextRevision { get; }
    public IReadOnlyList<string> ChangedCredentialKeys { get; }
    public bool RequiresControlledRestart => true;
    public bool RequiresCredentialCoordination => ChangedCredentialKeys.Count > 0;

    public static NativeConfigurationRotationPlan Create(NativeServiceConfigurationPlan previous, NativeServiceConfigurationPlan next)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(next);
        if (previous.InstallationId != next.InstallationId || previous.ServiceId != next.ServiceId || previous.ServiceSid != next.ServiceSid ||
            previous.Revision == next.Revision) { throw Invalid(); }
        var nextReferences = next.Secrets.ToDictionary(binding => binding.ConfigurationKey, binding => binding.SecretId, StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var binding in previous.Secrets)
        {
            if (!nextReferences.TryGetValue(binding.ConfigurationKey, out var reference)) { throw Invalid(); }
            if (reference == binding.SecretId) { continue; }
            if (StableReference(binding.ConfigurationKey)) { throw Invalid(); }
            changed.Add(binding.ConfigurationKey);
        }
        // Relocation, reindexing and destructive key retirement require a separately verified migration/recovery workflow.
        foreach (var key in previous.Settings.Keys.Concat(next.Settings.Keys).Where(StableSetting).Distinct(StringComparer.Ordinal))
        {
            if (!previous.Settings.TryGetValue(key, out var oldValue) || !next.Settings.TryGetValue(key, out var value) || oldValue != value) { throw Invalid(); }
        }
        return new(previous, next, changed.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    private static bool StableReference(string key) => NativeSecretMaterial.IsDataKey(key) ||
        key.EndsWith(":LookupHmacKey", StringComparison.Ordinal) || key.EndsWith(":RefreshTokenHashKey", StringComparison.Ordinal) ||
        key.EndsWith(":IdentifierHmacKey", StringComparison.Ordinal) || key.EndsWith(":UserIdHmacKey", StringComparison.Ordinal);

    private static bool StableSetting(string key) => key.EndsWith(":Storage:Postgres:Database", StringComparison.Ordinal) || key.EndsWith(":Storage:Postgres:Port", StringComparison.Ordinal) ||
        key.EndsWith(":Storage:Files:RootPath", StringComparison.Ordinal);

    private static InvalidOperationException Invalid() => new("Native configuration rotation would change ownership or retire protected data identity without a verified migration.");
}
