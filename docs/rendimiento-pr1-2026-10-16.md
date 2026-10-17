# PR 1 · Datos básicos tempranos

Fecha: 2026-10-16, America/Santiago. Base de producto: `f6d2936`.
Punto limpio previo: `a8f40d7`. PR 2 pendiente de aprobación.

## Alcance y comportamiento

- `ScanProgress` transporta `MachineInfo` y una lista de unidades de solo lectura. Se publican al leer cada fuente, antes de consultar drivers; no se adelanta ni acorta el muestreo.
- Resumen muestra identidad, capacidad de RAM y unidades disponibles. Antes de la primera muestra los recursos dicen «Midiendo…»; después indican `n/10`, última lectura y análisis pendiente. Solo el snapshot terminado publica promedios y hallazgos.
- Preparar/aplicar la selección principal y aplicar la selección manual requieren un diagnóstico completo del equipo actual. La guarda también se comprueba en las llamadas directas, no solo en el botón. La limpieza y preferencia de GPU de Ajustes comparten la condición; inspección y recuperación siguen disponibles.
- Empezar otro análisis invalida el permiso de aplicación anterior. Cancelación o fallo descartan los campos parciales y las trazas de esa tentativa. Si hay un snapshot anterior, permanece identificado como último análisis guardado y no habilita Aplicar. Los callbacks de otra generación o cancelados se ignoran.
- Un constructor interno permite sustituir únicamente la lectura WMI en tests. Se conservan consultas, timeout, captura de errores por fuente y las diez muestras con sus fechas reales. No se introdujo paralelismo.
- No cambian BLOOM, animación, malla, optimizaciones, journal, respaldos, ayudante ni actualizador. La línea de unidades aparece durante la carga; al terminar queda en la información desplegable, preservando la composición normal de Resumen.

## Verificación funcional

SDK .NET 10.0.402. Antes: 663 pruebas correctas, una consulta OEM opcional omitida; portable anterior, 245 comprobaciones de interacción correctas.

Después: **666 pruebas correctas**, la misma OEM opcional omitida; **249 comprobaciones de interacción** correctas tanto en desarrollo como en el portable. Compilación de solución: cero errores y cero advertencias. Publicación autocontenida `win-x64` correcta.

Regresiones:

- Información básica publicada antes de entrar al proveedor de drivers; cancelar allí no retorna un snapshot.
- WMI vacío o denegado conserva notas explícitas y valores desconocidos, mientras las lecturas nativas disponibles completan diez muestras.
- No preparar/aplicar antes del primer diagnóstico; tampoco por llamada directa.
- Tres muestras rotuladas como parciales y recursos pendientes sin cifras inventadas.
- Cancelar descarta identidad, unidades y trazas parciales, sin habilitar Aplicar.
- Dos análisis seguidos y callback antiguo retrasado no mezclan generaciones.
- Reanalizar bloquea ambas selecciones ya preparadas; fallar conserva únicamente el snapshot anterior identificado y mantiene bloqueado Aplicar.

Se observó el fallo inicial de disponibilidad de Optimizar PC. También se retiró temporalmente la publicación de identidad para demostrar que la regresión del colector falla; restaurada, la batería completa vuelve a pasar. Una ejecución intermedia reutilizó la DLL de esa prueba negativa debido a su fecha de modificación y falló cuatro comprobaciones; se forzó la recompilación y se conservaron ambos logs. No se contó como pasada esa ejecución.

Los fixtures de aplicación existentes ahora suministran un diagnóstico completo. La comprobación del resultado en ventana mínima incluye el desplazamiento hasta ese resultado y conserva Aplicar fijo; antes probaba un estado sin diagnóstico que esta entrega ya no permite. La visibilidad de Optimizar PC al abrir Resumen sigue verificada por la prueba de inicio automático.

Revisión independiente de código de solo lectura: sin hallazgos críticos, importantes ni menores. No acredita una prueba manual, uso de un lector de pantalla ni cambios reales de Windows.

## Mediciones

Medianas y rangos mínimo–máximo de **tres repeticiones** sobre este equipo, con caché caliente. No son garantías para otros equipos ni mediciones de arranque en frío. Evidencias y `summary.json` en `artifacts/faseB-pr1/`.

### Protocolo y límites

