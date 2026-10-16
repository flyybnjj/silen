using System.Security.AccessControl;
using System.Security.Principal;

namespace Silen.Windows;

internal static class ProtectedOptimizationDirectory
{
    internal static string OpenForCurrentAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("El respaldo del sistema requiere el ejecutor autorizado.");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        EnsureDirectory(root, create: false);
        var silen = Path.Combine(root, "Silen");
        EnsureDirectory(silen, create: true);
        var privileged = Path.Combine(silen, "privileged");
        EnsureDirectory(privileged, create: true);
        var path = Path.Combine(privileged, identity.User!.Value);
        EnsureDirectory(path, create: true);
        return path;
    }

    private static void EnsureDirectory(string path, bool create)
    {
        var info = new DirectoryInfo(path);
        if (!info.Exists && create)
        {
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            acl.SetOwner(admins);
            foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            info.Create(acl);
            info.Refresh();
        }
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new UnauthorizedAccessException("La carpeta de respaldos del sistema no es segura.");
        if (!create) return; // ProgramData es una raíz de Windows, no se modifica su ACL.
        ValidateAcl(info.GetAccessControl());
        foreach (var item in info.EnumerateFileSystemInfos())
        {
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("El respaldo del sistema contiene un enlace no permitido.");
            if (item is FileInfo file) ValidateAcl(file.GetAccessControl());
        }
    }

    internal static void ProtectNewFile(FileStream stream)
    {
        var acl = new FileSecurity();
        acl.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        acl.SetOwner(admins);
        foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        stream.SetAccessControl(acl);
    }

    internal static void ValidateAcl(FileSystemSecurity acl)
    {
        static bool Trusted(IdentityReference sid) => sid.Value is "S-1-5-18" or "S-1-5-32-544";
        if (!Trusted(acl.GetOwner(typeof(SecurityIdentifier))!))
            throw new UnauthorizedAccessException("El respaldo del sistema tiene un propietario no autorizado. Se conservó sin utilizarlo.");
        const FileSystemRights writes = FileSystemRights.Write | FileSystemRights.Modify | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !Trusted(rule.IdentityReference) && (rule.FileSystemRights & writes) != 0)
                throw new UnauthorizedAccessException("El respaldo del sistema permite escritura sin privilegios. Se conservó sin utilizarlo.");
    }
}
