# Silen

App de escritorio en español para Windows 11 x64. Analiza el equipo, muestra evidencia de oportunidades de rendimiento, reúne drivers oficiales y aplica ajustes reversibles para Windows y cualquier juego.

La interfaz usa superficies claras y tipografía Source Serif 4. El movimiento respeta la preferencia de animaciones de Windows y el modo de alto contraste.

## Abrir la app

En la distribución portable, extrae el ZIP y abre `Silen.exe`. Solo verás el ejecutable con el logo de Silen y una carpeta `Archivos` con los recursos, instrucciones y licencias. Conserva ambos al mover o compartir la app.

```text
Silen-win-x64/
├── Silen.exe
└── Archivos/
```

El ejecutable incluye .NET y las bibliotecas necesarias; el usuario no necesita instalar SDK, WinUtil, Optimizer ni utilidades aparte. Las bibliotecas nativas se extraen automáticamente en la caché temporal de .NET al arrancar, sin llenar la carpeta portable. La publicación usa [el formato de archivo único de Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) y conserva el arranque directo y la elevación desde el mismo ejecutable.

Desde el proyecto:

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

Para compilar necesitas el SDK .NET 10. El script también reconoce el SDK aislado de Codex en `%USERPROFILE%\.codex\runtimes\dotnet-10-sdk`. El SDK se puede descargar desde [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0).

## Uso

1. Abre Silen: el diagnóstico empieza automáticamente. Sigue las etapas y los recursos en vivo mientras se identifica si es portátil o escritorio, el fabricante, el modelo comercial y el SKU. Las lecturas son reales y puedes cancelar. Si falta el diagnóstico por un fallo o cancelación, puedes reintentarlo.
2. Revisa **RAM y red** y **Ajustes**. La memoria se mide en vivo mientras la ventana está activa. Actualizar detalles consulta procesos y paginación; Medir DNS ahora compara tiempos de resolución reales sin cambiar la configuración de red. Los ajustes elegidos guardan su estado anterior para deshacerlos.
3. En **Drivers**, pulsa **Buscar actualizaciones**. Si Windows Update ofrece drivers, aparece **Descargar** y una lista desplegable. Descargar guarda los archivos sin instalarlos; solicita permisos solo para esa operación. La interfaz y Bloom permanecen sin elevar. Para Lenovo, el análisis consulta también el catálogo oficial del modelo/SKU. La fecha de un driver no demuestra que esté desactualizado.
4. Pulsa el logo **silen** para entrar en Ajustes. **Limpiar archivos temporales (tamaño)** revisa y confirma un lote de archivos de al menos 24 horas, y los mueve a recuperación local. Puedes restaurar los seleccionados sin sobrescribir archivos existentes. Solo **Eliminar definitivamente** libera espacio y no se puede deshacer. La recuperación está dentro de temporales: Windows u otros limpiadores pueden retirarla; no es un respaldo permanente.
5. Pulsa **Optimizar PC** en Resumen para preparar una selección sin cambiar Windows. Revisa sus efectos, marca las opciones disponibles y pulsa **Aplicar selección**. La confirmación enumera lo elegido. El resultado distingue cambios aplicados, ajustes que ya estaban así, omisiones y errores. Los respaldos aparecen en Ajustes, en **Cambios y estado anterior**. Los controles generales para cualquier juego también están en este flujo.
6. Usa **Historial** para volver a un diagnóstico guardado y exporta un informe JSON o Markdown.

## Optimizar PC y elegir una aplicación

**Optimizar PC** ofrece 20 opciones generales según el equipo y entradas individuales de inicio. Elegir un ejecutable añade GPU, temporizador experimental y dos opciones de entrada. No requiere un juego ni un diagnóstico previo. Solo aplica la selección confirmada; temporizador, entrada, Chrome en segundo plano, apps de inicio, opciones avanzadas, servicios, capturas y efectos visuales quedan sin marcar inicialmente. Con corriente puede ofrecer el modo Mejor rendimiento o un plan Alto rendimiento existente. Comprueba disponibilidad y permisos de nuevo al aplicar y conserva los valores originales. Consulta el [catálogo y la restauración](optimizacion-pc.md).

El temporizador guarda un perfil por ruta del ejecutable. Mientras Silen está
abierto, detecta ese proceso cada dos segundos, solicita el intervalo mínimo
admitido y libera su propia solicitud al terminar. Puedes desactivar el perfil
desde la selección del producto o desde Ajustes, incluso si se desinstaló.
Windows 11 no garantiza un efecto sobre otro proceso; el estado muestra lo
solicitado y lo informado por Windows sin prometer menor latencia.

FilterKeys aplica aceptación de 0 ms, repetición de 130/10 ms y rebote de 0 ms,
sin el atajo ni sonidos por pulsación. El control de ratón pone aceleración y
umbrales en cero, sin modificar sensibilidad, DPI ni frecuencia USB. Ambos son
preferencias globales de Windows, no perfiles privados del juego, y permanecen
hasta restaurarlos en Ajustes. Algunas apps con entrada directa pueden ignorarlos.

