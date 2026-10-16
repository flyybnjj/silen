# Verificación de Silen autocontenido — 14 de octubre de 2026

> Registro histórico de una compilación anterior. PresentMon y la captura se retiraron después. Para 1.1.0 consulta [actualizaciones integradas](github-updates.md) y el README actual.

Esta versión incorpora herramientas nativas en Mejoras/Gaming, el colector oficial de captura incluido y el tema claro con tipografía y movimiento nativos de WPF. El paquete portable incluye el runtime .NET y PresentMon; no necesita instalar WinUtil, Optimizer, SDK ni una aplicación de captura aparte.

## Pruebas y revisión

Se ejecutó la suite completa Release contra los nuevos servicios y el colector incluido:

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| Core y helpers de App | 42 | 0 | 0 |
| Windows/OEM y herramientas nativas | 152 | 0 | 1 |
| Gaming y colector incluido | 57 | 0 | 0 |
| Total | 251 | 0 | 1 |

La omitida es la consulta Lenovo opt-in de red; el smoke publicado ejercita el catálogo real por modelo. Las 31 pruebas nuevas del servicio nativo verifican respaldo, recuperación parcial, tipos originales, conflictos, concurrencia y limpieza seleccionada. Los ajustes usan un proveedor de fixture y los archivos de limpieza se limitan a directorios propios; no se modifican preferencias reales del equipo.

La interfaz terminó con **63 comprobaciones deterministas correctas**, incluidas cancelación parcial, invalidación de una vista previa anterior, selección visible del plan y el nuevo flujo de permisos ETW. La elevación se sustituye por un delegado de fixture: se comprueban el clic explícito, el ejecutable propio, la cancelación de UAC, la conservación de la sesión original y el bloqueo durante otras operaciones/cierre. No se abrió un diálogo UAC real.

El probe de movimiento verificó 16 contratos WPF. La [revisión independiente](selfcontained-review.md) verificó otras 23 comprobaciones nativas, reproducciones de pérdida de foco y cancelación de interfaz, y 11 comprobaciones dirigidas al flujo de elevación. No quedan defectos concretos abiertos en ese alcance. La revisión visual conjunta usó dos lotes de las seis vistas a 1240×820 y 1000×680, con secciones inferiores y formularios. El render publicado final confirma la distribución; no se inició otra ronda de pulido.

La última compilación y publicación mediante `scripts/build.ps1 -SkipTests` terminó con **cero advertencias y cero errores**. Tras añadir el botón de permisos se ejecutaron las nuevas comprobaciones de App y el smoke del paquete, sin repetir la suite de servicios que no habían cambiado.

## Ejecutable publicado sin SDK

Se lanzó el apphost publicado con `--smoke-test`, `PATH` limitado a `Windows/System32`, sin `DOTNET_ROOT`, `DOTNET_ROOT_X64`, `DOTNET_ROOT(x86)` ni `MSBuildSDKsPath`, y `DOTNET_MULTILEVEL_LOOKUP=0`. Resultado:

- Salida 0 y `Success=true`, a las 13:36 del 14 de octubre de 2026.
- Diez muestras reales, 232 dispositivos con drivers y dos hallazgos.
- Portátil Lenovo LOQ 15IAX9 / 83GS identificado; 28 paquetes del catálogo oficial por modelo.
- `SerialLookupRequested=false`: no se hizo una consulta de serie BIOS.
- Seis vistas renderizadas a tamaño inicial y mínimo, además de estados y secciones específicas.
- 63 comprobaciones de interacción correctas; `AppliedSystemChanges=false` y `CapturedGaming=false`.
- Las exportaciones JSON y Markdown no contienen el nombre de usuario local ni la serie de fixture; el JSON no incluye una propiedad de serie.

Evidencia: [status.json](../artifacts/self-contained-release/status.json), [interaction-checks.json](../artifacts/self-contained-release/interaction-checks.json), [snapshot.json](../artifacts/self-contained-release/snapshot.json) y PNG del mismo directorio. El script reproducible de lanzamiento está en `artifacts/verify-published-self-contained.ps1`; los artefactos de ejecución no se guardan en Git.

El ZIP final tiene **443 entradas**, **68.057.823 bytes** y SHA256 `ED8E1472F0BBD8FB941435866E01D84EF1A3A7537CDCB19C6E41A52834BCE6F6`. Su apphost, ensamblado App y colector coinciden por SHA256 con la carpeta publicada probada:

| Archivo | SHA256 |
| --- | --- |
| Silen.exe | `5D88BF9F6CE72362101CC96E8C7FC0768E5613FCDCC70ABD2C1434155C27FADD` |
| Silen.dll | `767187294BA813F2B770F3C8CA912CF8CE539FB99F5DA88BD6BD6E3A08767D64` |
| tools/PresentMon/PresentMon.exe | `B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF` |

## Permisos de captura y límites comprobados

La consola incluida ejecutó su ayuda nativa sin SDK ni runtimes C++ adicionales. Un intento real de captura sobre nuestro propio proceso WPF terminó con código 6 y `failed to start trace session: access denied`: Windows requiere administrador o pertenencia al grupo Performance Log Users en este equipo. No se creó una sesión ETW ni se capturó un juego real. Evidencia: `artifacts/self-contained-probe/published-check/capture.json`.

Gaming reconoce ese error y ofrece **Abrir Silen como administrador** si el proceso actual no está elevado. El clic abre el apphost de Silen de su propia carpeta con `runas`; conserva la ventana actual y no inicia permisos automáticamente ni instala otro programa. La captura exitosa de un juego con elevación sigue pendiente de validación real.

Las temperaturas dependen de los sensores disponibles; en este equipo no se obtuvo temperatura CPU. Inicio gestiona únicamente las entradas HKCU Run del usuario; los paquetes OEM integrados específicos corresponden a Lenovo y no equivalen a actualizaciones confirmadas para cada componente. La limpieza seleccionada elimina archivos y no tiene deshacer. Los ajustes guardan originales y permiten restauración cuando no hay cambios externos incompatibles.
