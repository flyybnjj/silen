# Silen

**Analiza tu PC, encuentra drivers oficiales y aplica ajustes de rendimiento para Windows y tus juegos desde una sola app.**

Aplicación de escritorio en español para **Windows 11 x64**, desarrollada en **C# y WPF sobre .NET 10**. La distribución portable incluye el runtime: los usuarios no necesitan instalar .NET, Optimizer ni WinUtil por separado.

## Descargar

El portable de Silen se genera como `artifacts/Silen-win-x64.zip` al ejecutar `scripts/build.ps1`. Las versiones publicadas están en [las releases del repositorio](https://github.com/flyybnjj/flyyoptimizer/releases); comprueba el nombre del paquete antes de descargarlo.

Extrae todo el ZIP y abre **Silen.exe**. Conserva la carpeta **Archivos** junto al ejecutable. También puedes consultar [las releases](https://github.com/flyybnjj/flyyoptimizer/releases).

## Funciones

| Área | Qué ofrece |
| --- | --- |
| Diagnóstico | CPU, GPU, RAM, almacenamiento, procesos, inicio, red y drivers instalados. |
| Identificación | Distingue portátil y escritorio e identifica fabricante, modelo y SKU. |
| Drivers | Comprueba pausas, políticas y servicio de Windows Update antes de buscar drivers. Permite solicitar su reanudación con respaldo. Consulta también el catálogo oficial Lenovo. |
| Optimizar PC | Reúne Modo Juego, capturas, segundo plano, red, servicios y energía en una sola selección. Permite localizar juegos/apps del registro, Steam y Epic y añadir su preferencia GPU reversible. |
| RAM y red | Memoria física/comprometida en vivo, procesos, paginación y recomendaciones según el equipo; comparación de DNS con consultas reales, medianas y fallos. |
| Ajustes desde el logo | Preferencias de Silen, perfiles de temporizador, limpieza y restauración. El modo experto permite abrir el optimizador manual. |
| Temporales | Selección confirmada, recuperación local y purga irreversible separada. El espacio se libera al purgar. |
| Bloom | Chat con Gemini, OpenAI o Anthropic; claves personales cifradas con DPAPI, diagnóstico mínimo autorizado y acciones tipadas confirmadas. |
| Restauración | Recupera ajustes respaldados cuando su estado actual permite restaurarlos. |
| Historial | Conserva diagnósticos locales y exporta informes JSON o Markdown. |
| Actualizaciones de la app | Exige un manifiesto ECDSA firmado antes de descargar e instalar una release de GitHub. |

## Cómo usarlo

1. Extrae todo el ZIP portable y abre **Silen.exe**.
2. Silen identifica el equipo al abrirse y muestra sus recursos en Resumen. Puedes cancelar el diagnóstico; si falla, aparece una acción para reintentarlo.
3. En **Drivers**, revisa primero el estado de Windows Update. Si está pausado o su servicio está detenido/deshabilitado, usa la acción de reanudación cuando quieras activarlo; puede solicitar administrador. Después pulsa **Buscar actualizaciones**. Un bloqueo o error no se presenta como ausencia de drivers.
4. Pulsa **Optimizar PC** en Resumen, revisa la selección y confirma **Aplicar selección**. La app muestra el resultado de cada paso.
   También puedes elegir entradas de inicio del usuario y limitar Chrome en segundo plano; ambas opciones requieren selección manual y guardan su estado anterior. No cierran apps ni pestañas abiertas.
5. En **Elegir juego o aplicación**, busca el producto instalado, localiza sus ejecutables y elige el principal. También puedes elegir un .exe manualmente. Prepara de nuevo la selección para revisar GPU, temporizador experimental y entrada. Los ajustes de teclado y ratón son globales y permanecen hasta restaurarlos; no cambian el polling USB.
6. Pulsa el logo **silen** para abrir **Ajustes**. Allí puedes recuperar temporales, restaurar ajustes y desactivar perfiles de temporizador. **Modo experto** muestra el **Optimizador manual**: prepara su propia selección y pulsa **Aplicar selección manual**. Activar este modo no modifica Windows. Los informes se exportan desde **Historial**.

Tras aplicar cambios puedes elegir **Reiniciar ahora** o **Al cerrar Silen** y cancelar esa elección. Al cerrar se confirma de nuevo. No todos los ajustes requieren reinicio; cada opción explica cuándo se refleja. La app no fuerza el cierre de otras aplicaciones.

El temporizador consulta el mínimo admitido por Windows y solicita ese intervalo
solo mientras el ejecutable elegido está en ejecución y Silen sigue abierto.
Guarda el perfil para próximas sesiones y libera su propia solicitud al cerrar el
producto, desactivar el perfil o salir de Silen. Es experimental: Windows 11 no
garantiza que la solicitud de Silen cambie la temporización de otro proceso.
[Alcance, entrada y restauración](docs/temporizador-entrada-experto.md).

La descarga de drivers guarda los paquetes en la caché de Windows Update; la instalación se realiza mediante Windows Update o el proveedor oficial. Solo las operaciones que lo requieren solicitan un ayudante elevado para una acción validada; la interfaz y Bloom permanecen sin permisos de administrador.

En **Bloom**, elige proveedor y modelo y guarda tu clave en el campo protegido.
Los mensajes se envían al proveedor elegido; el diagnóstico mínimo requiere
consentimiento antes de recopilarse. El chat permanece en memoria y las claves
se cifran para tu usuario. Cada acción necesita confirmación local. Las cuotas
y cargos dependen del proveedor. [Uso, límites y privacidad](docs/bloom.md).

En **RAM y red**, las lecturas de memoria se actualizan cada dos segundos mientras
la ventana está activa. **Actualizar detalles** consulta procesos y paginación.
**Medir DNS ahora** compara tiempos de resolución desde tu conexión con varias
consultas por servidor. No cambia tus DNS ni garantiza menor ping de juegos.
[Fuentes y repositorios revisados](docs/research/2026-10-15-ram-dns-repositorios.md).

## Portable ordenado

```text
Silen-win-x64/
├── Silen.exe
└── Archivos/
    ├── README.md
    ├── THIRD-PARTY-NOTICES.md
    ├── licenses/
    └── tools/
```

El ejecutable integra .NET y las bibliotecas de la aplicación. **Archivos** reúne los recursos y las licencias. Conserva ambos al mover o compartir la app.

## Ajustes de rendimiento

Según la compatibilidad del equipo, Silen permite:

- Activar Modo Juego y reducir la grabación y la actividad de Xbox Game Bar.
- Reducir aplicaciones y procesos en segundo plano de Store y Edge.
- Reducir animaciones, transparencias y otros efectos del escritorio.
- Usar **Mejor rendimiento** con corriente, o un plan **Alto rendimiento** ya existente cuando corresponda.
- Establecer la preferencia de GPU de alto rendimiento para el ejecutable elegido; Windows y el hardware determinan su aplicación efectiva.
- Limitar las descargas de Delivery Optimization en segundo plano al 10 % del ancho de banda estimado; no limita otros launchers ni garantiza menor ping.

Los cambios se comprueban y guardan su estado anterior. La selección de producto no edita sus archivos internos ni anti-cheat. Teclado y ratón conservan todos sus campos originales en el respaldo; el temporizador tiene su propio control para desactivar el perfil. El efecto de cada ajuste depende del hardware y de la carga del equipo; no se promete una cantidad de FPS. Los respaldos históricos siguen disponibles para restaurar.

Los ajustes de registro restringidos por Windows, como las políticas de Edge, se comprueban antes de seleccionarlos. Si faltan permisos, aparecen desactivados con su explicación; los demás ajustes disponibles se pueden aplicar. [Corrección y verificación](docs/edge-permissions-fix.md).

## Actualización integrada

Desde **1.1.0**, Silen busca nuevas releases estables de este repositorio al abrirse y descarga automáticamente el portable si existe una versión superior. La barra lateral muestra el progreso y, cuando está listo, **Reiniciar y actualizar**. El reinicio requiere pulsar ese botón y espera a que la aplicación esté libre.

La descarga usa GitHub sin tokens, no envía el diagnóstico del equipo y exige el nombre de archivo, tamaño y SHA-256 publicados. El reemplazo conserva una copia para restaurar si falla; los informes y respaldos locales quedan fuera de la instalación. Si no hay conexión o Windows impide el reemplazo, se informa del problema. La comprobación de integridad depende del repositorio oficial y no equivale a una firma Authenticode.

Las actualizaciones se aplican desde una sesión normal de Silen. Si la abriste como administrador, ciérrala y ábrela normalmente para reiniciar y actualizar.

**Cambio de nombre:** si utilizas FlyyOptimizer 1.0 o 1.1.0, extrae el portable de Silen en una carpeta nueva y abre `Silen.exe`. El actualizador antiguo exige el nombre anterior y no puede instalar directamente un ZIP de Silen. Los accesos directos anteriores deben apuntar al nuevo ejecutable. Las siguientes actualizaciones de Silen usarán `Silen-win-x64.zip`.

El repositorio conserva su dirección `flyybnjj/flyyoptimizer`. Es un identificador externo real; el cambio de marca local no renombra el repositorio ni publica una release.

## Compilar desde el código

Requisitos de desarrollo: **Windows 11 x64**, **Git** y **SDK .NET 10**.

```powershell
git clone https://github.com/flyybnjj/flyyoptimizer.git Silen
cd Silen
.\scripts\build.ps1
.\scripts\run.ps1
```

El script ejecuta las pruebas, compila y genera:

```text
artifacts/Silen-win-x64/
artifacts/Silen-win-x64.zip
```

El SDK se necesita para desarrollar y compilar; el runtime viene integrado en la distribución para usuarios. Los archivos de `artifacts/` quedan fuera del repositorio.

## Verificar

```powershell
dotnet test Silen.sln -c Release
.\scripts\verify-portable.ps1
```

La verificación portable compara los archivos del ZIP con la publicación y ejecuta la copia extraída desde una ruta con espacios, sin buscar el SDK. Comprueba el análisis, la interfaz y los contratos de elevación sin aplicar optimizaciones ni descargar drivers.

## Datos y compatibilidad

En instalaciones nuevas, los diagnósticos y respaldos se guardan en `%LocalAppData%\Silen`. Si existe `%LocalAppData%\FlyyOptimizer`, Silen conserva esa ubicación para compartir el historial, los respaldos y sus bloqueos con la versión anterior. No mueve ni duplica los datos; si existen ambas carpetas, usa la anterior. La pantalla Historial indica la ubicación utilizada. La consulta por modelo no envía la serie del equipo; la búsqueda por serie es una acción explícita y la serie no se guarda en los informes.

Las temperaturas dependen de los sensores accesibles. El análisis no instala controladores de sensores. La consulta de Windows Update respeta las políticas del sistema; el catálogo Lenovo muestra paquetes del modelo y no determina por sí solo que un driver instalado esté desactualizado. Para otros fabricantes se ofrece su portal oficial cuando no hay un catálogo integrado verificado.

Los temporales retirados se conservan en recuperación local hasta restaurarlos o purgarlos. Otros limpiadores o Windows pueden retirar esa carpeta temporal: no sustituye un respaldo permanente. La purga no se puede deshacer. Los ajustes de energía pueden aumentar consumo, ruido y temperatura. Esta versión necesita validación en más modelos de hardware.

## Documentación y referencias

- [Guía detallada de uso](docs/user-guide.md)
- [Actualizaciones integradas y verificación](docs/github-updates.md)
- [Controles de rendimiento y Fortnite](docs/performance-focused.md)
- [Herramientas nativas y restauración](docs/native-tools.md)
- [Interfaz y animaciones](docs/apple-theme-motion.md)
- [Investigación de GitHub, Reddit y Microsoft](docs/research/2026-10-13-referencias.md)
- [Dependencias, atribuciones y licencias de terceros](THIRD-PARTY-NOTICES.md)

La selección de ajustes toma como referencia **Optimizer**, **Chris Titus Tech WinUtil** y la documentación de Microsoft. Silen implementa sus propios controles nativos; la selección fijada de WinUtil y sus atribuciones se incluyen en el proyecto.
