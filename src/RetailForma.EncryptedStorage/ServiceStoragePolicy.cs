using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RetailForma.EncryptedStorage;

/// <summary>Installer/service policy definition only. Merely building this descriptor changes no ACL.</summary>
[SupportedOSPlatform("windows")]
public static class ServiceStoragePolicy
{
    public const string ServiceName = "RetailFormaData";
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RetailForma", "Data");

    public static DirectorySecurity DirectoryDescriptor(SecurityIdentifier serviceSid, bool ownerIsService = false)
    {
        ArgumentNullException.ThrowIfNull(serviceSid);
        // A single service SID, not S-1-5-80-0 (ALL SERVICES) or an arbitrary user/group.
        var parts = serviceSid.Value.Split('-');
        if (parts.Length != 9 || !serviceSid.Value.StartsWith("S-1-5-80-", StringComparison.Ordinal))
            throw new ArgumentException("A specific Windows service SID is required.");
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var descriptor = new DirectorySecurity();
        descriptor.SetOwner(ownerIsService ? serviceSid : admins);
        descriptor.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { admins, system, serviceSid })
            descriptor.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        return descriptor;
    }
}
