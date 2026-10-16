# Silen: investigación de referencias

Fecha de consulta: 13 de octubre de 2026.

Objetivo indicado por el usuario: una app que analice el PC y encuentre mejoras de rendimiento, incluidos drivers. Público confirmado: uso general y gaming, con diagnóstico completo y mejoras guiadas. Windows es una propuesta de plataforma inicial, pendiente de validar junto con el diseño.

Este documento registra fuentes y hallazgos. La arquitectura y el alcance de implementación todavía requieren revisión.

## Proyectos de GitHub

| Proyecto | Licencia declarada | Qué aporta | Papel posible |
| --- | --- | --- | --- |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 y avisos de terceros | Sensores de temperatura, carga, frecuencias, voltaje y ventiladores; biblioteca .NET reutilizable por NuGet. | Integración para sensores, con estado explícito cuando un dato no esté disponible. |
| [PresentMon](https://github.com/GameTechDev/PresentMon) | MIT y avisos de terceros | Captura y análisis de tiempos por frame y latencias; consola con salida CSV. | Mediciones durante juegos y comparación de sesiones. |
| [systeminformation](https://github.com/sebhildebrandt/systeminformation) | MIT | Inventario y métricas mediante Node.js; declara soporte parcial en Windows. | Alternativa de colector si se elige Node; referencia de cobertura. |
| [UniGetUI](https://github.com/Devolutions/UniGetUI) | MIT | Interfaz para administradores de paquetes y sus actualizaciones. | Referencia de interfaz y de actualización de aplicaciones; no es un detector de drivers. |
| [Winutil](https://github.com/ChrisTitusTech/winutil) | MIT | Catálogo de instalación, ajustes y reparación de Windows. | Referencia de acciones que deben evaluarse individualmente. |
| [Optimizer](https://github.com/hellzerg/optimizer) | GPL-3.0 | Categorías históricas de mantenimiento y ajustes de Windows. | Referencia histórica: el repositorio está archivado desde el 20 de enero de 2026. |

LibreHardwareMonitor documenta que algunos sensores requieren privilegios de administrador y que la lectura varía por fabricante. No se puede prometer cobertura universal de temperaturas. [README oficial](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).

PresentMon documenta limitaciones de permisos y de instrumentación según API gráfica y configuración. Las métricas deben conservar su contexto; un porcentaje aislado de CPU o GPU no demuestra un cuello de botella. [README oficial](https://github.com/GameTechDev/PresentMon).

La [web oficial de Snappy Driver Installer Origin](https://www.glenn.delahoy.com/snappy-driver-installer-origin/) enlaza su código en SourceForge. Los repositorios GitHub encontrados no permiten atribuirles la condición de fuente oficial.

## Necesidades observadas en Reddit

Estas conversaciones aportan experiencias y necesidades de usuarios. No demuestran causalidad ni mejoras técnicas por sí solas.

| Conversación | Fecha | Señal de producto |
| --- | --- | --- |
| [Driver updaters](https://www.reddit.com/r/buildapc/comments/1424s74/driver_updaters/) | 6 de junio de 2023 | Reducir la fricción de buscar drivers en varios fabricantes. |
| [Problemas atribuidos a cambios de drivers](https://www.reddit.com/r/techsupport/comments/1c3z9ok/stupid_it_guy_came_to_fix_a_printer_for_my_mom/) | 14 de abril de 2024 | Registrar cambios y explicar opciones de recuperación. |
| [Identificar cuellos de botella](https://www.reddit.com/r/buildapc/comments/1ih8yxu/what_is_the_easiest_way_to_identify_any_potential/) | 4 de febrero de 2025 | Interpretar las métricas y ofrecer siguientes pasos concretos. |
| [Discusión sobre debloat](https://www.reddit.com/r/Windows11/comments/1m95ltl/please_dont_use_debloat_software_scripts_or/) | 25 de julio de 2025 | Explicar cada ajuste y sus consecuencias; la discusión contiene opiniones contrapuestas. |
| [Monitorización del PC](https://www.reddit.com/r/buildapc/comments/1s5zug1/which_monitoring_software_do_you_use_to_monitor/) | 28 de marzo de 2026 | Correlacionar temperaturas, frames y registros históricos. |

Inferencia a partir de estas conversaciones: una interfaz que reúna datos, explique la evidencia y permita comparar resultados resolvería varias de las fricciones observadas.

## Base técnica de Microsoft

- **Inventario de drivers:** `Win32_PnPSignedDriver` expone proveedor, versión, fecha y firma. [Documentación WMI](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/whqlprov/win32-pnpsigneddriver).
- **Actualizaciones aplicables:** Windows Update Agent permite consultar actualizaciones de tipo Driver y distinguir las opcionales. Una consulta sin resultados no prueba que ningún fabricante tenga otra versión. [IUpdateSearcher.Search](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatesearcher-search).
- **Fuentes de descarga:** Microsoft recomienda Windows Update y, cuando sea necesario, el fabricante para un driver compatible. [Microsoft Support](https://support.microsoft.com/en-us/windows/hardware/drivers/automatically-get-recommended-and-updated-hardware-drivers).
- **Fecha y compatibilidad:** Windows clasifica paquetes antes de desempatar por fecha y versión. Por tanto, la antigüedad por sí sola no permite etiquetar un driver como desactualizado. [Selección de drivers](https://learn.microsoft.com/windows-hardware/drivers/install/overview-of-the-driver-selection-process).
- **Mejoras guiadas:** Windows ofrece enlaces directos a inicio, almacenamiento, energía y actualizaciones opcionales mediante `ms-settings`. [Launch Windows Settings](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).
- **Métricas:** existen APIs y contadores de rendimiento para recopilar carga y consumo de recursos. [Performance Counters](https://learn.microsoft.com/en-us/windows/win32/perfctrs/about-performance-counters).

## Opciones de plataforma

- **C# con .NET 10 y WPF:** candidato para una app exclusiva de Windows por su integración con .NET y LibreHardwareMonitorLib. WPF es específico de Windows; .NET 10 tiene soporte LTS hasta noviembre de 2028. [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [soporte .NET](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support).
- **Tauri:** interfaz web sobre el webview del sistema; introduce Rust y un puente para las APIs de diagnóstico. [Documentación oficial](https://v2.tauri.app/start/).
- **Electron:** permite utilizar Node y systeminformation; distribuye Chromium y Node. [Documentación oficial](https://www.electronjs.org/docs/latest/).

La preferencia por C# y WPF es una decisión propuesta, no una comparación medida de consumo o rendimiento entre las tres opciones.

## Estado del proyecto

La carpeta contenía únicamente Git, sin código ni commits. La comprobación de herramientas encontró Node, Python y el ejecutable de .NET; `dotnet --list-sdks` no devolvió SDKs. Una implementación con .NET requerirá disponer del SDK.

La investigación no incluyó ejecutar scripts de los proyectos consultados ni aplicar ajustes al PC.
