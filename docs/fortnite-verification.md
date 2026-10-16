# Verificación del perfil Fortnite — 14 de octubre de 2026

Se corrigió la navegación de Inicio/Energía/Temporales/Modo Juego para usar controles internos y se incorporó Optimizar PC para Fortnite con detección, selección, progreso, resultados y restauración. Modo Juego utiliza el fragmento oficial WinUtil fijado y embebido; los demás pasos son implementación de Silen. No se ejecuta otra aplicación de optimización ni se descarga un script remoto.

## Pruebas

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| Core y helpers de App | 42 | 0 | 0 |
| Windows/OEM y perfiles | 199 | 0 | 1 |
| Gaming | 57 | 0 | 0 |
| Total | 298 | 0 | 1 |

La suite completa pasó; tras corregir dos defectos observados en la revisión se volvió a ejecutar el proyecto Windows afectado. La omitida es la consulta Lenovo de red opt-in; el smoke publicado consultó realmente el catálogo. Build/publicación finales: cero errores y cero advertencias.

Las pruebas nuevas incluyen 18 de perfil/WinUtil/GPU/AC y 29 de archivos Fortnite. Se observaron antes de corregirse la selección de solo lectura, el reemplazo con error 32, la alimentación que cambia mientras se espera un lease y la redirección de AppData de Windows. Se usan proveedores falsos para ajustes de Windows y archivos propios para mutaciones. El probe de revisión conserva un archivo reemplazado con otra identidad y bloquea una restauración parcial ajena.

Las **82 comprobaciones WPF** del paquete final pasan con fixtures. Incluyen el botón de recomendación de Inicio, desplazamiento real a su sección, detección automática/manual, selección de pasos, opciones no disponibles, resultados mixtos, progreso, cancelación parcial, respaldos visibles y espera de cierre. No se abrieron programas externos reales, UAC ni una captura de juego.

## Paquete publicado

Se ejecutó `Silen.exe --smoke-test` con PATH limitado a System32, sin las variables de SDK/DOTNET_ROOT y con búsqueda multilevel de .NET deshabilitada. `artifacts/verify-published-fortnite-final.ps1` reproduce el lanzamiento.

- Salida 0 y Success=true; CapturedAt `2026-10-14T14:28:29-03:00`.
- Perfil Fortnite cargado; instalación real detectada en `C:\Program Files\Epic Games\Fortnite`, cinco opciones disponibles según el estado actual.
- El smoke ahora espera la detección y falla si el perfil no carga; no depende solo del resultado de la pantalla de análisis.
- Diez muestras, 232 dispositivos con drivers y 28 paquetes Lenovo por modelo. Sin consulta por serie BIOS.
- Seis vistas a 1240×820 y 1000×680, con formularios y secciones inferiores. La captura de Gaming se desplaza a su ancla después de añadir el perfil.
- AppliedSystemChanges=false y CapturedGaming=false: no se optimizó realmente este PC ni se editó su configuración real del juego.

Evidencia: [status.json](../artifacts/fortnite-final/status.json), [interaction-checks.json](../artifacts/fortnite-final/interaction-checks.json), [gaming.png](../artifacts/fortnite-final/gaming.png) y restantes PNG en ese directorio. Los artefactos de ejecución están excluidos de Git.

La primera tanda visual del paquete detectó un error: Windows redirigía AppData al cache MSIX del proceso y la comparación literal de rutas rechazaba su propio lock. Se reprodujo con un archivo propio en AppData. El archivo se valida ahora contra el directorio físico ya abierto/verificado, manteniendo sus handles y el rechazo de enlaces. La segunda tanda confirma que el perfil carga y el botón está disponible.

El ZIP final tiene 446 entradas, **68.114.666 bytes** y SHA256 `0F77BB1EF102712BF2DCCB1F12C372034FD6517254419F557BADE7735DDB376B`. Se verificaron sus apphost, ensamblados App/Windows, PresentMon, fragmento WinUtil y licencia contra la carpeta publicada probada. App DLL: `3A9F0148561B186377CD149735FBB700FDCDF74305A8AB86311565F4F3D71E5A`; Windows DLL: `1DBBCCE96CBB7AF027E36A42C8F23A9CA6CE82C302372161F89943810131CE6A`.

## Límites

El perfil aplica preferencias y valores existentes; no demuestra que se haya alcanzado el máximo de FPS del hardware. No se capturó una partida real comparable ni se validó una ganancia de FPS. Renderizador, resolución y límite FPS se conservan; Modo Performance se explica como paso del juego. Solo se usa un plan Alto rendimiento existente con AC, sin crear Ultimate Performance. La preferencia GPU no garantiza la GPU efectiva.

La configuración tiene respaldo durable y escritura sobre un handle exclusivo; no es atómica ante apagones. La recuperación parcial exige contenido reconocido, respaldos íntegros e identidad coincidente; otros cambios se conservan. Ver [fortnite-game.md](fortnite-game.md) y [la revisión con su alcance real](fortnite-review.md).
