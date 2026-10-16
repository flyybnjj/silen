# Revisión independiente de herramientas incluidas y WPF

Revisión del 14 de octubre de 2026 contra el código final de la especificación `docs/superpowers/specs/2026-10-14-self-contained-tools.md`. Alcance: herramientas nativas y recuperación, limpieza seleccionada, distribución de PresentMon, cancelación/cierre y movimiento WPF. No quedan defectos concretos abiertos en este alcance tras comprobar las correcciones.

## Defectos reproducidos y resueltos

| Caso | Evidencia anterior | Corrección y resultado final |
| --- | --- | --- |
| Pérdida de foco de teclado durante una presión | Después de Space y `LostKeyboardFocus`, el control conservaba escala `0.975`. | `Motion` atiende también la pérdida de foco; la misma prueba termina en escala `1`. Las selecciones interrumpidas terminan en opacidad `1` o `0` según el estado final. |
| Cancelación después de una aplicación parcial | El proveedor conservaba estado mixto y un respaldo, pero la interfaz mostraba el estado previo y cero cambios. | `ToolsViewModel.ApplyAsync` vuelve a leer en `finally`, sin el token cancelado y dentro de la operación compartida. La misma prueba muestra estado mixto y un cambio recuperable. |
| Cancelación al renovar la vista previa de temporales | El proveedor invalidaba los identificadores previos, mientras la interfaz conservaba una selección con el botón de limpieza habilitado. | Al iniciar una nueva vista previa se descartan filas y selección. La misma prueba termina con cero filas, `HasPreview=false` y limpieza deshabilitada, además de una indicación para reintentar. |

Las reproducciones usan proyectos propios que enlazan los archivos de producción sin modificarlos. Las correcciones las hicieron los dueños del código; el revisor volvió a ejecutar las mismas secuencias para confirmar el resultado.

## Comprobación independiente

El probe nativo terminó con **23 comprobaciones correctas**, salida 0. Usa un proveedor falso de registro/energía y archivos de fixture en un directorio propio:

- Conserva y restaura exactamente originales MultiString, Binary, None, QWord y DWord, incluso al recrear el servicio.
- Una cancelación después del primer valor de Capturas Xbox conserva un respaldo durable y permite recuperar la ausencia original de ambos valores.
- El plan de energía anterior eliminado produce rechazo de deshacer y conserva el plan actual.
- Modo Juego consulta, aplica y restaura sus dos valores, incluyendo un original mixto.
- La vista previa solo ofrece archivos antiguos y no elimina; la limpieza toca el archivo seleccionado y contabiliza sus bytes.
- Archivos ocupados, identificadores ajenos, vínculos duros y selecciones de una vista previa antigua se omiten sin afectar archivos externos al alcance.

Evidencia: `artifacts/selfcontained-review/native-probe/bin/Release/net10.0-windows/native-results.json`.

Los probes WPF ocultos terminaron con salida 0 y compilación sin errores ni advertencias. Evidencia:

- `artifacts/selfcontained-review/motion-probe/bin/Release/net10.0-windows/focus-loss-result.json`: escala tras pérdida de foco `1`, selección rápida `1`, deselección rápida `0`.
- `artifacts/selfcontained-review/tools-probe/bin/Release/net10.0-windows/partial-cancel-result.json`: proveedor e interfaz muestran estado mixto y un cambio.
- `artifacts/selfcontained-review/tools-probe/bin/Release/net10.0-windows/preview-cancel-result.json`: ninguna fila/selección accionable después de cancelar.

## Inspección del código final

El journal usa exclusión entre instancias, respaldo escrito y sincronizado antes de la mutación, reemplazo atómico, validación de destinos permitidos y rechazo de destinos duplicados. Deshacer comprueba los valores presentes antes de restaurarlos y no reemplaza un valor externo distinto. Los registros pendientes o parciales siguen disponibles para recuperación.

La limpieza revalida edad, tamaño, creación y escritura usando un handle del archivo; omite reparse points y archivos con más de un vínculo. Comprueba el destino resuelto y sus ancestros, y usa el mismo handle para marcar la eliminación. La enumeración y selección tienen límites; cancelar durante la limpieza devuelve los conteos de las eliminaciones realizadas.

PresentMon se copia a salida/publicación con su procedencia y licencia. El resolver usa el directorio de la aplicación y comprueba el SHA256 fijado antes de la captura. El asset revisado coincide con `B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF`. El build rechaza ausencia o alteración y no descarga herramientas. La interfaz final no solicita un ejecutable externo.

La inspección inicial y el análisis no aplican ajustes. Las acciones de Mejoras/Gaming comparten bloqueo, cancelación y espera de cierre; los resultados y cambios parciales se vuelven a leer antes de liberar esa operación. Las filas de inicio y su copia describen explícitamente presencia/remoción en HKCU Run, sin atribuir un estado global de inicio a Windows.

El tema final incluye el selector de plantilla necesario para presentar el nombre del plan/proceso. La navegación usa el estado real de selección; las transiciones retiran relojes reemplazados y respetan la preferencia de animaciones de Windows/alto contraste. Los renders esperan 240 ms antes de capturar. La comprobación de movimiento reducido fue de código; no se cambió la preferencia del equipo.

## Límites de esta revisión

No se aplicaron cambios reales de registro, energía ni animaciones, ni se limpió la carpeta Temp del usuario ni se inició ETW. Las eliminaciones y el vínculo duro se limitaron a fixtures propios. No se repitió la suite completa ni una tanda visual independiente; esos resultados y el smoke del paquete publicado pertenecen a la verificación final coordinada. La captura de un juego real permanece fuera de estas pruebas.

## Revisión dirigida de elevación para ETW

Después de que la verificación coordinada encontró un rechazo de permisos al iniciar ETW, se revisó el botón opcional para abrir una nueva ventana de Silen como administrador. No hay hallazgos concretos abiertos en esta incorporación.

`Elevation.CreateStartInfo` selecciona únicamente `AppContext.BaseDirectory/Silen.exe`, con el mismo directorio de trabajo, `UseShellExecute=true`, `Verb=runas` y ningún argumento. No busca ejecutables en PATH, no eleva PresentMon directamente y no reutiliza argumentos de smoke. La ruta ausente muestra una indicación de recuperar el paquete completo.

El botón solo aparece después de un error de permisos de captura y cuando el token del proceso no está elevado. La inicialización y el rechazo de ETW no solicitan UAC automáticamente. El comando exige un clic explícito y comparte `RunBusyAsync`/`OperationLifetime`: mientras la solicitud está pendiente se bloquean nuevos análisis/herramientas, y cerrar espera a que termine. La ventana original y su sesión se conservan. El código 1223 de Windows informa que la solicitud se canceló y permite reintentar desde el botón.

Un probe WPF que enlaza las fuentes finales terminó con **11 comprobaciones correctas**, salida 0 y compilación sin advertencias/errores. Verificó la ruta/verb/argumentos sin llamar `Process.Start`, inicialización sin elevación, error de permisos sin lanzamiento automático, rechazo 1223 y reintento, conservación de la sesión original, exclusión de token ya elevado/errores de CSV, y bloqueo/espera de cierre durante un lanzamiento simulado. El apphost utilizado para comprobar la ruta fue un archivo inerte en el directorio propio del probe y nunca se ejecutó.

Evidencia: `artifacts/selfcontained-review/elevation-probe/bin/Release/net10.0-windows/elevation-results.json`, con `UacOpened=false` y `EtwStarted=false`. La revisión dirigida no abrió UAC ni inició una traza ETW real.
