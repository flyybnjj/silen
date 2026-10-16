using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Silen.Windows;

/// <summary>Solo en el ayudante sin WPF y antes del primer objeto COM/WMI.</summary>
internal static class SystemRestoreComSecurity
{
    // Callback local mínimo para los principales documentados por Microsoft. Sin Everyone.
    internal static byte[] BuildAccessList()
    {
        var acl = new RawAcl(2, 5);
        foreach (var sid in new[] { WellKnownSidType.BuiltinAdministratorsSid, WellKnownSidType.LocalServiceSid,
            WellKnownSidType.NetworkServiceSid, WellKnownSidType.SelfSid, WellKnownSidType.LocalSystemSid })
            acl.InsertAce(acl.Count, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, 3, new SecurityIdentifier(sid, null), false, null));
        var bytes = new byte[acl.BinaryLength]; acl.GetBinaryForm(bytes, 0); return bytes;
    }
    internal static IDisposable Initialize()
    {
        var hr = CoInitializeEx(IntPtr.Zero, 0); // MTA; se equilibra también S_FALSE.
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            var admin = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var sid = new byte[admin.BinaryLength]; admin.GetBinaryForm(sid, 0);
            var acl = BuildAccessList();
            var descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<AbsoluteSecurityDescriptor>());
            var aclPointer = Marshal.AllocHGlobal(acl.Length);
            var sidPointer = Marshal.AllocHGlobal(sid.Length);
            try
            {
                Marshal.Copy(acl, 0, aclPointer, acl.Length); Marshal.Copy(sid, 0, sidPointer, sid.Length);
                if (!InitializeSecurityDescriptor(descriptor, 1) || !SetSecurityDescriptorOwner(descriptor, sidPointer, false) ||
                    !SetSecurityDescriptorGroup(descriptor, sidPointer, false) || !SetSecurityDescriptorDacl(descriptor, true, aclPointer, false))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                // Descriptor absoluto requerido por CoInitializeSecurity. Autenticación privada,
                // identificación y sin activación como activador ni marshaling personalizado.
                hr = CoInitializeSecurity(descriptor, -1, IntPtr.Zero, IntPtr.Zero, 6, 2, IntPtr.Zero, 0x1000 | 0x2000, IntPtr.Zero);
                Marshal.ThrowExceptionForHR(hr); // RPC_E_TOO_LATE también detiene el preflight.
            }
            finally { Marshal.FreeHGlobal(sidPointer); Marshal.FreeHGlobal(aclPointer); Marshal.FreeHGlobal(descriptor); }
            return new ComLease();
        }
        catch { CoUninitialize(); throw; }
    }
    private sealed class ComLease : IDisposable { public void Dispose() => CoUninitialize(); }
    [StructLayout(LayoutKind.Sequential)]
    private struct AbsoluteSecurityDescriptor
    {
        public byte Revision, Reserved;
        public ushort Control;
        public IntPtr Owner, Group, Sacl, Dacl;
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint apartment);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoInitializeSecurity(IntPtr descriptor, int services, IntPtr authenticationServices, IntPtr reserved1, uint authenticationLevel, uint impersonationLevel, IntPtr authenticationList, uint capabilities, IntPtr reserved3);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeSecurityDescriptor(IntPtr descriptor, uint revision);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSecurityDescriptorOwner(IntPtr descriptor, IntPtr owner, [MarshalAs(UnmanagedType.Bool)] bool defaulted);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSecurityDescriptorGroup(IntPtr descriptor, IntPtr group, [MarshalAs(UnmanagedType.Bool)] bool defaulted);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSecurityDescriptorDacl(IntPtr descriptor, [MarshalAs(UnmanagedType.Bool)] bool present, IntPtr dacl, [MarshalAs(UnmanagedType.Bool)] bool defaulted);
}
