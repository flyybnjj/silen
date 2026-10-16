# Referencias para Mejoras y Gaming

Investigación del 14 de octubre de 2026 a partir de la petición de buscar opciones como Optimizer y Chris Titus Tools. Este documento propone funciones; no añade esas funciones al ejecutable existente.

## Situación actual de Silen

Mejoras muestra hallazgos y abre Configuración de Windows mediante `WindowsActions`. Gaming importa CSV o captura con una consola PresentMon que el usuario debe conseguir y seleccionar. Son herramientas de diagnóstico y orientación; falta un flujo que aplique opciones seleccionadas, compruebe el resultado y permita restaurar ajustes.

## Aplicaciones de referencia

| Proyecto | Qué aporta | Uso recomendado |
| --- | --- | --- |
| [Chris Titus WinUtil](https://github.com/ChrisTitusTech/winutil) | Ajustes individuales, presets, WinGet, limpieza y herramientas de reparación. | Principal referencia funcional: opciones seleccionables, estado y ejecución por acción. |
| [Optimizer](https://github.com/hellzerg/optimizer) / [OptimizerNXT](https://github.com/hellzerg/optimizerNXT) | Inicio, limpieza, red, opciones de Windows y automatización. | Referencia de funciones concretas. El primero fue archivado el 20 de enero de 2026 y remite al segundo, que es una CLI YAML. |
| [UniGetUI](https://github.com/Devolutions/UniGetUI) | Buscar, instalar, actualizar y desinstalar software mediante gestores de paquetes. | Referencia para una lista de apps con versión instalada/disponible y acciones por selección. |
| [Autoruns de Microsoft](https://learn.microsoft.com/en-us/sysinternals/downloads/autoruns) | Inventario amplio de inicio automático y desactivación de entradas. | Referencia para mostrar origen y editor antes de elegir qué desactivar. |

## Primera tanda recomendada

**Mejoras:** activar/desactivar entradas de inicio elegidas con copia recuperable; elegir entre planes de energía existentes; reducir animaciones como preferencia visual; actualizar apps seleccionadas mediante WinGet. La limpieza temporal debe mostrar antes qué archivos y espacio se eliminarían, y después bytes realmente eliminados/omitidos. La limpieza y las actualizaciones no deben anunciar deshacer universal.

**Gaming:** controles separados para Modo Juego y capturas Xbox; perfil de sesión que guarde y restaure el plan de energía anterior, considerando batería/cargador en portátil; medición de latencia/variación/pérdida de red; captura PresentMon guiada que quite la necesidad de localizar manualmente su ejecutable. La comparación de FPS, 1% low y frame times sigue siendo la comprobación de rendimiento, con escenas equivalentes.

Cada ajuste necesita estado actual, alcance, selección, aplicar, lectura de comprobación, resultado y restauración del estado previo exacto cuando sea reversible. Guardar tipo/valor/existencia del registro y GUID activo; un punto de restauración complementa ese registro, pero no sustituye una copia por acción.

## Diferencias del código que conviene tener presentes

- WinUtil actual tiene presets Standard/Minimal/Advanced; su FAQ de gaming conserva referencias antiguas a Desktop. Fuente vigente: [preset.json](https://github.com/ChrisTitusTech/winutil/blob/main/config/preset.json). La investigación fijó WinUtil a [07ccd8e2](https://github.com/ChrisTitusTech/winutil/tree/07ccd8e2e755a706f31569808b31f5b77acad6a9).
- Desactivar Ultimate Performance en WinUtil llama `/restoredefaultschemes`; Silen debería restaurar solo el GUID que estaba activo. Fuente: [Invoke-WPFUltimatePerformance.ps1](https://github.com/ChrisTitusTech/winutil/blob/main/functions/public/Invoke-WPFUltimatePerformance.ps1).
- El Undo de WinUtil utiliza valores originales declarados en configuración, que pueden diferir de lo que tenía ese usuario. Fuente: [Invoke-WinUtilTweaks.ps1](https://github.com/ChrisTitusTech/winutil/blob/main/functions/private/Invoke-WinUtilTweaks.ps1).
- Optimizer agrupa Modo Juego, HAGS y opciones de pantalla completa; conviene separar controles. Sus ajustes `SystemResponsiveness=1` y `GPU Priority` no justifican una promesa de mejora: Microsoft indica que el primero se convierte en 20 y que el segundo no se utiliza. Fuentes: [OptimizeHelper.cs](https://github.com/hellzerg/optimizer/blob/master/Optimizer/OptimizeHelper.cs), [MMCSS de Microsoft](https://learn.microsoft.com/en-us/windows/win32/procthread/multimedia-class-scheduler-service).
- Las capturas Xbox y su grabación de fondo tienen alcance distinto; el bloque de Optimizer también desactiva captura manual. La opción debe explicar exactamente qué cambia. Fuentes: [DisableGameBar](https://github.com/hellzerg/optimizer/blob/master/Optimizer/OptimizeHelper.cs#L1047), [AllowGameDVR de Microsoft](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-applicationmanagement#allowgamedvr).

Fuentes de acciones adicionales: [StartupHelper.cs](https://github.com/hellzerg/optimizer/blob/master/Optimizer/StartupHelper.cs), [PingerHelper.cs](https://github.com/hellzerg/optimizer/blob/master/Optimizer/PingerHelper.cs), [Game Mode en WinUtil](https://winutil.christitus.com/code-reference/tweaks/customize-preferences/gamemode/), [limpieza temporal de WinUtil](https://winutil.christitus.com/code-reference/tweaks/essential-tweaks/deletetempfiles/).

Licencias declaradas: [WinUtil MIT](https://github.com/ChrisTitusTech/winutil/blob/main/LICENSE), [UniGetUI MIT](https://github.com/Devolutions/UniGetUI/blob/main/LICENSE), [Optimizer GPL-3.0](https://github.com/hellzerg/optimizer/blob/master/LICENSE). Estas referencias no implican importar o ejecutar su código.
