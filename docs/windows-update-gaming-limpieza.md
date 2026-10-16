# Windows Update, Gaming general y limpieza directa

Paquete del 15 de octubre de 2026. Se conserva la identidad visual existente.

## Windows Update

`WindowsUpdateDiagnostics` comparte el diagnóstico entre `WindowsCollector`,
`DriverUpdateService` y Drivers. Comprueba las seis fechas locales de pausa en
`HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings`, las políticas GPO/MDM,
los estados efectivos de pausa y la configuración/ejecución de `wuauserv`.
Una lectura fallida se presenta como desconocida. Un bloqueo no se confunde con
una búsqueda correcta sin drivers. La consulta comprueba el estado antes y después
de llamar a Windows Update Agent.

Reanudar es una acción explícita. Requiere permisos, registra primero los originales
en `OptimizationJournal`, retira las fechas locales cuando corresponde y activa
el servicio. Si estaba deshabilitado, cambia a inicio manual antes de iniciarlo.
No elimina políticas de organización ni escribe indicadores efectivos de Windows.
La restauración conserva presencia/ausencia, tipo y valor de cada entrada, tipo
de inicio y estado de ejecución del servicio. Ante un cambio externo incompatible,
Undo informa el conflicto sin sobrescribirlo.

La comprobación posterior debe confirmar que no queda pausa efectiva y que el
servicio está en ejecución. Si Windows sigue informando una pausa, la acción no
se anuncia como exitosa; los cambios parciales conservan su respaldo.
`IAutomaticUpdates.Resume` no se usa: la API está obsoleta y no hace nada en
Windows 10 y posteriores.

## Gaming y acciones principales

Gaming funciona sin Fortnite. Expone Modo Juego, capturas, un límite opcional
para tráfico de actualizaciones en segundo plano, energía y preferencia de GPU
para un ejecutable local elegido. La GPU efectiva depende de Windows y del hardware.
No se modifica la prioridad global de todos los procesos.

El límite de Delivery Optimization fija `DOPercentageMaxBackgroundBandwidth=10`:
puede reducir congestión de descargas de Windows Update/Store, no limita otros
launchers ni garantiza menor ping. Requiere administrador y guarda su estado
previo. No se aplican valores TCP indiscriminadamente a todas las interfaces.

Fortnite solo aparece instalado, en un apartado inicialmente plegado. Su botón
aplica exclusivamente las opciones visibles del juego, previa confirmación.
La acción principal **Optimizar PC** permanece en Resumen; no se duplica en Gaming.
Se retira el atajo de restauración de Gaming. Las exportaciones quedan en Historial.

Temporales usa un solo botón con el tamaño calculado. La acción obtiene una lista
fresca y el motor revalida identidad, antigüedad y ubicación de cada archivo antes
de borrarlo. Omite enlaces, archivos en uso o cambiados. El resultado muestra
espacio recuperado, eliminados y omitidos. Un cálculo cancelado no habilita una
limpieza con tamaño desconocido; actualizar permite reintentarlo. La eliminación
de archivos no tiene deshacer y se indica antes de ejecutarla.

## Verificación

SDK usado: `%USERPROFILE%\.codex\runtimes\dotnet-10-sdk\dotnet.exe`.

- `dotnet test Silen.sln -c Release`: 479 pruebas correctas, cero fallos;
  una prueba opcional del catálogo Lenovo en vivo omitida.
- `scripts/build.ps1 -SkipTests`: solución y publicación autocontenida para
  Windows x64, cero errores y cero advertencias.
- `scripts/verify-portable.ps1`: 30 archivos coinciden con el ZIP; arranque desde
  una extracción nueva con espacios en la ruta y sin SDK en PATH; 164 comprobaciones
  de interacción correctas. Evidencia local:
  `artifacts/portable verification d4ce6b51749a4025a7a1bd2cbdcecda6/smoke/status.json`.
- Preversión copiada al escritorio: los 30 archivos coinciden con la publicación;
  acceso directo verificado y carpeta anterior conservada. SHA-256 del ejecutable:
  `BE556B1C75EAE47B07D8E27BD547062264C4213124B283C68480C7C6F81B209F`.
- Las pruebas del motor cubren pausa efectiva, fechas vencidas, servicio detenido
  o deshabilitado, permisos, respaldo/Undo, conflictos externos, GPU genérica y
  restauración del límite de tráfico.
- Las pruebas WPF cubren el estado de Drivers, reanudación explícita y Undo,
  Gaming sin Fortnite, perfil instalado plegado, selección limitada al juego,
  limpieza directa, cancelación del cálculo y ausencia de botones redundantes.

Las mutaciones se prueban con plataformas y archivos controlados: no se repara
Windows Update, cambia la GPU, limita la red ni limpia temporales del usuario
durante la verificación. La convergencia real de una pausa local en Windows 11
tras retirar sus fechas sigue requiriendo una prueba explícita en un equipo de
pruebas; el código informa fallo si el estado efectivo continúa pausado.

La revisión independiente no encontró hallazgos demostrables adicionales en
journal, restauración, separación del perfil Fortnite, GPU o limpieza directa.

## Referencias técnicas

- [Configuración y estados efectivos de Windows Update](https://learn.microsoft.com/en-us/windows/deployment/update/waas-configure-wufb).
- [IAutomaticUpdates.Resume](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iautomaticupdates-resume).
- [Opciones de Delivery Optimization](https://learn.microsoft.com/en-us/windows/deployment/do/waas-delivery-optimization-reference).
