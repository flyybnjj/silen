# Actualizaciones de Silen 1.1.0

Al abrir normalmente la app, se consulta la última release estable de `flyybnjj/flyyoptimizer` y se descarga automáticamente `Silen-win-x64.zip` si es más reciente. El usuario pulsa **Reiniciar y actualizar** para instalar; el botón espera a que no haya análisis ni cambios en curso. Los modos de diagnóstico y prueba no consultan actualizaciones automáticamente.

## Integridad y reemplazo

La consulta pública no usa credenciales ni envía diagnósticos. Se exige una versión numérica estable, un único asset con el nombre previsto, HTTPS del repositorio oficial, tamaño permitido y SHA-256. El archivo incompleto nunca se considera listo. El ayudante vuelve a consultar la release concreta y verifica el archivo antes de reemplazarlo.

El mismo ejecutable autocontenido se copia como ayudante y recibe un trabajo protegido con DPAPI para el usuario actual. No se ejecutan scripts ni se necesita instalar otra herramienta. Se comprueban rutas, identidad del proceso, hash de la instalación anterior y versión del nuevo ejecutable. El ZIP solo puede aportar el EXE y **Archivos**; se rechazan rutas ajenas, enlaces y límites excesivos.

El reemplazo tiene un bloqueo exclusivo y usa una copia de la instalación anterior para restaurarla si una operación falla. Los informes y respaldos de ajustes en LocalAppData quedan fuera. Si no se puede restaurar por completo, se conserva la copia anterior y se indica su ubicación.

## Compatibilidad y límites

- **El primer paso de FlyyOptimizer a Silen requiere instalación manual en una carpeta nueva.** Los clientes antiguos exigen el nombre anterior del ZIP y del ejecutable. Silen exige `Silen-win-x64.zip`, `Silen-win-x64/` y `Silen.exe`; no se ha publicado un puente de actualización como parte de este rename.
- El repositorio sigue siendo `flyybnjj/flyyoptimizer`; no se sustituye por una URL sin publicar. El bloqueo interno `.flyy-update.lock` se conserva para coordinar ayudantes antiguos y nuevos.
- Los datos nuevos usan `%LocalAppData%\Silen`; una carpeta existente `%LocalAppData%\FlyyOptimizer` tiene prioridad para conservar historial y respaldos sin moverlos ni dividir sus bloqueos.
- La aplicación de actualizaciones requiere abrir Silen normalmente, sin permisos de administrador, y permisos de escritura en su carpeta portable.
- Además de GitHub, HTTPS y SHA-256, se exige `Silen-release.json` firmado con ECDSA P-256 y la clave pública incorporada. Se autentican repositorio, versión, nombre, tamaño y hash antes de descargar y nuevamente al instalar. Un paquete sin firma válida se rechaza. No hay certificado Authenticode público: esta firma de manifiesto no elimina los avisos de Windows.
- La restauración cubre excepciones durante el reemplazo y fallos al iniciar el nuevo proceso. No detecta un fallo de la app después de un inicio correcto ni recupera automáticamente cortes eléctricos durante el reemplazo.
- La caché elimina de forma conservadora paquetes y trabajos antiguos reconocidos. Los archivos bloqueados, desconocidos o fuera del límite de enumeración pueden permanecer hasta otra limpieza.

## Verificación reproducible

```powershell
dotnet test Silen.sln -c Release
.\scripts\build.ps1 -SignRelease -ReleaseTag v1.1.0
.\scripts\verify-portable.ps1 -RequireSignedRelease -ReleaseTag v1.1.0
```

Las pruebas usan HTTP simulado y carpetas aisladas para versiones, destinos, cancelación, límites, hashes, ZIP hostiles, archivos bloqueados y restauración. Las pruebas de interfaz sustituyen descarga y lanzamiento por fixtures. El smoke portable ejecuta el ZIP extraído con PATH limitado a System32 y sin búsqueda del SDK, sin aplicar ajustes reales ni instalar actualizaciones sobre la app en uso.

También se comprobó una descarga pública real de v1.0 con el motor Release: **63.297.009 bytes**, SHA-256 `0627CCE3096C8D64485BD56747EEA7A117D98C03C8A3409852D4134900707016`, sin token y sin instalar el paquete.

Referencias técnicas: [releases de la API oficial de GitHub](https://docs.github.com/en/rest/releases/releases) y [assets y digest](https://docs.github.com/en/rest/releases/assets).

Verificación final de servicios en Release: **396 pruebas correctas, 0 fallidas y 1 omitida** (consulta OEM de red opcional). La interfaz pasó **125 comprobaciones deterministas**, incluidas descarga automática, reintento, cancelación y cierre con limpieza, reinicio explícito, bloqueo durante otras tareas y estados a 1000×680. Estas comprobaciones no aplican ajustes reales ni lanzan un ayudante real.