En Ajustes, **Modo experto** guarda una preferencia de Silen y muestra
**Optimizador manual**. Este botón revela los controles individuales existentes
de Windows, energía e inicio. Activar el modo no aplica cambios. Limpieza,
restauración y desactivación de temporizadores siguen disponibles en modo normal.

En las opciones de inicio, elige solo apps que no necesitas al entrar en Windows.
El efecto corresponde a próximas sesiones; no se cierran procesos actuales ni se
eliminan instalaciones. Se retira la entrada del usuario registrada; Windows puede
tenerla deshabilitada en Configuración y la app puede tener otras reglas de inicio.
La actividad aparece como sin verificar, sin atribuir consumo por la presencia de
la entrada. Si cambia mientras revisas, Silen
la conserva y pide preparar otra vez. Puedes recuperar su comando exacto en Ajustes.
El control de Chrome al cerrarlo limita su modo de segundo plano y puede afectar
apps web o extensiones; no cierra pestañas. Si no se detecta Chrome o una política
del equipo controla la opción, Silen lo indica antes de aplicar.

La pestaña Gaming se retiró: sus ajustes ya están en el optimizador principal. En **Elegir juego o aplicación**, pulsa Buscar productos instalados, filtra por nombre, elige el producto y pulsa Localizar ejecutables. Selecciona el principal y prepara de nuevo las opciones. Puedes elegir manualmente un .exe para productos no detectados. Windows y el hardware determinan la GPU efectiva; vuelve a abrir la app después del cambio. Los demás pasos son generales de Windows, no ajustes internos del producto.

El perfil de Fortnite también se retiró de la interfaz. La detección es general para aplicaciones del registro y bibliotecas Steam/Epic. No se ejecutan los archivos encontrados ni se modifican sus configuraciones o anti-cheat. Los respaldos históricos siguen disponibles en Ajustes. La app no promete una mejora de FPS sin medirla.

En **Drivers**, el estado de Windows Update distingue pausa y fecha, servicio detenido o deshabilitado, políticas y lectura desconocida. La consulta no muestra un fallo como si no hubiera drivers. La acción de reanudación solicita administrador cuando hace falta, guarda los valores locales y el estado previo del servicio y comprueba el resultado. No elimina políticas de organización. Si Windows sigue informando una pausa, se explica que la reanudación no quedó confirmada; revisa Windows Update. Los cambios respaldados se pueden restaurar en Ajustes.

## Qué puede comprobar

CPU, GPU, RAM, unidades y espacio libre, procesos por memoria, aplicaciones de inicio, redes, plan energético, drivers instalados y códigos de incidencia. Las temperaturas y otras lecturas dependen del hardware y de los permisos de sus proveedores.

El umbral orientativo de temperatura es 90 °C en tres lecturas. No es el límite térmico universal ni confirma throttling; revisa las especificaciones del modelo. CPU y memoria se señalan por carga media sostenida en varias muestras; una observación corta no determina la causa de todos los problemas.

