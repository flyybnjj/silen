# Revisión independiente de interacción y OEM — v2

Revisión limitada al cambio de interacción/OEM del 14 de octubre de 2026, con la especificación `docs/superpowers/specs/2026-10-14-interaction-oem.md`, el diff `.superpowers/review-v2.diff` y los archivos actuales del checkout como fuente autoritativa. No se editaron código, índice ni ramas. Las reproducciones usan fuentes enlazadas en proyectos temporales y HTTP de fixture; no se consultó la red, ejecutaron herramientas Windows, instaladores o capturas ETW, ni se repitió la suite completa.

## Hallazgos reproducidos

### P2 — Identidades nulas del historial interrumpían la búsqueda OEM

Ubicaciones originales: `src/Silen.Windows/OemDriverService.cs:36`, `:38`, `:119` y `:121`; entrada aceptada en `src/Silen.Core/ReportStore.cs:65–73`.

Un snapshot JSON con `Machine.Model = null`, `Machine.Manufacturer = null` o `Machine.SystemSku = null` supera la validación estructural de `ReportStore.LoadAsync`. La nueva búsqueda invocaba `.Trim()` en esos valores. Reproducción offline con guardado/carga reales: los tres informes se cargan y la consulta lanza `NullReferenceException`, antes de construir un fallback o aprovechar un SKU Lenovo válido. La app captura el error, pero no obtiene el catálogo o portal para el informe aceptado.

Estado: **corregido y verificado independientemente**. El servicio normaliza fabricante/modelo/SKU con acceso null-safe (`OemDriverService.cs:36–39`, `:122–124`). El mismo probe, con HTTP de fixture, confirma: modelo null + SKU válido obtiene el catálogo 83GS y dos paquetes; SKU null con modelo genérico devuelve portal sin coincidencia; fabricante null devuelve soporte desconocido sin excepción. Las regresiones nuevas incluyen identidad totalmente null y Microsoft sin modelo.

### P2 — Una respuesta de serie contradictoria se aceptaba como modelo identificado

Ubicaciones originales: `src/Silen.Windows/OemDriverService.cs:69–76` y `:167–174`.

Para una solicitud por serie `PFTEST1234`, el fixture oficial de producto se modifica únicamente para devolver `Serial = PFWRONG5678`. La búsqueda informa `Success = true`, `ModelMatched = true` y dos paquetes. El parser descarta `Serial`, y la comprobación de identidad solo compara el tipo de máquina en búsquedas sin serie. Una respuesta que contradice explícitamente la serie autorizada debe rechazarse antes de descargar el catálogo.

Estado: **corregido y verificado independientemente**. El parser conserva la serie solo dentro de la tupla local y la comparación en `OemDriverService.cs:72–74` rechaza la contradicción. Repetir el mismo fixture produce `Success = false`, `ModelMatched = false`, cero paquetes y una única solicitud HTTP, antes del catálogo. El resultado solo contiene la máscara de la serie solicitada. Se revisaron además las regresiones de serie coincidente, vacía o ausente.

### P2 — Cargar un informe antiguo ofrecía sus PID archivados para una captura actual

Ubicaciones originales: `src/Silen.App/MainViewModel.cs:303`, `:456–461`, `:496–497`.

`ApplySnapshot(result, false)` vuelve a poblar el selector Gaming con los procesos archivados. La comprobación de captura solo confirma que el PID siga existiendo. Si Windows reutilizó el número, la elección conserva el nombre del juego antiguo pero apunta a otro proceso. Reproducción sin captura: un informe del 1 de enero con nombre `ArchivedGame` y PID del proceso de prueba se ofrece como selección; los inputs se aceptan y el PID corresponde al proceso actual `UIReview`. No se invocó `CaptureCommand` ni se ejecutó PresentMon.

Estado: **corregido y verificado independientemente**. `MainViewModel.cs:303–308` conserva procesos como candidatos solo para un análisis actual y limpia opciones/PID al cargar historial. El probe STA enlazado a la fuente confirma: observación actual tiene una opción y PID seleccionado; después de cargar el mismo snapshot como historial hay cero opciones, selección null y PID vacío. No se invocó una acción nativa. Las regresiones UI conservan el inventario histórico para inspección y mantienen disponibles PID manual y refresco de procesos actuales.

## Áreas revisadas sin otro hallazgo material

- La adquisición consulta modelo/nombre/SKU y chassis/PCSystemType sin leer una serie; UPS/batería sola no demuestra portátil.
- El progreso lleva muestras reales y sus callbacks verifican generación y cancelación. Al completar/fallar/cancelar se invalida la generación.
- La búsqueda por modelo no llama al lector BIOS de serie; la búsqueda explícita conserva únicamente la máscara en el resultado. Los fallos HTTP agrupados no exponen la URL de solicitud.
- El cliente de producción desactiva redirección automática, y cada salto exige HTTPS, host Lenovo exacto, puerto por defecto y ausencia de userinfo. El referrer es la raíz del portal; no incluye la serie.
- Los enlaces de paquetes pasan por hosts oficiales exactos y solo se abren en el navegador. El catálogo permanece separado de ofertas Windows Update y no afirma demostrar una actualización instalada.
- Una nueva observación reinicia ofertas, catálogo, portal, serie y selección OEM; las operaciones del usuario se serializan con `IsBusy`.
- El PID manual limpia una selección anterior; la validación de duración y ejecutable da errores locales al formulario.
- Los lectores y escritores públicos de acciones usan el mismo lease de archivo entre procesos; los escritores leen mediante el helper sin adquirir un segundo lease. La evidencia independiente previa de 200 escritores con lector está documentada en `docs/tools-audit-v2.md` y no se repitió.
- JSON/Markdown incluyen los nuevos campos de identidad; no existe campo de serie en `MachineInfo` ni serialización de `OemSupportResult` dentro del snapshot/exportación.

## Evidencia y límites

Proyectos temporales: `C:\Users\avalo\AppData\Local\Temp\Silen-review-v2-5e2f44aadacd433c8f605033b6bc0305\Review.csproj` y subdirectorio `ui\UIReview.csproj`. Ambos enlazan archivos fuente actuales; el probe OEM utiliza `System.Management.dll` únicamente como referencia y reemplaza HTTP y lector de serie por fixtures. El probe UI no abre una ventana o herramienta.

No se verificó ETW de un juego real, un lookup de serie real ni un OEM adicional mediante API. La consulta real modelo-only de Lenovo 83GS, pruebas y renders completos pertenecen a la evidencia del integrador. Esta revisión no vuelve a juzgar defectos baseline ya corregidos ni estética de interfaz.

## Veredicto

**Apto para integrar: sí, en el alcance revisado.** Los tres hallazgos reproducidos fueron corregidos y se verificaron con reproducciones independientes contra la fuente actual. No queda ningún defecto material identificado. Las pruebas finales de publicación y la captura ETW real mantienen los límites descritos arriba.
