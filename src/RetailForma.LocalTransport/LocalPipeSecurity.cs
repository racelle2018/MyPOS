using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace RetailForma.LocalTransport;

public static class LocalPipeSecurity
{
    // Do not grant CreateNewInstance to clients: GENERIC_WRITE includes that dangerous right.
    public const PipeAccessRights ClientRights = PipeAccessRights.ReadData | PipeAccessRights.WriteData |
        PipeAccessRights.ReadAttributes | PipeAccessRights.WriteAttributes | PipeAccessRights.ReadExtendedAttributes |
        PipeAccessRights.WriteExtendedAttributes | PipeAccessRights.ReadPermissions | PipeAccessRights.Synchronize;
    public static SecurityIdentifier ServiceSid => (SecurityIdentifier)new NTAccount("NT SERVICE", "RetailFormaData").Translate(typeof(SecurityIdentifier));

    public static PipeSecurity Descriptor(SecurityIdentifier owner)
    {
        var security = new PipeSecurity();
        security.SetOwner(owner); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AnonymousSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        foreach (var sid in new[] { owner, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) }.Distinct())
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), ClientRights, AccessControlType.Allow));
        return security;
    }

    public static NamedPipeServerStream Listen(string name, SecurityIdentifier owner, bool firstInstance) =>
        NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 16, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | (firstInstance ? PipeOptions.FirstPipeInstance : PipeOptions.None),
            65536, 65536, Descriptor(owner), HandleInheritability.None);

    public static NamedPipeClientStream Connect(string name, SecurityIdentifier expectedOwner)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains('/')) throw new ArgumentException("Use a local pipe name.");
        var path = @"\\.\pipe\" + name;
        var handle = OpenPipe(path);
        if (handle.IsInvalid && Marshal.GetLastWin32Error() == 231)
        {
            handle.Dispose();
            if (!WaitNamedPipe(path, 5000)) throw new IOException("The RetailForma service is busy or unavailable.");
            handle = OpenPipe(path);
        }
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error(); handle.Dispose();
            throw new IOException("The RetailForma data service is unavailable. No local database fallback is permitted.", new Win32Exception(error));
        }
        var stream = new NamedPipeClientStream(PipeDirection.InOut, true, true, handle);
        try
        {
            if (!Equals(stream.GetAccessControl().GetOwner(typeof(SecurityIdentifier)), expectedOwner))
                throw new UnauthorizedAccessException("The local pipe is not owned by the trusted RetailForma service.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private static SafePipeHandle OpenPipe(string path) => CreateFile(path, (uint)ClientRights, 0, IntPtr.Zero, 3,
        0x40000000 | 0x00100000 | 0x00010000, IntPtr.Zero); // overlapped + SQOS identification (not impersonation)

    public static (string Sid, bool Elevated) Identify(NamedPipeServerStream pipe)
    {
        string? sid = null; var elevated = false;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value;
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        });
        return (sid ?? throw new UnauthorizedAccessException("The Windows client could not be identified."), elevated);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafePipeHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "WaitNamedPipeW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WaitNamedPipe(string name, uint timeout);
}
