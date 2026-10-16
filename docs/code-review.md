# Revisión independiente de código

Revisión del primer Silen nativo para Windows 11, contra el diseño y el plan del 13 de octubre de 2026. Base: `8d4f59c`; fuente final del checkout. No se editaron código, índice ni ramas durante la revisión.

## Fortalezas

- Separación clara entre contratos/reglas, adquisición Windows, captura Gaming e interfaz WPF. Los datos ausentes siguen siendo desconocidos.
- Acciones oficiales enumeradas, fabricantes HTTPS permitidos y ejecución de PresentMon sin shell. Los informes usan escritura atómica y las capturas evitan sobrescribir archivos existentes.
- Métricas Gaming coherentes con las definiciones aprobadas: media de frame times, rango próximo y media del 1% más lento. El parser rechaza mezclas identificables de procesos y swap chains.
- La consulta WUA conserva callback y cancelación hasta su limpieza; el historial conserva registros dañados y los resultados válidos.

## Hallazgos originales y resultado

### P2 — Un plan energético nulo impedía leer todo el historial

Ubicación: `src/Silen.Core/RecommendationEngine.cs:57`, invocada desde `ReportStore.cs:43`.

Un JSON con `Id` y `CapturedAt` válidos y `Machine.PowerPlanGuid = null` superaba la validación estructural. El uso de `.Equals` sobre ese valor lanzaba `NullReferenceException`, abortando la lista completa de informes. Corregido mediante `string.Equals` con manejo de null. El integrador reprodujo el fallo y verificó la regresión `UnknownPowerPlanInOneReportDoesNotBreakEntireHistory`; la suite Core/Lifetime final observada por el integrador fue 30/30.

### P2 — Cerrar la ventana podía interrumpir la limpieza de captura

Ubicación original: `src/Silen.App/MainWindow.xaml.cs:11` y `App.xaml.cs:35`; limpieza asíncrona en `GamingCapture.cs:78` y `:100`.

El cierre solo solicitaba cancelación y ejecutaba inmediatamente `Shutdown`. Un colector de prueba con limpieza lenta dejó vivo su proceso de limpieza y conservó el CSV temporal al terminar WPF. Corregido: `MainWindow.xaml.cs:19` aplaza el cierre; `MainViewModel.cs:343` espera `OperationLifetime.StopAsync`; `OperationLifetime.cs:49` espera la finalización real de la operación.

Regresión independiente contra el App Release final, usando la ventana y el modelo reales en un proceso oculto y un colector de prueba cuya limpieza se bloquea 20 segundos: cierre aplazado 5,038 segundos; `Busy=False` al cerrar; colector y proceso de limpieza terminados; cero CSV temporales; salida 0. Se conservó el error explícito de limpieza con el nombre de la sesión propia. El marcador de sesión del fixture permaneció deliberadamente por el fallo simulado; no se inició ETW real ni se modificaron ajustes Windows.

Evidencia temporal: `C:\Users\avalo\AppData\Local\Temp\Silen-ReviewClose-f1dfb3dec361430fb624c364b49518e8\FixedCleanupRun`. El escenario anterior está en `SlowCleanupRun`.

## Pendientes de revisión

Ningún hallazgo material pendiente. No se volvió a ejecutar la suite extensa durante esta revisión; la verificación completa y la publicación corresponden al integrador.

## Conductas no juzgadas

- Captura ETW real de un juego con consola oficial: no se proporcionó un binario instalado ni un juego activo; la limitación está documentada.
- Autenticar por firma el ejecutable de consola seleccionado: el alcance admite selección explícita por el usuario y documenta que no autentica el binario.
- Sensores CPU que requieren un controlador de kernel: deshabilitados deliberadamente según el alcance aprobado.
- Aplicar automáticamente ajustes o instalar drivers: fuera del alcance; las guías abren las herramientas oficiales.

## Veredicto

**Apto para integrar: sí.** Los dos fallos encontrados se corrigieron y tienen evidencia de regresión. No quedan defectos materiales identificados en las áreas revisadas; se mantiene la limitación documentada de captura ETW real pendiente.

Los nombres y semántica CSV se contrastaron con la [documentación oficial de PresentMon 2.3.1](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/README-ConsoleApplication.md) y su [aplicación de captura](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/README-CaptureApplication.md). El callback DISPID 0 y la limpieza WUA se contrastaron con [BeginSearch](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-beginsearch) y [CleanUp](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-isearchjob-cleanup). Los destinos de ajustes coinciden con la [lista de URI de Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).
