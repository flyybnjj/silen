# Herramientas nativas autocontenidas

Implementación independiente en `Silen.Windows`, sin ejecutar PowerShell, WinUtil, Optimizer ni instaladores externos. `NativeOptimizationService` implementa el contrato compartido `INativeOptimizationService`; las llamadas trabajan fuera del hilo de WPF. El análisis no aplica estas acciones: se ejecutan únicamente al seleccionar un control.

## Preferencias e inicio

- Modo Juego: valores DWORD del usuario `HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled` y `AllowAutoGameMode`. Se leen y cambian juntos, conservando cada valor previo por separado.
- Capturas Xbox: valores del usuario `AppCaptureEnabled` en `Software\Microsoft\Windows\CurrentVersion\GameDVR` y `GameDVR_Enabled` en `System\GameConfigStore`. Solo se cambia esta preferencia; no se desinstala Game Bar ni se alteran políticas globales. La disponibilidad efectiva de captura también depende de Windows, sus políticas y Game Bar.
- Animaciones: API `SystemParametersInfoW`, `SPI_GETCLIENTAREAANIMATION` y `SPI_SETCLIENTAREAANIMATION`, con persistencia y notificación de la preferencia. El alcance son las animaciones del área cliente; no se promete desactivar todos los efectos visuales.
- Energía: `PowerEnumerate`, `PowerReadFriendlyName`, `PowerGetActiveScheme` y `PowerSetActiveScheme`. Solo se permite seleccionar esquemas ya instalados, incluidos los del OEM; no se crean ni restablecen planes.
- Inicio: exclusivamente valores string/expand-string de `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` para el usuario actual. Deshabilitar elimina ese valor; habilitar restaura su dato y tipo exactos desde el respaldo. El comando nunca se ejecuta. No se enumeran tareas, servicios, claves de otros usuarios ni `StartupApproved`: el estado mostrado describe la presencia de esta entrada Run, no todos los mecanismos de arranque de Windows.

Las preferencias ausentes, con tipos inesperados o valores mixtos se muestran como desconocidas, sin inferir el default de Windows. Una acción explícita puede establecerlas conservando la ausencia o tipo/dato previos. Si la API de animaciones no permite leer su estado previo se rechaza la modificación.

## Respaldo y recuperación

El journal local vive en `optimization\optimizations.json` bajo la raíz de datos seleccionada: `%LocalAppData%\Silen` para instalaciones nuevas, o `%LocalAppData%\FlyyOptimizer` si esa carpeta ya existe. Antes de la primera mutación se publica un respaldo durable que contiene los valores anteriores e intencionados y estado `Pending`. La escritura usa archivo temporal en el mismo directorio, flush a disco y reemplazo atómico. Un lease de archivo compartido entre procesos serializa inspecciones, cambios y deshacer. Se conserva el archivo dañado y se bloquean nuevas mutaciones cuando el contenido no es válido; los destinos de registro están limitados y se rechazan destinos duplicados.

Cada valor se vuelve a consultar antes de escribir y después se comprueba el resultado. Una escritura parcial, cancelación o fallo al publicar el estado final conserva el respaldo `Pending`/`Partial`; otra instancia puede recuperar los valores aplicados. Deshacer comprueba todos los valores antes de comenzar y los vuelve a comprobar individualmente antes de restaurarlos. Si un valor actual no coincide con el valor anterior ni con el intencionado, no se sobrescribe el cambio externo. Solo se restauran los valores que pertenecen a la acción: nunca se elimina una clave completa ni se alteran entradas no seleccionadas. Los valores de registro ausentes vuelven a quedar ausentes.

## Temporales

La ruta de producción es fija: `%LocalAppData%\Temp` del usuario actual, sin depender de un `TEMP` arbitrario ni aceptar rutas de la interfaz. La vista previa es de solo lectura; ofrece archivos con última escritura de al menos 24 horas, identificadores opacos válidos solo para esa vista previa y nombres relativos. Se limita a 5.000 candidatos y 50.000 entradas recorridas, con indicador de truncación.

No se recorren junctions/reparse points ni se borran directorios. Antes de eliminar se revalidan el alcance, todos los ancestros, atributos, tamaño, fecha de escritura y creación, edad y ruta final resuelta de un handle Win32. El handle solicita eliminación y no comparte escritura ni eliminación, por lo que la comprobación y la eliminación se refieren al mismo archivo estable. Se omiten archivos ocupados, cambiados, enlazados o inaccesibles. Los hardlinks con más de un enlace también se omiten para evitar anunciar espacio recuperado cuando otro enlace conserva sus datos.

Solo se procesan los IDs seleccionados. Una vista previa nueva invalida los IDs anteriores. La cancelación conserva los resultados parciales. No se limpia al abrir la app.

### Recuperación y purga (16 de octubre de 2026)

La retirada ya no elimina definitivamente: antes de mover cada archivo se publica un manifiesto duradero en `.silen-recovery`, dentro del temporal autorizado. Un renombrado por handle mantiene el archivo en el mismo volumen; los ancestros y el directorio de destino permanecen bloqueados contra sustituciones. Se excluye esa carpeta de futuras vistas previas. Retirar archivos informa bytes recuperables, no bytes liberados.

En Ajustes, selecciona los archivos recuperables para restaurarlos o eliminarlos definitivamente. Restaurar revalida manifiesto, identidad y ruta; nunca sobrescribe un archivo existente. Purgar requiere otra confirmación y libera solo los bytes de los archivos efectivamente eliminados. Bloom no tiene herramienta de purga. Windows u otros limpiadores pueden vaciar la recuperación temporal: no sustituye un respaldo permanente.

### Privilegios y respaldos del sistema

UI y Bloom se ejecutan con el usuario normal. El ayudante elevado acepta una acción tipada y parámetros limitados, autentica el proceso y canal local y termina al completarla. La desconexión cancela el lote antes del siguiente paso. Los respaldos privilegiados se guardan bajo `%ProgramData%\Silen\privileged\{SID}` con propietario y ACL comprobados, sin enlaces. Los journals editables heredados no autorizan restauraciones elevadas. **Cargar respaldos del sistema** solicita autorización para consultarlos; los cambios del usuario conservan su restauración local.

## Verificación

31 pruebas dirigidas pasan: 18 de preferencias, energía, inicio, journal, restauración, conflictos y concurrencia; 13 de limpieza. El ciclo RED → GREEN cubre el servicio inicialmente ausente, dos claves de Modo Juego, journal con destinos duplicados, limpieza ausente y rechazo de hardlinks. Las pruebas de mutación de preferencias usan un proveedor de fixture; las de archivos y junctions NTFS usan únicamente directorios propios bajo `SilenTests`. No se modificaron preferencias reales, planes ni entradas de inicio del equipo, ni se limpiaron sus temporales.

Los tests verifican respaldo antes de la primera escritura, fallo después de una escritura recuperable desde otra instancia, restauración exacta de expand-string/ausencia, conflicto externo sin restauración parcial, dos instancias conservando ambos cambios, no-op sin nuevo journal, pre-cancelación, archivo ocupado/cambiado, IDs ajenos, límites, junctions y sustitución de ancestro, hardlinks y contabilidad de cancelación parcial. La suite completa y el smoke publicado pertenecen a la verificación de integración.

Referencias primarias consultadas el 14 de octubre de 2026: [SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow), [PowerEnumerate](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerenumerate), [PowerReadFriendlyName](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadfriendlyname), y [configuración de WinUtil](https://github.com/ChrisTitusTech/winutil/blob/main/config/tweaks.json) como referencia de preferencias de Windows. No se copió ni se ejecuta código de WinUtil.
