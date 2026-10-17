# Revisión de código, seguridad y fiabilidad · 2026-10-17

## Resultado

Base revisada: `6910d16`. Se corrigieron cuatro fallos reproducibles mediante
servicios simulados y archivos descartables, con un commit por hallazgo.
No se cambiaron diseño, catálogo de optimizaciones, permisos de UAC ni datos
del sistema del usuario. No se usaron claves reales ni conversaciones remotas.
La revisión es focalizada; no certifica la ausencia de vulnerabilidades.

## Hallazgos y correcciones

Las líneas de origen se refieren a la base 6910d16; las de corrección, a d748cb5.

| ID | Área | Gravedad | Evidencia de origen | Problema | Corrección y evidencia actual | Commit |
| --- | --- | --- | --- | --- | --- | --- |
| RC-01 | Integridad de Deshacer | Alto | `src/Silen.Windows/OptimizationJournal.cs:9`, `:13`, `:23`, `:77`; `NativeOptimizationPlatform.cs:17` | El parser inventaba un ID/estado si faltaban y aceptaba `Previous:{}` como ausencia. Un valor numérico omitido se convertía en cero. Un respaldo incompleto podía autorizar una restauración distinta de la original. | Id/State obligatorios; parámetros de constructor requeridos y Number explícito si el valor existe y es DWord/QWord. `OptimizationJournal.cs:9`, `:25`, `:97`, `:161`. Rechazo antes de escribir, archivo conservado. | `972b5f3` |
| RC-02 | Migración de respaldos | Medio | `src/Silen.Windows/OptimizationJournal.cs:101` | Una cuarentena antigua impedía copiar un segundo origen diferente; después se filtraban las entradas legadas y una escritura podía perderlas. | Cada origen distinto conserva copia por hash, sin sobrescribir la primera. Temporal, flush y renombrado antes de filtrar; mismo origen reutiliza copia verificada. `OptimizationJournal.cs:109`, `:120`, `:152`. | `4dbfb86` |
| RC-03 | Privacidad de Bloom | Medio | `src/Silen.Windows/BloomWindowsTools.cs:52`, `:84`; `src/Silen.Core/BloomConversation.cs:78`, `:89` | Errores de IO de aplicar/deshacer podían contener rutas privadas; el resultado completo terminaba en el contexto enviado al proveedor. También faltaba separar errores de preparación locales del contexto remoto. | Lista positiva central de salida: resumen tipado, estados, verificación y contadores; sin títulos, rutas, ID de respaldo o mensajes libres. El detalle sigue local mediante MessageAdded. `BloomResultPrivacy.cs:9`, `BloomConversation.cs:78`, `:89`. Conserva Uncertain y MayHaveChanged. | `83fbb4f` |
| RC-04 | Robustez del historial | Medio | `src/Silen.Core/ReportStore.cs:135`, `:140`, `:149` | Lectura completa sin límite de archivos locales dañados o alterados; el escritor podía crear archivos sin un límite coherente. | Máximo 16 MiB por archivo al abrir y durante la lectura, también si crece. Mismo máximo UTF-8 antes de escribir; rechazo sin truncar ni reemplazar el anterior. `ReportStore.cs:10`, `:137`, `:157`. | `d748cb5` |

RC-01 no demuestra escalada de privilegios: el journal de máquina requiere
acceso elevado para alterarlo. La gravedad corresponde al riesgo de pérdida de
estado al deshacer un respaldo corrupto, también por corrupción accidental.
RC-04 es agotamiento de recursos en el proceso del usuario; no ejecución remota.

## Qué se conservó

- Canal del ayudante ligado a PID, SID, sesión, ejecutable y digest aprobado;
  catálogo de acciones tipadas, sin comandos arbitrarios.
- Separación de autoridad usuario/máquina y política de DLL de System32.
- Acuse de operación independiente del historial paginado, y estados de
  verificación incierta. Las correcciones nuevas no borran respaldos.
- Limpieza con identidad de archivo, exclusión de enlaces y recuperación local.
- Claves DPAPI por proveedor; endpoints permitidos, redirecciones restringidas,
  plazos y límites de respuesta; confirmación local por herramienta de Bloom.
- Firma/hash/tamaño de actualización y conservación de versión anterior hasta
  acuse de arranque. No se ejecutó una actualización real del usuario.
- Datos básicos tempranos y bloqueo de Aplicar hasta diagnóstico actual completo.

Estas conclusiones combinan inspección de fuente y regresiones existentes. No
equivalen a probar todos los flujos con permisos reales o hardware distinto.

## Pruebas

Evidencia: `artifacts/revision-codigo-2026-10-17/`.

- Antes: **673 correctas**, una OEM opcional omitida (`tests-before.log`).
- Después: **685 correctas**, la misma omitida (`tests-after.log`):
  138 Core, 490 Windows, 57 Gaming. Sin fallos.
- RC-01: cuatro pruebas fallaron antes y pasan después: omisión de Id, State,
  Previous.Exists y Previous.Number; se comprueba rechazo y ausencia de cambios.
- RC-02: segunda fuente legada diferente; ambas copias exactas permanecen tras
  aplicar un ajuste de usuario en la plataforma simulada.
