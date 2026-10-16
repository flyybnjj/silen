# Verificación final — 14 de octubre de 2026

## Código y distribución

Se ejecutó `scripts/build.ps1` sobre la fuente final con SDK .NET 10.0.402:

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| Core y helpers de App | 42 | 0 | 0 |
| Windows/OEM | 121 | 0 | 1 |
| Gaming | 51 | 0 | 0 |
| Total | 214 | 0 | 1 |

La omitida es una consulta de red Lenovo opt-in; se había ejecutado por separado con resultado correcto y también se ejercitó el servicio real durante el smoke final. Build Release: cero advertencias y errores. Publicación self-contained `win-x64` correcta.

La revisión independiente en [review-v2.md](review-v2.md) verificó las correcciones de sus tres hallazgos con probes offline contra fuente actual. No quedan hallazgos materiales pendientes en el alcance revisado. La auditoría de todas las herramientas y sus límites está en [tools-audit-v2.md](tools-audit-v2.md).

## Ejecutable publicado

Se lanzó `artifacts/Silen-win-x64/Silen.exe --smoke-test artifacts/v2-final-verification`, con historial de prueba aislado:

- Exit code 0 y `Success=true`.
- 26 comprobaciones deterministas de interacción correctas, incluida limpieza de PID históricos reutilizados.
- Análisis real: diez muestras, 225 dispositivos, Lenovo LOQ 15IAX9 / 83GS identificado como Laptop.
- Consulta real por modelo: 28 paquetes del catálogo oficial. `SerialLookupRequested=false`.
- Seis vistas a 1240×820 y 1000×680, además de estados vacío, análisis vivo, detalle OEM y formulario gaming.
- `AppliedSystemChanges=false`, `CapturedGaming=false`. Las fixtures de interacción no abren herramientas externas.
- JSON y Markdown contienen la identidad del modelo, sin propiedad de serie; la comprobación adicional no encontró nombre de usuario local ni series de fixture en ambas exportaciones.

Evidencia: [status.json](../artifacts/v2-final-verification/status.json), [interaction-checks.json](../artifacts/v2-final-verification/interaction-checks.json), [snapshot.json](../artifacts/v2-final-verification/snapshot.json), [report.md](../artifacts/v2-final-verification/report.md) y PNG en el mismo directorio. Los artefactos de ejecución quedan fuera de Git.

El ZIP contiene 439 entradas. El SHA-256 de su `Silen.dll` coincide con el ensamblado del ejecutable probado. ZIP final: 67.538.027 bytes; SHA-256 `FE0F0342A687F75667368B8AA49C2B60731526E462500C197A3C3CE2C9CF02EC`.

## Límites comprobables

La captura con PresentMon se valida con una consola de prueba; no se realizó una captura ETW de un juego real. La serie se prueba con fixtures, sin leer ni enviar una serie BIOS real. El catálogo específico integrado es Lenovo; otros OEM ofrecen su portal oficial cuando no hay una API verificada. Los paquetes de modelo no equivalen a ofertas de actualización confirmadas para cada componente instalado. La temperatura CPU no estuvo disponible con los proveedores actuales y se muestra como tal.
