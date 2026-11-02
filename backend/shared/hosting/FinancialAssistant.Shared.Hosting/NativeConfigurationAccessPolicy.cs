using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FinancialAssistant.Shared.Hosting;

internal static class NativeConfigurationAccessPolicy
{
    [SupportedOSPlatform("windows")]
    internal static void Validate(FileSystemSecurity descriptor, string serviceSid)
    {
        if (!NativeServiceCatalog.IsVirtualServiceSid(serviceSid) || !descriptor.AreAccessRulesProtected ||
            descriptor.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner ||
            owner.Value is not ("S-1-5-18" or "S-1-5-32-544")) { throw Invalid(); }
        var serviceRead = false;
        var systemControl = false;
        var administratorControl = false;
        foreach (FileSystemAccessRule rule in descriptor.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.IsInherited || rule.AccessControlType != AccessControlType.Allow ||
                (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0) { throw Invalid(); }
            var sid = rule.IdentityReference.Value;
            if (sid is "S-1-5-18" or "S-1-5-32-544")
            {
                var fullControl = (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl;
                systemControl |= sid == "S-1-5-18" && fullControl;
                administratorControl |= sid == "S-1-5-32-544" && fullControl;
            }
            else
            {
                const FileSystemRights permitted = FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize;
                if (sid != serviceSid || (rule.FileSystemRights & ~permitted) != 0) { throw Invalid(); }
                serviceRead |= (rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read;
            }
        }
        if (!serviceRead || !systemControl || !administratorControl) { throw Invalid(); }
    }

    internal static void ValidateIdentity(string intendedSid, string? actualSid, string configuredSid)
    {
        if (!NativeServiceCatalog.IsVirtualServiceSid(intendedSid) || intendedSid != actualSid || intendedSid != configuredSid)
        { throw new InvalidOperationException("Native configuration requires its intended virtual service identity."); }
    }

    private static InvalidOperationException Invalid() => new("Native configuration ownership or read-only access policy is invalid.");
}