El análisis usa WMI y sensores opcionales de [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Las lecturas GPU usan las API accesibles de AMD/NVIDIA. Los sensores de CPU que requieren un controlador de bajo nivel están deshabilitados; se puede obtener temperatura de CPU cuando ya está expuesta por un proveedor WMI de LibreHardwareMonitor u OpenHardwareMonitor en ejecución. La app mantiene el resto de los datos y explica qué no estuvo disponible. No instala ni activa un controlador de sensores.

La búsqueda de drivers respeta las fuentes y políticas de Windows Update. No encontrar ofertas no descarta otra versión compatible del fabricante. La instalación se realiza mediante Windows Update o el proveedor oficial.

## Identificación y soporte oficial del equipo

La clasificación usa el tipo de sistema y su chassis. Una batería de UPS conectada a un escritorio no lo convierte en portátil. Si Windows no aporta evidencia suficiente, se muestra tipo desconocido.

El catálogo Lenovo se resuelve con modelo o SKU y se verifica contra el nombre del producto oficial. Sus paquetes son los publicados para ese modelo: pueden incluir variantes de componentes o paquetes opcionales y no equivalen a actualizaciones confirmadas para cada dispositivo instalado. Revisa el sistema operativo y la ficha antes de instalar.

**Buscar por serie** es una acción explícita. Solo entonces se lee la serie de BIOS y se consulta al fabricante oficial. La interfaz la enmascara; no se guarda en el diagnóstico, historial ni exportaciones. La consulta normal por modelo no envía la serie. Las series vacías o genéricas de BIOS se descartan.

Otros fabricantes ofrecen su portal oficial cuando no hay un servicio de catálogo verificado. No se presenta una búsqueda genérica como resultado específico del modelo. Los drivers genéricos de Microsoft dirigen al Catálogo Microsoft Update; soporte Surface corresponde solo a un equipo Surface identificado.

## Actualizar Silen

Desde 1.1.0, al abrir la app se comprueba la última release estable del repositorio oficial. Si existe una versión superior, el portable se descarga automáticamente. La barra lateral muestra el progreso y, cuando el archivo está verificado, ofrece **Reiniciar y actualizar**. Pulsa el botón cuando hayan terminado el análisis y las demás operaciones. La app no reinicia por su cuenta.

Si la consulta o descarga falla, la app sigue disponible y puedes reintentar desde la barra lateral. Si está al día, muestra que no hay actualizaciones disponibles. Las actualizaciones no necesitan una cuenta de GitHub ni tokens.

Para aplicar el paquete usa una sesión normal, sin permisos de administrador, y una carpeta portable donde tengas permiso de escritura. Los informes y respaldos de ajustes no se reemplazan. Un fallo durante el reemplazo intenta restaurar la instalación anterior y muestra el problema; no hay recuperación automática ante un corte eléctrico durante el reemplazo.

**Si vienes de FlyyOptimizer**, extrae el portable de Silen en una carpeta nueva y abre **Silen.exe**. La primera instalación de Silen es manual: el actualizador anterior no acepta el paquete renombrado. Conserva **Silen.exe** y **Archivos** juntos. Silen reutiliza `%LocalAppData%\FlyyOptimizer` si existe, para mantener el historial y las restauraciones; en instalaciones nuevas usa `%LocalAppData%\Silen`. El repositorio de [releases oficiales](https://github.com/flyybnjj/silen/releases) ahora está en `flyybnjj/silen`.
## Datos locales

Las preferencias de apariencia incluyen Claro, Oscuro y Sistema; el alto contraste de Windows tiene prioridad. Los respaldos privilegiados nuevos se guardan en ProgramData con ACL protegidas y se consultan desde **Cargar respaldos del sistema**. Un journal editable del usuario no puede autorizar cambios elevados.

Los diagnósticos y el registro de guías se guardan en la subcarpeta `reports` de la ubicación indicada en Historial: `%LocalAppData%\Silen`, o `%LocalAppData%\FlyyOptimizer` cuando ya existe. No se suben automáticamente. Los nuevos informes guardados y exportados omiten comandos y argumentos de inicio; contienen hardware, modelo/SKU y nombres de procesos: revísalos antes de compartirlos. No se reescriben informes antiguos. Bloom comparte únicamente el diagnóstico reducido autorizado descrito en [su guía](bloom.md).

Un informe dañado no oculta los otros informes válidos. La app conserva un registro de guías dañado y muestra el error en lugar de sobrescribirlo silenciosamente.

Varias instancias pueden escribir y refrescar las guías compartidas sin sobrescribir los registros de otra instancia.

Las herramientas de optimización guardan respaldos locales para restaurar ajustes y entradas de inicio. Esos respaldos conservan los valores y comandos originales necesarios para deshacer. Un cambio externo posterior puede impedir la restauración automática para no sobrescribirlo. El diagnóstico no modifica Windows ni limpia archivos. Al abrir Silen solo se reactivan automáticamente los perfiles de temporizador que hayas confirmado previamente, cuando está ejecutándose el producto correspondiente. Estos perfiles y el modo experto se guardan en la ubicación local de Silen, fuera del portable.

## Verificación desde código

```powershell
dotnet test Silen.sln
dotnet build Silen.sln -c Release
dotnet run --project src/Silen.App -- --scan "$env:TEMP\silen-scan.json"
dotnet run --project src/Silen.App -- --smoke-test "$env:TEMP\silen-smoke"
```

Estos comandos no aplican optimizaciones al equipo. Los modos `--render <png>` y `--smoke-test <directorio>` permiten revisar el render real de WPF. El modo de prueba usa un historial aislado dentro del directorio de salida.

Después de compilar, `./scripts/verify-portable.ps1` comprueba la estructura de dos elementos, las licencias y el recurso fijado; compara todos los archivos del ZIP con la publicación y ejecuta el ZIP extraído desde una ruta con espacios, sin búsqueda del SDK y con un directorio de trabajo diferente. Verifica el análisis, la interfaz y los contratos de elevación sin aplicar optimizaciones, descargar drivers ni abrir UAC.

## Referencias

Consulta [la investigación de GitHub, Reddit y Microsoft](research/2026-10-13-referencias.md), [las herramientas integradas](native-tools.md), [el colector incluido](bundled-gaming.md), [el tema y movimiento](apple-theme-motion.md) y [la verificación de esta versión](self-contained-verification.md). Los proyectos usados como inspiración no se ejecutan ni importan como presets.

Las dependencias y avisos están en [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). Esta primera versión requiere pruebas adicionales en otros modelos de hardware antes de una distribución amplia.
