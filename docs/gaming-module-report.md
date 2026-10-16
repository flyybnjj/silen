# Módulo Gaming: implementación y verificación

## Contratos y métricas

`PresentMonCsv.Parse(string path)` devuelve el `GamingSession` de Core. `new GamingCapture().CaptureAsync(executable, processId, durationSeconds, outputPath, cancellationToken)` ejecuta la captura opcional y devuelve el mismo contrato. El módulo no modifica Core ni depende de la interfaz.

Los tiempos se expresan en milisegundos. FPS medio = `1000 / media(frame times)`, sin promediar FPS instantáneos. P95 y P99 utilizan rango más próximo: ordenar ascendentemente y tomar el elemento en posición `ceil(percentil × n)`, con posiciones desde uno. El 1% low = `1000 / media de los ceil(n × 0,01) frames más lentos`, con al menos un frame. La lista de tiempos conserva el orden del CSV. Estas métricas describen tiempos de frames y no son latencia de entrada.

## Importación

Se reconocen `FrameTime` y `MsBetweenPresents` sin distinguir mayúsculas; la primera tiene prioridad si ambas existen. Los nombres proceden de la documentación oficial de [CSV de la aplicación de captura](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/README-CaptureApplication.md) y [CSV de consola](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/README-ConsoleApplication.md). Se admite UTF-8 con BOM, delimitador coma o punto y coma, campos entre comillas, comillas escapadas y punto o coma decimal, independientemente de la cultura del sistema. No se admiten separadores de miles ambiguos.

Se cuentan en `IgnoredRows` los registros malformados, con distinto número de campos, tiempos vacíos/no numéricos/no finitos/no positivos y PID inválido cuando existe esa columna. Las líneas vacías no representan frames. Un CSV vacío, sin columna reconocida, con columnas relevantes duplicadas o sin frames válidos produce un error explícito. Se rechazan archivos mayores de 64 MiB y más de 1.000.000 de frames válidos. La media incremental evita que la suma de valores finitos desborde.

Se rechazan procesos distintos, incluidos dos PID con el mismo nombre. Cuando existe `SwapChainAddress`, también se rechazan varias cadenas, para evitar sumar presentaciones de ventanas o superficies independientes como si fueran FPS de un juego. Se debe exportar/filtrar un único proceso y la cadena principal. `CapturedAt` corresponde a la fecha de modificación del CSV, no a una fecha inferida de sus muestras.

## Captura opcional

El contrato de argumentos se basa en la [consola oficial PresentMon 2.3.1](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/README-ConsoleApplication.md): `--process_id`, `--timed`, `--terminate_after_timed`, `--no_console_stats`, `--v1_metrics`, `--session_name` y `--output_file`. Se solicita el esquema histórico para obtener `MsBetweenPresents`. La consola se selecciona desde los [releases oficiales](https://github.com/GameTechDev/PresentMon/releases); no se descarga ni instala automáticamente. Se valida su existencia y extensión `.exe`, sin autenticar el origen del binario. La aplicación gráfica PresentMon y scripts de shell no son el contrato admitido.

Se validan PID positivo, duración entre 15 y 300 segundos, salida `.csv`, carpeta existente y ausencia de archivo previo. `UseShellExecute=false` y `ArgumentList` evitan interpretar rutas como comandos. Cada captura usa un nombre ETW aleatorio y un CSV temporal aleatorio en la carpeta de destino. El CSV validado se mueve al nombre elegido mediante una operación que rechaza sobrescrituras, incluso si otro programa creó ese destino durante la captura. Las capturas incompletas o inválidas no se publican como sesiones válidas.

Hay un plazo de duración + 30 segundos. Al cancelar o superar el plazo se termina únicamente el proceso de consola iniciado por esta llamada, sin terminar el PID del juego ni su árbol de procesos. El [código oficial del manejador de cierre](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/PresentMon/MainThread.cpp) advierte que un cierre forzado puede dejar la sesión ETW abierta. Por eso se ejecuta la limpieza oficial `--terminate_existing_session --session_name <nombre propio>`, también con argumentos separados y plazo limitado. No se detienen sesiones ajenas. Un fallo de esta limpieza se informa con el nombre de la sesión. Se intenta eliminar solamente el CSV temporal propio; si Windows mantiene ese archivo bloqueado puede quedar para revisión. La salida estándar y de error se drenan simultáneamente reteniendo como máximo 4096 caracteres por canal.

## Evidencia

Se observó la fase roja del parser (22 pruebas frente al contrato aún no implementado), de captura (13 pruebas) y de la regresión de limpieza ETW. La suite final contiene 49 pruebas: métricas calculadas por separado, límites, culturas, datos inválidos, procesos/cadenas mixtos, formato malformado, cotas de memoria, argumentos de proceso, cancelación, captura colgada y preservación del destino.

El fixture `presentmon-2.3.1-gold-excerpt.csv` contiene la cabecera y tres filas reales de un único proceso/cadena del [gold CSV oficial 2.3.1](https://github.com/GameTechDev/PresentMon/blob/v2.3.1/Tests/Gold/test_case_0_v1.csv); su licencia MIT está junto al fixture. El ejecutable `FakePresentMon` vive únicamente en los tests: verifica los argumentos recibidos por un proceso real y reproduce salida/error/bloqueo/limpieza. No se incorpora a la aplicación.

Comandos ejecutados con `C:\Users\avalo\.codex\runtimes\dotnet-10-sdk\dotnet.exe`:

- `test tests/Silen.Gaming.Tests/Silen.Gaming.Tests.csproj -c Release --no-restore`: **49 correctas, 0 fallidas, 0 omitidas**, 45 segundos; salida 0.
- `build src/Silen.Gaming/Silen.Gaming.csproj -c Release --no-restore -warnaserror`: **0 advertencias, 0 errores**; salida 0.
- `test Silen.sln -c Release`: Gaming 49/49 y Core 23/23; se observó una prueba Windows todavía en implementación: `SensorSafetyTests.Read_only_scan_disables_the_providers_that_use_a_kernel_sensor_driver`, con `NotImplementedException` en `HardwareSensors.CreateReadOnlyComputer`. Se comunicó al agente principal; la verificación final de toda la solución corresponde a la integración.

La compatibilidad comprobada es el parser de datos oficiales y el contrato CLI documentado de 2.3.1 mediante el colector de pruebas. No se ejecutó una captura ETW real con un binario oficial instalado ni se midieron FPS del equipo; esa verificación requiere seleccionar la consola y un juego activo. No se aplicaron optimizaciones, se instalaron drivers ni se modificaron ajustes del sistema durante estas pruebas.