1. **Portable:** mismo `scripts/measure-engine.ps1` y modo seguro `--performance-test` de la Fase A. Se alternó control/PR1 tres veces. Cada toma conserva 2,3 s de estabilización, 5 s de ventana activa y 3 s minimizada tras 0,5 s. Las seis tomas cumplieron el foco y entregaron cuadros. Este modo aplica el snapshot completo a la ventana; no ejecuta el arranque normal completo ni publica progreso parcial. Sirve para comparar arranque del contenedor y reposo, no para medir la mejora de información temprana.
2. **Hitos:** host auxiliar con los mismos recursos, MainWindow y `MainViewModel.ScanAsync`, colector real y reportes aislados; sin consulta OEM, claves/IA, actualización ni activación de perfiles. El reloj interno comienza en el host, después de cargar el runtime; no equivale al reloj externo desde lanzar el portable ni incluye preferencias/historial del arranque normal. Se midió el mismo host antes/después. El control final se compiló desde un `git archive f6d2936` con revisión informativa exacta. Las DLL auxiliares previas a U-01 se detectaron y sus tres tomas se excluyeron; se conservan.
3. **Disponibilidad y visibilidad separadas:** la primera captura de básicos mostraba solo la introducción de BLOOM. El binding ya tenía datos, pero ApplicationChrome aún era transparente. Se añadió observación pasiva de su propiedad Opacity a la sonda, sin alterar la app. «Visibles» exige los tres datos básicos, opacidad ≥0,98 y una pasada UpdateLayout en DispatcherPriority.Render; no mide el instante de salida de píxeles del compositor/GPU. Se repitieron los hitos con esta definición: `visibility-control-1/2/3` y `visibility-pr1-1/3/4`, todos claros. `visibility-pr1-2` se conserva como captura oscura separada. Las mediciones anteriores sin opacidad son preliminares y no se mezclan en el resultado final.
4. Las capturas se obtuvieron en el primer estado visible y a tres muestras. La captura clara de la primera toma ocurre después de registrar el hito de visibilidad; los datos auxiliares no se usan para comparar CPU/RAM. Las comparaciones de reposo provienen únicamente de los portables sin captura adicional. Se conservó la animación y su introducción.

### Hitos del flujo real de diagnóstico en la sonda

Segundos desde el reloj interno del host; **mediana [mín.–máx.]**.

| Hito | Control f6d2936 | PR 1 | Lectura |
| --- | ---: | ---: | --- |
| Identidad disponible en bindings | 13,844 [13,811–14,057] | 0,893 [0,887–0,930] | Se publica al leerla. |
| Capacidad de RAM disponible | 13,844 [13,811–14,057] | 0,893 [0,887–0,930] | No es porcentaje de uso ni promedio. |
| Unidades disponibles | 13,844 [13,811–14,057] | 0,902 [0,896–0,939] | Antes de drivers. |
| Tres datos básicos visibles, opacidad ≥0,98 y layout | 13,988 [13,942–14,193] | **1,672 [1,648–1,694]** | Información útil unos **12,3 s antes**, sin cambiar la introducción. |
| Primera lectura de uso de RAM | 4,370 [4,352–4,597] | 4,437 [4,415–4,510] | Sin adelanto demostrado; el inventario sigue antes de observar. |
| Diez muestras en las trazas | 13,822 [13,789–14,034] | 13,872 [13,864–13,951] | Sin mejora demostrada. |
| Resultado completo, layout | 13,988 [13,942–14,193] | 14,024 [14,014–14,102] | Sin mejora demostrada. |

Los JSON guardan las diez fechas reales de adquisición. No se redujeron las diez esperas de un segundo ni se presentaron tres muestras como promedio terminado. Estos hitos usan el flujo ScanAsync real en un host seguro; queda sin medir el arranque completo de producción con todas las preferencias del usuario.

### Portable y reposo con animación

CPU normalizada a los 12 procesadores lógicos del equipo. Working Set es memoria residente; Private Bytes es memoria privada comprometida: no se suman ni representan la misma medida.

| Métrica | Control f6d2936 | PR 1 |
| --- | ---: | ---: |
| Primera ventana UIAutomation, s | 1,078 [0,979–1,736] | 0,989 [0,984–1,066] |
| Análisis completo desde lanzamiento, s | 14,383 [14,140–15,280] | 14,098 [14,098–14,296] |
| Memoria residente, MiB | 190,5 [188,6–194,4] | 191,6 [191,3–198,0] |
| Memoria privada comprometida, MiB | 469,7 [444,9–509,5] | 469,2 [447,1–483,9] |
| CPU activa, % del equipo | 0,703 [0,599–0,729] | 0,755 [0,521–0,755] |
| Cuadros por segundo observados | 19,60 [19,40–19,80] | 19,60 [19,20–19,99] |

