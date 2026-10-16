# Corrección de permisos de Edge — incluida en 1.1.0

El reporte mostraba «Reducir procesos de Edge al cerrarlo» y «Windows denegó el acceso». Silen seleccionaba el ajuste automáticamente aunque no pudiera escribir en `HKCU\Software\Policies\Microsoft\Edge`. El resto del perfil podía completarse, pero el resultado global se mostraba como incompleto.

La vista previa comprueba ahora el acceso de escritura a las claves que necesitan cambios. Cuando una clave no existe, comprueba su antecesor existente más cercano sin crear claves ni valores. Una denegación detiene la comprobación: no se intenta usar un antecesor más permisivo para eludirla. Un ajuste que ya tiene todos sus valores de destino no necesita escritura.

Los ajustes restringidos quedan desactivados y sin selección automática, con una explicación en Gaming. Antes de crear un respaldo para estos grupos se vuelven a comprobar los permisos. Si los permisos cambian durante una operación, el error y los respaldos de cambios parciales siguen disponibles; los demás pasos independientes continúan.

La app conserva las ACL, las políticas y los respaldos existentes. Esta corrección no solicita elevación ni declara aplicado un ajuste que Windows bloquea.

La regresión reproduce las políticas de Edge denegadas, la finalización de los otros pasos, la pérdida de permisos después de preparar el perfil, las claves inexistentes, la denegación en una clave existente y los valores ya correctos sin acceso de escritura. Las pruebas de aplicación usan una plataforma de registro simulada. La verificación portable solo consulta el equipo y renderiza la interfaz.

Referencia de los permisos: [Microsoft: seguridad y derechos de acceso del registro](https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-key-security-and-access-rights) y [RegistryKey.OpenSubKey](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.opensubkey?view=net-10.0).