- RC-03: tres resultados con rutas/mensajes adversarios y un fallo de preparación;
  el cliente simulado no recibe el marcador privado. El detalle local permanece.
  Se comprueba conservación de incertidumbre. 42 pruebas Bloom focalizadas verdes.
- RC-04: lectura sobredimensionada, registro de acciones sobredimensionado y
  escritura UTF-8 excesiva; no se reemplaza ni borra el archivo anterior y el
  historial válido sigue accesible. 24 pruebas de informes verdes.
- Las revisiones independientes de las correcciones fueron solo de lectura y no
  identificaron bloqueos. Las compilaciones y pruebas las ejecutó el agente principal.
- Una compilación intermedia de RC-02 falló por un Span atravesando un await;
  corregido antes del commit y repetido correctamente. Log conservado, no contado
  como verificación exitosa.
- Consulta de paquetes directos y transitivos con .NET/NuGet: no reportó
  vulnerabilidades conocidas en esta fecha; `dependencies.json`. No es una
  garantía sobre fallos desconocidos ni un análisis del historial de secretos.

## Compatibilidad y límites

Los respaldos válidos del escritor actual incluyen los campos ahora exigidos;
`Exists:false` conserva sus campos opcionales. Un respaldo incompleto se rechaza
y queda intacto: no se rellena para hacerlo parecer recuperable.
El historial mayor de 16 MiB se conserva, pero debe revisarse antes de volver a
abrirlo o añadirle registros; no hay borrado automático. ListAsync mantiene su
conducta de omitir informes inválidos conservando los válidos. No se añadió una
nueva interfaz de reparación o migración masiva.

La lectura acotada añade un almacenamiento intermedio de hasta 16 MiB por archivo,
además del texto y objetos deserializados. No se afirma ahorro de RAM ni CPU.
El número total de informes y el coste de recalcular hallazgos siguen creciendo
con el historial. Un índice/paginación y cancelación de toda esa lectura son una
posible tarea posterior, con medición antes/después y compatibilidad preservada.

La ubicación protegida y firma del ayudante, campaña real de interrupciones del
actualizador y ensayo de Recuperación en VM siguen pendientes conocidos de
publicación. No se implementan por inferencia dentro de esta revisión.

## Arranque y entrega

EXE de control 0889f1e frente a d748cb5; tres arranques normales alternados por
versión, misma publicación autocontenida y PC, caché caliente. Sin builds ni
tests durante las tomas. Sonda `scripts/measure-startup.ps1`; historial,
preferencias, OEM, GitHub y perfiles reales. Datos y hashes en `baseline-*/`,
`candidate-*/` y `comparison.json`.

| Hito desde inicio del proceso, s | Anterior: mediana [mín.–máx.] | Nuevo: mediana [mín.–máx.] |
| --- | ---: | ---: |
| Ventana | 1,322 [1,088–2,167] | 1,258 [1,099–1,356] |
| Modelo/CPU/RAM visibles y layout | 1,538 [1,250–2,449] | 1,456 [1,293–1,541] |
| Básicos con unidades visibles y layout | 1,646 [1,365–2,738] | 1,572 [1,434–1,672] |
| Primera muestra | 2,864 [2,596–4,077] | 2,792 [2,673–2,951] |
| Snapshot completo | 12,530 [12,250–13,748] | 12,480 [12,327–12,669] |
| Todo el trabajo de arranque terminado | 16,625 [15,676–17,358] | 16,336 [15,798–17,728] |

Todos los rangos se solapan: **sin aceleración demostrada**. La segunda toma
del control fue más lenta y permanece incluida. No mezclar este lote con el
1,18 s de la entrega anterior ni convertirlo en una garantía para otros equipos.
Se preservan seis tomas completas de diez muestras, con tiempos reales. GitHub
falló y había cero perfiles en las seis; no acredita red exitosa ni perfiles
activos. Visibilidad significa opacidad y layout, no presentación GPU.

No se midieron memoria/CPU en reposo, FPS o rendimiento del PC en este cierre.
La mejora es de validación, conservación y minimización de datos, no un ahorro
demostrado de recursos. Build .NET SDK 10.0.402 sin errores ni advertencias;
`build.log`. El portable final pasó **259 comprobaciones de interacción**
(`portable-interactions/interaction-checks.json`). No se modificó fuente después
de compilar; el siguiente commit solo contiene este informe.

- Código de entrega: `d748cb503db644152c00b9ab9584932f2ab6f327`.
- EXE artifacts: `artifacts/Silen-win-x64/Silen.exe`.
- EXE SHA256: `08821E0FA8B65845F03B5502ABAB0408F88ADB4FB2FE0F477AE27A750E46A446`.
- ZIP SHA256: `FB70BF3880AFD4C555DEBF21217EEA5A4F8E7C767CEEF151180B426EDB6AF69F`.
- Escritorio: `C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`.
- Anterior preservado en
  `C:\Users\avalo\Desktop\Silen - preversion anterior 20261017-seguridad-08821E0F`.
- Los **33 archivos** se compararon por hash antes de sustituir la carpeta.
  Manifiesto: `artifacts/revision-codigo-2026-10-17/delivery.json`.

Árbol limpio al cerrar; cuatro commits de correcciones reversibles por separado
y uno de documentación. No se presenta el resultado como una auditoría completa
de publicación ni una mejora de rendimiento del PC.
