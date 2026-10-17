# PR 2 · Observación e inventario simultáneos

Fecha: 2026-10-17, America/Santiago. Base de trabajo limpia: `4c34a67`.
Control solicitado: `f377ab0035940fcd81207d012b5ca9503cf85bb0`, exportado sin
cambiar la rama activa. Artefactos: `artifacts/faseB-pr2-2026-10-17/`.

## Cambio y garantías

Tras leer identidad, RAM instalada y unidades, se inicia la observación mientras
un único trabajador consulta drivers/PnP, inicio, procesos y red. Máximo dos
tareas de trabajo por diagnóstico; no una tarea ilimitada por consulta. Cada
tarea posee sus listas/notas y se unen antes de devolver el resultado.

Progreso tipado serializado: 14 etapas completadas de forma monótona; las muestras
ya no dependen de acabar el inventario. Diez esperas de un segundo y diez fechas
reales de adquisición, conservadas. Los errores esperables siguen identificados
por proveedor. No se habilita Aplicar hasta obtener el snapshot completo actual.

Presupuesto cooperativo global de 35 segundos, con error explícito al agotarse;
distinto de la cancelación del usuario. Al cancelar/fallar se espera al trabajador
de inventario antes de retornar: no queda consultando en segundo plano. WMI
conserva su timeout de cinco segundos. Una llamada nativa en curso puede tardar
en observar la cancelación: no se promete un corte instantáneo ni un límite duro
de 35 segundos sobre controladores bloqueados.

No cambia el motor de ajustes, ayudante, journal, respaldos, BLOOM ni interfaz.
La base `4c34a67` ya incluía el pulido de Resumen posterior a `f377ab0`; esto se
declara al interpretar diferencias de memoria/renderizado frente al control.

## Pruebas

SDK .NET 10.0.402. Antes: 666 correctas, una OEM opcional omitida. Después:
**670 correctas**, una omitida. Compilación de solución: cero errores y cero
advertencias. Logs `tests-before.log`, `red.log`, `green-expanded.log`,
`tests-after.log` y `solution-build.log`.

- Con driver bloqueado llega una muestra; cancelar espera al trabajador.
- Todas las consultas reciben cancelación global, incluso sin token del usuario.
- Agotar el presupuesto devuelve un error literal, espera la limpieza del
  trabajador y no entrega un snapshot parcial.
- Driver denegado y PnP fallido conservan errores separados; el resto continúa.
- Los callbacks de progreso no se ejecutan simultáneamente; secuencia 1–14 y
  diez fechas separadas al menos 900 ms en la regresión con proveedores simulados.
- Diagnóstico real de solo lectura y regresiones anteriores de cancelación,
  vacío/denegación y descarte de generaciones; ninguna optimización aplicada.

Las pruebas de solapamiento y presupuesto fallaron antes de implementar. La
comprobación antigua de «sin muestras antes del paso 5» describía el orden
secuencial; se retira esa suposición, manteniendo la secuencia y el contenido.

## Medición del portable

Mismo `scripts/measure-engine.ps1` y modo seguro `--performance-test` de la Fase A:
tres repeticiones alternadas control/candidato, caché caliente, misma configuración
de publicación autocontenida y SDK, en el mismo PC. Estabilización 2,3 s, ventana
activa 5 s y minimizada 3 s tras 0,5 s. Las seis tomas conservan foco y cuadros.
No se compila ni ejecutan tests durante estas mediciones. Resultados completos y
hashes en `portable-*/` y `portable-comparison.json`.

| Métrica | f377ab0, mediana [mín.–máx.] | Candidato, mediana [mín.–máx.] |
| --- | ---: | ---: |
| Análisis completo desde lanzamiento, s | 14,503 [14,380–14,774] | **11,864 [11,863–12,076]** |
| Primera ventana UIAutomation, s | 1,032 [1,017–1,121] | 1,021 [1,011–1,097] |
| Residente, MiB | 188,68 [188,50–194,36] | 189,42 [189,20–191,62] |
| Privada comprometida, MiB | 457,34 [443,02–504,62] | 433,59 [432,64–435,75] |
| CPU activa, % del equipo | 0,312 [0,286–0,651] | 0,469 [0,469–0,495] |

La reducción del análisis es **2,639 s, aproximadamente 18,2 %**, con rangos
separados. Se conserva PR 2 según el criterio acordado. Ventana, residente y CPU
tienen rangos solapados: no se acredita mejora. La privada resultó menor en este
lote; no se atribuye al paralelismo, porque el control también precede al pulido
de UI y las superficies nativas no se han aislado. No se midió mejora de juegos
ni del rendimiento del PC.

