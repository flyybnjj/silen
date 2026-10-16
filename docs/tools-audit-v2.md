# Auditoría funcional de herramientas — 14 octubre 2026

Se revisaron todas las herramientas de la versión nativa WPF. Las reproducciones propias usan los DLL publicados en `artifacts/Silen-win-x64`, para no confundir fallos de esa versión con cambios concurrentes en el código. No se modificaron los módulos de aplicación, Core, Windows o Gaming ni Git. No se aplicaron ajustes, instalaron drivers o ejecutaron juegos.

## Hallazgos reproducidos

### P2 — El proveedor Microsoft de drivers del sistema se confunde con un equipo Surface

Ubicación: `src/Silen.Windows/WindowsActions.cs:29` y `src/Silen.App/MainViewModel.cs:392` (resolución `ManufacturerUrl` en el checkout durante la auditoría).

Al pulsar **Fabricante** en un dispositivo cuyo fabricante no está reconocido pero cuyo proveedor es Microsoft, la aplicación abre `https://support.microsoft.com/surface`. La identidad del proveedor de un driver de Windows no identifica un dispositivo Surface. En el inventario real del equipo **LENOVO 83GS**, 144 de 225 dispositivos se resolvieron a Surface, incluidos ACPI Fan, ACPI Lid y ACPI Power Button. El destino existe, pero no corresponde a esos dispositivos. La página está identificada como [soporte Surface de Microsoft](https://support.microsoft.com/en-us/surface/).

Dirección de corrección: separar soporte del equipo Microsoft/Surface de los drivers genéricos publicados por Microsoft. Los drivers inbox deben dirigir a soporte de Windows/Windows Update, o no ofrecer una fuente de fabricante si falta información; reservar Surface para una identidad de hardware comprobada. Añadir una regresión con un dispositivo ACPI de un equipo Lenovo y `Provider=Microsoft`.

### P2 — Dos instancias pueden perder registros del historial de guías

Ubicación: `src/Silen.Core/ReportStore.cs:10`, lectura y sustitución en `:86–88`.

El semáforo `actionGate` es de una instancia de `ReportStore`. Dos ventanas/procesos comparten `actions.json` pero pueden leer el mismo estado, añadir por separado y sustituirlo. La escritura atómica evita JSON parcial; no evita sobrescribir una entrada recién guardada por otro escritor. Reproducción con dos `ReportStore` independientes sobre el mismo directorio y 40 operaciones paralelas: **20 registros conservados, 20 perdidos**, sin excepción. La aplicación publicada admite múltiples instancias.

Dirección de corrección: coordinar lectura, modificación y sustitución mediante un bloqueo entre procesos para el directorio normalizado, o impedir una segunda instancia de la aplicación. La regresión debe usar escritores independientes; la prueba actual de concurrencia utiliza una única instancia y no cubre esta carrera.

### P3 — Cancelar una importación CSV activa espera a que termine el parser

Ubicación: `src/Silen.App/MainViewModel.cs:440`, `src/Silen.Gaming/PresentMonCsv.cs:48`, también el parse posterior a captura en `src/Silen.Gaming/GamingCapture.cs:97`.

El token se pasa a `Task.Run` y se comprueba después, pero el parser síncrono no recibe el token. Una vez iniciado el trabajo, cancelar evita publicar el resultado pero no detiene la lectura/cálculo. Reproducción con un CSV permitido de **62.050.039 bytes / 850.000 frames**: cancelación solicitada a los 58 ms; seguía ocupado a los 250 ms y terminó a los 2.465 ms. El cierre esperó correctamente esta operación. En una máquina más lenta, la espera puede ser mayor.

Dirección de corrección: aceptar un token en `PresentMonCsv.Parse` y comprobarlo periódicamente durante la lectura y antes de fases de cálculo; propagarlo desde importación y captura. Verificar cancelación después de que la lectura ya haya empezado.

## Cobertura y resultados

| Herramienta | Verificación | Resultado |
| --- | --- | --- |
| Analizar equipo | Artefacto completo previo `final-verification/status.json`; nueva cancelación real durante adquisición | Baseline: 10 muestras, 225 dispositivos, seis vistas. Cancelación: 581 ms, sin resultado publicado. |
| Buscar ofertas Windows Update | Consulta WUA real de solo lectura + cancelación real | 3 ofertas, consulta exitosa en 7.231 ms; cancelación en 262 ms. |
| Filtrar drivers | Modelo WPF real publicado e inventario del equipo | Vacío: 225; ` nVIDIA `: 4; Microsoft: 157; display: 2; texto inexistente: 0. Espacios y mayúsculas funcionan. |
| Todos los destinos de Windows | Ocho invocaciones de `WindowsActions.Open`, sin ajustar nada | Inicio, Almacenamiento, Energía, Windows Update, Drivers opcionales, Gráficos, Game Mode y Administrador de tareas aceptaron el lanzamiento sin error. Se observaron procesos SystemSettings y Taskmgr. |
| Fuentes de fabricantes | Reglas de alias/HTTPS + páginas oficiales | NVIDIA, AMD, Intel, Dell, Lenovo, ASUS, MSI y Microsoft accesibles mediante búsqueda web; HP HTTP 200 con redirección regional. Acer agotó una consulta HEAD de 15 s; esto es un límite de red de la revisión, no prueba de URL rota. Fallo de correspondencia Microsoft/Surface descrito arriba. |
| Fuente oficial PresentMon | URL fija y consulta de su página oficial | [Releases oficiales accesibles](https://github.com/GameTechDev/PresentMon/releases). |
| Gaming importar / comparar | Pruebas existentes sobre CSV, identidad de proceso/swap chain, percentiles, datos inválidos, columna y límites | Comprobaciones dirigidas correctas; cancelación de parser descrita arriba. |
| Gaming configurar / capturar / error / cancelar | Pruebas existentes con consola de prueba | PID y duración, extensión, falta de exe, archivos existentes, nombre con espacios, errores ETW, salida grande, resultados vacíos y limpieza por cancelación correctos. |
| Cerrar durante operación/captura | Pruebas de OperationLifetime y revisión previa del cierre real con limpieza lenta | El cierre espera la finalización; no se volvió a ejecutar la prueba previa de cierre de 20 s. |
| Historial / cargar / registros dañados | ReportTests + probe de escritores independientes | Carga, orden, preservación de registros dañados y plan energético null correctos; carrera entre instancias descrita arriba. |
| Exportar JSON / Markdown | Roundtrip y evidencia de Markdown de ReportTests; inspección del diálogo y escritura | JSON conserva lecturas/desconocidos; Markdown incluye medición/evidencia y escape de columnas. No se automatizaron los diálogos de archivo. |
| CLI / portable | Ejecución del exe publicado con argumento inválido e inspección de distribución | Salida 2; runtime .NET Core y WindowsDesktop 10.0.13 incluidos; README y licencias presentes. No se repitió el escaneo CLI completo, que guarda historial por diseño. |

Los siete URI de configuración coinciden con la [referencia actual de Microsoft](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings). Se comprobó aceptación de lanzamiento y presencia de los procesos; no se inspeccionó visualmente cada página ni se pulsaron ajustes. **Las herramientas de mejoras abren destinos oficiales para que el usuario decida qué cambiar. No ejecutan automáticamente una optimización.** El texto y el historial de la aplicación describen esa conducta correctamente.

Pruebas dirigidas, sin reconstruir ni ejecutar la suite completa: **58 Windows + 47 Gaming + 16 Core/Lifetime, todas correctas**. Se excluyeron la captura con deadline de 45 s, el caso de más de un millón de frames, y una prueba nueva de exportación de identidad OEM que estaba siendo incorporada por otro agente y falló antes de que existiera su implementación; esa prueba nueva no representa la versión publicada auditada.

## Evidencia y límites

- `artifacts/tools-audit-v2/Program.cs` y `ToolsProbe.csproj`: probe STA/Dispatcher contra ensamblados publicados, sin ventana propia.
- `artifacts/tools-audit-v2/probe-results.json`: inventario, filtros, destinos, concurrencia, importación/cancelación, escaneo/cancelación y WUA.
- `artifacts/tools-audit-v2/launch-results.json`: apertura real de los ocho destinos oficiales.
- `docs/code-review.md`: reproducción previa del cierre durante limpieza y fixes ya integrados.

La captura ETW de un juego real permanece sin verificar: no se proporcionó consola oficial instalada ni juego activo. La revisión utilizó únicamente el fixture de consola y no inició ETW real. Los tres hallazgos de este documento son reproducibles y tienen causa identificada; no se atribuyó el fallo generalizado a funciones que sí respondieron correctamente.

## Verificación de correcciones Core/Gaming en Release

Seguimiento contra los DLL actualizados de `src/Silen.Core/bin/Release/net10.0` y `src/Silen.Gaming/bin/Release/net10.0`, copiados por un proyecto independiente sin referencias de proyecto. No se repitieron WUA, los lanzamientos Windows o la suite completa.

- **Cancelación CSV corregida en Gaming:** el mismo CSV de 62.050.039 bytes / 850.000 frames interrumpió el parser **1,08 ms después de solicitar cancelación** (71,67 ms desde el inicio). El caso precancelado con ruta inexistente también terminó con `OperationCanceledException`, sin intentar abrir la ruta. El seguimiento de la integración del token en la interfaz corresponde al integrador.
- **La pérdida silenciosa entre escritores queda corregida:** el escenario original con dos `ReportStore` y 40 operaciones conserva **40/40** entradas. Dos procesos reales, con 100 entradas cada uno y sin un lector concurrente, conservan **200/200** identificadores distintos, sin errores.
- **La carrera adicional con lectores concurrentes también queda corregida:** inicialmente, dos procesos escritores y un tercer `ReportStore` que refrescaba `GetActionsAsync` provocaron `UnauthorizedAccessException` durante `File.Move(..., overwrite:true)` (`ReportStore.cs:143` en ese momento), conservando 114/200 entradas. Se corrigió haciendo que los lectores públicos compartan el mismo lease entre procesos, con lectura privada sin un segundo lease dentro de los escritores. El mismo probe, ejecutado una sola vez contra el Core Release recién compilado, conservó **200/200 registros y 200 identificadores distintos**, con **12 refrescos del lector, cero errores del lector y cero errores de los escritores**, en 4.325 ms. Ya no queda pendiente la carrera encontrada en este seguimiento.

Evidencia adicional: `artifacts/tools-audit-v2/updated-first-reader-failure.json`, `updated-no-reader-results.json`, `updated-cross-reader-fixed-results.json` y `updated/Program.cs`. El nuevo probe inicia dos procesos propios ocultos y utiliza únicamente directorios bajo `artifacts/tools-audit-v2`; no modifica el historial del usuario.

## Estado final de integración

Los tres hallazgos iniciales están corregidos. Los drivers genéricos cuyo proveedor es Microsoft ahora abren el Catálogo Microsoft Update; Surface requiere identidad de equipo Surface. Las pruebas de esa resolución y el resto de Windows pasan en la suite final. La cancelación del parser se propaga tanto desde importación WPF como desde captura. La publicación y sus verificaciones finales se registran en `docs/verification.md`.
