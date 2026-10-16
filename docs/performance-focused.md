# Controles de rendimiento — 14 de octubre de 2026

Se retiró el flujo de capturar partidas, importar CSV, comparar sesiones y explicar métricas. PresentMon ya no se distribuye. La biblioteca y sus pruebas históricas permanecen en código, sin controles de captura en la app. Las lecturas del análisis de hardware siguen disponibles para diagnosticar el equipo.

Gaming ofrece Optimizar PC y una selección de controles reales, disponibles según el equipo. La confirmación usa una ventana WPF propia con propietario y botones Ahora no / Sí, optimizar. El icono Silen se integra en el ejecutable y la ventana, con siete tamaños de 16 a 256 px; la marca vectorial de la interfaz comparte el mismo dibujo. En Drivers, antes del análisis se muestra una acción para identificar el equipo en lugar de inventarios vacíos y controles inactivos.

## Controles y límites

- Modo Juego y desactivación de capturas Xbox: preferencias existentes, respaldadas.
- Game Bar: audio/cursor de grabación, apertura con mando y panel inicial. Referencia de configuración: [Optimizer, `DisableGameBar`, commit cc88a0e79bde103dd30dcb9fa1f166ac9ad5d924](https://github.com/hellzerg/optimizer/blob/cc88a0e79bde103dd30dcb9fa1f166ac9ad5d924/Optimizer/OptimizeHelper.cs). No se copió su código GPL ni se cambió la política HKLM.
- Segundo plano: preferencias de apps Store y Startup Boost/background de Edge. Pueden limitar notificaciones, sincronización y apps web al cerrar el navegador. El efecto de la preferencia Store depende de Windows.
- Efectos visuales: cinco pares documentados SPI_GET/SET para menús, combo boxes, listas, fades y ayudas, con aplicación inmediata y restauración individual. Composición del escritorio: transparencias y Aero Peek; puede requerir cerrar sesión. Inspiración: [WinUtil `WPFTweaksDisplay`, commit 07ccd8e2e755a706f31569808b31f5b77acad6a9](https://github.com/ChrisTitusTech/winutil/blob/07ccd8e2e755a706f31569808b31f5b77acad6a9/config/tweaks.json). Se usa la [API oficial SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow), conservando el estado real anterior; no se sobrescribe todo UserPreferencesMask ni se altera suavizado de fuentes.
- Energía: modo Mejor rendimiento mediante PowrProf cuando hay corriente y plan Equilibrado compatible. Se conserva el GUID anterior y se vuelve a comprobar alimentación antes de escribir; aplicación y restauración nativas requieren corriente. Si no está disponible, el perfil puede usar un plan Alto rendimiento ya existente. No crea planes Ultimate ni cambia controles térmicos del fabricante. [Microsoft: modo de rendimiento y overlays](https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider). El uso de energía puede incrementar consumo, temperatura y ruido.
- Fortnite: GPU de alto rendimiento por ejecutable y claves gráficas existentes de calidad baja. Juego cerrado, detección validada, respaldo exacto y preservación de resolución, renderizador, límite FPS y controles.

Publicidad, personalización diagnóstica, contenido promocional y búsqueda Bing ya no forman parte del perfil ni de sus opciones. Sus respaldos anteriores siguen siendo compatibles para restaurar. Los controles del escritorio afectan la respuesta/interfaz de Windows; no se anuncian como incrementos medidos de FPS. No se ejecuta WinUtil/Optimizer ni se necesita instalar otra app.

El journal guarda originales antes de mutar, comprueba cada escritura y preserva cambios externos al deshacer. Un resultado parcial conserva respaldo y no dice PC listo. La selección manual pide la misma confirmación que el botón automático.

## Actualizaciones dentro de Silen

Drivers empieza con Buscar actualizaciones. Cuando la consulta verificada a Windows Update no ofrece drivers, muestra «No hay actualizaciones de drivers disponibles en Windows Update» y oculta descarga/lista. Si hay ofertas, muestra Descargar y un desplegable con las disponibles y su estado. Una búsqueda incompleta muestra un error; no se presenta como ausencia de actualizaciones. Se retiraron los botones redundantes de apertura de Windows Update de esta pantalla.

La descarga usa [BeginDownload/EndDownload de Windows Update Agent](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatedownloader-begindownload), con una búsqueda nueva de drivers no instalados/ocultos y coincidencia exacta de ID/revisión. Los archivos quedan en la caché de Windows Update, sin instalación. No acepta licencias automáticamente; los paquetes que requieren licencia muestran ese requisito. Solo se marcan descargados los IDs confirmados; cancelación y fallos permiten reintentar.

Según los [permisos de WUA](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/secured-methods-and-properties-in-the-wua-api), descargar puede requerir administrador. Desde una sesión normal, Descargar abre únicamente Silen con UAC y el argumento fijo `--drivers`; mantiene la ventana original y permite reintentar si se cancela el permiso. La nueva ventana abre Drivers para buscar y descargar explícitamente. No instala una herramienta externa ni lanza un instalador de drivers.

El límite de 20 minutos y la cancelación acotan la espera de la interfaz incluso si [CleanUp de WUA sigue esperando](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-idownloadjob-cleanup). El trabajador conserva sus recursos hasta terminar y mantiene una exclusión de descarga durante la limpieza para evitar un reintento simultáneo en esa instancia.

La verificación real detectó un archivo de lock propio redirigido por el contexto de archivos del host Codex, pese a que el hijo no tenía identidad de paquete. SafeFiles permite únicamente la correspondencia exacta de AppData a la caché de una identidad de paquete informada por Windows para el proceso o su ascendencia real (máximo ocho niveles); valida y mantiene abiertos también los ancestros físicos del destino. Los otros enlaces, hardlinks y destinos siguen rechazados. Esta corrección permitió preparar el perfil en la verificación real sin aplicar ajustes al PC.

## Verificación final

Suite: 318 pruebas correctas, ninguna fallida y una omitida de red. Compilación/publicación sin errores ni advertencias. El ejecutable publicado pasó 105 comprobaciones de interacción y el diagnóstico real sin SDK en PATH: 10 muestras, 232 drivers, 28 paquetes del catálogo Lenovo y Fortnite detectado con 10 controles disponibles. Evidencia en `artifacts/performance-updates-final/`. No se aplicaron ajustes al PC, descargaron drivers, ejecutó UAC ni midieron ganancias de FPS: descarga/permisos/cancelación se verificaron con fixtures; el acceso a archivos y diagnóstico sí se verificó con el entorno real.

Revisión independiente de código: se corrigió el detalle ausente de errores de optimización, la espera de limpieza de WUA y la recuperación de permisos. La revisión final no dejó hallazgos críticos/importantes. Se inspeccionaron dos lotes visuales con tamaño normal y mínimo, estados de actualización, confirmación y el icono extraído del ejecutable. Se corrigió el singular del mensaje de una actualización y se priorizaron calidad/GPU/energía en la lista de Gaming; la selección mantiene el orden de aplicación del servicio.

ZIP final: 445 entradas, 67 704 267 bytes. Los ensamblados/executable, el recurso WinUtil y su licencia coinciden con el directorio publicado probado; PresentMon.exe no está incluido. SHA-256 de `artifacts/Silen-win-x64.zip`: `5EF3771CF68692CF40F683336084006E89619C7AA326695BDCDBD2E85220E042`.
