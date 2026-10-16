# Colector gaming incluido

Silen distribuye la consola standalone oficial x64 de PresentMon 2.6.0. No usa el servicio, la aplicación gráfica, un instalador MSI ni `presentmon-cli`. No descarga componentes al entrar a Gaming o al iniciar una captura. Los binarios oficiales se mantienen en el repositorio para que la publicación sea reproducible sin consultar GitHub.

## Integración

`Silen.Gaming.BundledPresentMon.Resolve(string? baseDirectory = null)` devuelve la ruta absoluta a `tools/PresentMon/PresentMon.exe`. El valor predeterminado usa `AppContext.BaseDirectory`, sin depender del directorio actual. Antes de devolverla lee el archivo y compara su SHA256 fijado. Ausencia, manipulación y errores de acceso producen `InvalidOperationException` con una acción concreta para el usuario; un error de lectura conserva su excepción interna. No hay fallback a un ejecutable del PATH o de otra instalación.

La aplicación WPF copia ejecutable, procedencia y licencias a salida y publicación mediante `CopyToOutputDirectory` y `CopyToPublishDirectory`. `scripts/build.ps1` rechaza un asset ausente o cuyo SHA256 no coincide; no incorpora descargas. La API de captura conserva su parámetro de ejecutable interno para probar de forma aislada el ciclo de vida del proceso. La interfaz resuelve automáticamente el colector incluido.

## Procedencia

- Release [v2.6.0 oficial](https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0), publicada el 21 de septiembre de 2026.
- Archivo original [PresentMon-2.6.0-x64.exe](https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe), renombrado a `PresentMon.exe` sin modificar bytes.
- Tamaño: **980.320 bytes**.
- SHA256: `B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF`; coincide con el digest publicado por la API de GitHub.
- Firma Authenticode comprobada: **Valid**, Intel Corporation.
- [Licencia MIT original](https://raw.githubusercontent.com/GameTechDev/PresentMon/v2.6.0/LICENSE.txt) conservada completa en `licenses/PresentMon-2.6.0-MIT.txt`.
- Metadatos en `assets/PresentMon/provenance.json`.

## Dependencias y compatibilidad

La inspección PE confirma AMD64 (`0x8664`) y ausencia de cabecera CLR. Importa `KERNEL32.dll`; carga diferida de `ADVAPI32.dll`, `SHELL32.dll`, `USER32.dll` y `tdh.dll`. Son componentes de Windows. No requiere archivos nativos adicionales, SDK .NET ni VC++ Redistributable para esta consola; el [proyecto oficial](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/PresentMon/PresentMon.vcxproj) también configura el runtime C++ estático en Release.

La [documentación de consola fijada](https://github.com/GameTechDev/PresentMon/blob/v2.6.0/README-ConsoleApplication.md) y la ayuda del binario incluyen todas las opciones usadas por Silen: `--process_id`, `--timed`, `--terminate_after_timed`, `--no_console_stats`, `--v1_metrics`, `--session_name`, `--output_file` y `--terminate_existing_session`. Se conserva el formato CSV v1 compatible con el parser existente. La limpieza de cancelación usa solo la sesión ETW propia, con nombre aleatorio.

La captura ETW continúa sujeta a permisos de Windows. No se instala un servicio ni se cambia la pertenencia a grupos automáticamente. La disponibilidad del colector incluido y la medición efectiva de un juego son verificaciones distintas.

## Verificación del 14 de octubre de 2026

Se escribieron primero seis pruebas para ruta con espacios, resolución predeterminada, archivo ausente, archivo modificado, archivo bloqueado y ejecución de ayuda sin PATH de SDK/herramientas. Las seis fallaron con la API sin implementar y pasan con el resolver real.

La suite del proyecto Gaming terminó con **57 pruebas correctas, 0 errores y 0 omitidas** en 46 segundos, incluyendo el ciclo de vida de captura con el proceso de fixture y su timeout. No se ejecutó una captura de juego real.

La prueba nativa ejecuta únicamente `--help`, sin captura ni ETW. Limita PATH a System32 y elimina `DOTNET_ROOT`; muestra `PresentMon 2.6.0` y todas las opciones necesarias. La ayuda retorna **1** por diseño upstream: `ParseCommandLine` imprime ayuda y retorna falso; `wmain` retorna 1 antes de iniciar la traza. No se interpreta ese código como fallo de dependencia.

Evidencia local: `artifacts/bundled-gaming/pe-imports.json`, `help.stdout.txt` y `help.stderr.txt`. La suite completa y el smoke del paquete publicado corresponden a la verificación final coordinada del producto.