Objetivos provisionales de este equipo: análisis ≤12,5 s alcanzado en las tres
tomas; residente ≤170 MiB y privada ≤350 MiB no alcanzados. La mediana de CPU
queda por debajo de 0,50 %, pero con una muestra corta y sin mejora demostrada
frente al control. No son garantías para otros equipos.

## Arranque y hitos

Tres repeticiones con la misma sonda compilada, alternando las DLL de f377ab0 y
del candidato. El host observa las propiedades reales del MainViewModel y la
opacidad de ApplicationChrome ≥0,98 más UpdateLayout. No acredita el instante de
presentación del compositor. Sus relojes comienzan en Main del host, después de
arrancar el runtime: no equivalen al lanzamiento externo del portable anterior.

| Hito del host, s | f377ab0 | PR 2 |
| --- | ---: | ---: |
| Identidad disponible | 0,998 [0,949–1,264] | 0,935 [0,927–1,067] |
| Unidades disponibles | 1,008 [0,959–1,274] | 0,951 [0,942–1,084] |
| Básicos visibles y layout | 1,755 [1,701–2,029] | 1,710 [1,703–1,854] |
| Primera muestra | 4,482 [4,415–4,845] | **2,150 [2,142–2,352]** |
| Diez muestras | 14,142 [14,063–14,499] | **11,805 [11,798–12,008]** |
| Resultado completo y layout | 14,311 [14,221–14,660] | **11,971 [11,971–12,168]** |

Los básicos ya eran tempranos en PR 1; sus rangos se solapan y no se les atribuye
una nueva mejora. El colector duró 13,295 [13,263–13,377] frente a 11,010
[11,000–11,075] s. Driver firmado: 1,912 frente a 1,901 s de mediana; PnP:
0,272 frente a 0,386 s. PnP se ralentiza en este lote, pero el tiempo total
disminuye de forma clara al solaparse con la observación. Se mantienen las diez
fechas reales de cada toma en `phases-*/phases.json`.

### Inicialización con dependencias de producción

El mismo host usa además el constructor público de MainViewModel y ejecuta
`InitializeAsync(autoScan: true)`, comprobación GitHub concurrente, preferencias
reales, consulta OEM real y `ApplicationTimerService.StartAsync` real. Los
delegados de lectura se envuelven para cronometrarlos sin sustituir sus resultados;
la sonda conserva su código y DLL exacta por variante. Las consultas WMI usan
el lector real y el mismo presupuesto; la envoltura registra únicamente clase
de proveedor y duración, sin contenido de las filas ni claves.

| Medida de las tres tomas, s | Mediana [mín.–máx.] |
| --- | ---: |
| Básicos visibles y layout | 1,722 [1,712–1,806] |
| Snapshot completo disponible | 11,968 [11,955–12,009] |
| InitializeAsync, duración | 13,846 [13,689–14,561] |
| Todo el arranque del host terminado | **14,721 [14,523–15,370]** |
| Colector dentro de InitializeAsync | 11,025 [11,011–11,033] |
| OEM, duración | 1,444 [0,939–1,617] |
| GitHub, duración concurrente | 0,471 [0,457–0,510] |
| Inicio de perfiles, duración | 0,012 [0,012–0,017] |

El ciclo completo con estos servicios tarda **2,750 s más** de mediana que el
host abreviado de PR 2 (11,971 s). No sumar medianas de fases: GitHub se ejecuta
en paralelo y los hitos usan otro origen que sus duraciones.

**Límites importantes:** se aisló el directorio de informes para no añadir tomas
al historial del usuario; su carga no representa un historial real grande. Las
preferencias y servicios son los de producción, pero sigue siendo un host, no
la ruta de entrada Program.Main/App.OnStartup del EXE. No se presenta 1,7 s como
tiempo de arranque completo del portable. Los tres intentos de GitHub terminaron
en error, incluido su tiempo; no se acredita una búsqueda exitosa ni se descarga
una actualización. Los perfiles cargados eran cero en las tres tomas: se midió
su inicialización vacía, no activación de perfiles con juegos abiertos. No se
aplicaron optimizaciones ni se enviaron conversaciones a IA.

La inicialización normal completa del EXE con historial del usuario, éxito de
GitHub y perfiles no vacíos sigue sin acreditarse por estas tomas; no se marca
como verificada por extrapolación. La lectura de teclado y la entrega portable
se registran aparte en el cierre de verificación.