Los rangos se solapan: **no hay mejora demostrada de análisis total, CPU o memoria**, ni se atribuye la pequeña diferencia de medianas de arranque al PR 1. En las tres tomas minimizadas de cada portable se contabilizaron cero cuadros y 0 % de CPU en la ventana medida; esto no demuestra consumo absolutamente nulo fuera de la resolución del contador. No se midió GPU ni rendimiento de juegos/PC antes y después.

### Objetivos de Fase A

| Objetivo provisional, solo para este equipo | Estado con PR 1 |
| --- | --- |
| Primera información útil ≤2 s; ninguna de tres tomas >3 s | **Cumple en la sonda de visibilidad:** 1,672 s, máximo 1,694 s. No se acredita aún como garantía del arranque completo de producción. |
| Capacidad de RAM/unidades ≤2,5 s | Cumple en la misma sonda; incluidas en el primer contenido visible. |
| Primera ventana mediana ≤1,1 s | 0,989 s. El control también cumplía; sin mejora demostrada. |
| Análisis completo ≤12,5 s desde lanzamiento | No cumple: 14,098 s en portable. Muestreo conservado. |
| Residente ≤170 MiB | No cumple: 191,6 MiB. |
| Privada ≤350 MiB, provisional | No cumple: 469,2 MiB; atribución nativa pendiente. |
| CPU activa mediana ≤0,50 % | No cumple: 0,755 %. |

Los cambios de render y malla siguen fuera del alcance. PR 2 requerirá aprobación y su propia medición/commit; no se ha implementado.

### Integridad de las evidencias

- Portable de control conservado en `artifacts/fase2-motor/U01-final-portable/Silen.exe`, SHA-256 `29BDC60715E52236CA833837615F78EE5EA3EE099BF317871B59240A0E9F45B0`.
- Portable local de PR 1 en `artifacts/faseB-pr1/portable/Silen.exe`, SHA-256 `6D5019E99613CF7694EEE2B025CD8585564317267C205F1CFC00D25723E4D9FD`. Compilado con las fuentes de PR 1 antes del commit final; la revisión informativa embebida corresponde al checkpoint `a8f40d7`, no se usa como prueba de que el árbol estuviera limpio al compilar.
- Las fuentes, logs de compilación/pruebas, scripts de la sonda y todos los resultados intermedios quedan conservados. Los artefactos de diagnóstico están ignorados en Git; no se sustituyó la preversión del escritorio.

## Capturas y checklist de Resumen

Las capturas WPF con fixtures están en `artifacts/faseB-pr1/interactions-portable/interaction-fixtures/carga-*`. Los valores de estos fixtures son simulados. Capturas con datos del equipo real: `artifacts/faseB-pr1/visibility-pr1-1/01-basicos-visibles.png` (claro, datos básicos y 0/10) y `artifacts/faseB-pr1/visibility-pr1-2/02-tres-muestras.png` (oscuro, 3/10); sus carpetas incluyen los otros estados. No son capturas de pantalla obtenidas por un usuario ni prueban foco/hover manual.

- [x] Carga: información básica temprana, progreso, cancelación y muestras pendientes; cuatro capturas de básicos en claro/oscuro y ambos tamaños, y dos con tres muestras.
- [x] Vacío: desconocidos explícitos, WMI vacío y sin cifras ficticias.
- [x] Error/cancelación: parciales descartados; snapshot anterior identificado cuando existe.
- [x] Selección/aplicación: bloqueo inicial, durante diagnóstico y tras fallo; ambas selecciones se habilitan al completar, con servicios simulados.
- [x] Recuperación: batería existente con servicios simulados; ningún cambio al preflight ni al journal.
- [x] Claro/oscuro y tamaños 1240×820/1000×680: renders automáticos y comprobaciones de disponibilidad.
- [ ] Recorrido manual de hover, foco, teclado completo, lector de pantalla y alto contraste real.
- [ ] Movimiento/reducir animaciones: validación manual no realizada en este PR; código sin cambios.
- [x] Alcance: sin paralelismo PR 2 ni cambios a render, BLOOM o acciones del sistema.

Antes de publicar quedan las interrupciones reales del actualizador y decidir la instalación protegida del ayudante: [lista de pendientes](pendientes-antes-de-publicar.md). No se implementan aquí.
