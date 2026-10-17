# Información del equipo al abrir · 2026-10-17

El usuario pidió ver información actual al entrar, sin esperar al diagnóstico
completo. Se mejora la obtención y presentación de datos; no se añaden ajustes
de Windows ni se afirma que el PC o sus juegos rindan más.

## Causa y cambio

- El inicio cargaba todos los informes antes de detectar el equipo. Ahora lee
  preferencias e inicia la detección; carga historial al guardar el resultado o
  después del fallo, conservando el error y el reintento.
- La identidad esperaba varias consultas WMI. Ahora publica antes de WMI y de
  inspeccionar servicios una previa local: fabricante/modelo y CPU desde claves
  fijas de descripción del hardware; capacidad de RAM desde GlobalMemoryStatusEx.
  Sin elevación, red, serie ni datos persistidos de una sesión anterior.
- El contenido se desvanecía entre 0,4 y 0,95 s; la introducción completa de la
  ilustración dura 1,8 s. Ahora contenido e interacción están disponibles desde
  el primer fotograma; el velo de legibilidad parte de 0,75. Geometría, poses,
  desenfoque, transformaciones, cadencia y política de pausa de Bloom conservados.
- El nuevo orden expuso una invalidación anticipada de UI Automation al cambiar
  de vista. PageHost reinicia la caché después de que TabControl actualice su
  ContentPresenter, en prioridad Loaded. La regresión comprueba nombre y controles.

Código: `BasicMachineReader.cs`, `WindowsCollector.Collect`,
`MainViewModel.InitializeAsync`, `RibbonAnimator.ApplyFrame` y
`PageHost.OnSelectionChanged`. Revisión de entrega: **0889f1e**.

## Seguridad de los datos parciales

No se inventan porcentajes: CPU, uso de memoria, GPU y temperaturas esperan sus
muestras. GPU sin inventariar: «Midiendo gráficos…»; capacidad desconocida:
«Midiendo capacidad…». La previa comienza en 0/14 pasos y no se guarda como
informe terminado. Optimizar PC y Aplicar principal/manual siguen bloqueados
hasta completar el diagnóstico actual. Cancelar o fallar retira previa y trazas
parciales. Diez muestras y sus fechas reales conservadas. No se cambian ayudante,
journal, respaldos, confirmaciones ni configuración del PC.

## Medición del EXE normal

Tres repeticiones alternadas control/candidato, mismo PC, caché caliente,
publicación autocontenida win-x64. Sin builds, tests ni conexión de automatización
de navegador durante las tomas. Control: ca8c663 anterior, preservado en `control/`;
candidato: 0889f1e. Sonda `scripts/measure-startup.ps1`, entrada normal sin
argumentos de prueba, variable local a cada proceso. Historial y preferencias,
OEM, herramientas, GitHub y perfiles reales.

| Hito desde inicio del proceso, s | Anterior: mediana [mín.–máx.] | Nuevo: mediana [mín.–máx.] |
| --- | ---: | ---: |
| Ventana mostrada | 0,996 [0,992–1,038] | 1,026 [0,988–1,056] |
| Capacidad RAM disponible | 1,412 [1,396–1,487] | **1,180 [1,147–1,210]** |
| Modelo/CPU/RAM visibles y layout | No instrumentado por separado | **1,184 [1,151–1,212]** |
| Básicos con unidades visibles y layout | 1,920 [1,919–1,968] | **1,285 [1,259–1,314]** |
| Primera muestra | 2,621 [2,608–2,702] | 2,497 [2,478–2,522] |
| Diez muestras | 12,290 [12,274–12,381] | 12,182 [12,123–12,215] |
| Snapshot completo | 12,316 [12,300–12,402] | 12,221 [12,160–12,259] |
| Todo el trabajo de arranque terminado | 16,518 [16,366–17,087] | 16,464 [16,445–17,197] |

El hito común de básicos con unidades baja **0,635 s (33,1 %)**, con rangos
separados. Modelo/CPU/RAM aparecen unos 0,16 s después de mostrarse la ventana.
El proceso necesita iniciar runtime y ventana: no es instantáneo ni una garantía
para otros equipos. El arranque completo tiene rangos solapados: **sin mejora
demostrada**. No se midieron CPU/RAM en reposo o FPS en esta tarea.

La sonda acredita opacidad ≥0,98 más layout, no presentación GPU. Se corrigió
Identity, que podía marcarse solo por IsScanning, antes de recibir el modelo.
No se compara ese antiguo hito. FirstInformationVisibleLayout exige identidad,
RAM y contenido visible. BasicsVisibleLayout mantiene el criterio común con
unidades. No extrapolar su comparación al coste puro del colector.

Seis tomas completas con diez muestras cada una, GitHub fallido y cero perfiles
en todas. Sus tiempos están incluidos; no acredita red exitosa ni perfiles
activos. PreferencesAndHistory contiene solo preferencias en el inicio automático;
el historial pasó detrás del diagnóstico. No comparar esa fase como idéntica.

Datos crudos y hashes: `artifacts/inicio-inmediato-2026-10-17/`,
`baseline-{1,2,3}/`, `candidate-{1,2,3}/` y `comparison.json`.

## Verificación y entrega

- [x] Baseline: 670 unitarias correctas y una OEM opcional omitida.
- [x] Después: **673 unitarias correctas**, misma omisión, cero fallos.
- [x] Build .NET 10 sin errores ni advertencias nuevas.
- [x] Regresiones fallan antes: primera consulta sin previa, historial delante
  de detección y contenido oculto en el primer fotograma.
- [x] **259 comprobaciones WPF** en fuentes y en el portable final: previa sin
  GPU/porcentajes inventados, bloqueo de Aplicar, cancelación, historial posterior
  y árbol accesible actualizado al navegar.
- [x] 32 poses de contraste; mínimo **4,8004:1**, superior a 4,5:1.
- [x] Capturas de previa en claro/oscuro a 1240×820, datos simulados rotulados.
- [x] Tres arranques por binario, diez muestras en cada toma.
- [x] 33 comprobaciones de ciclo de vida, cero omisiones. La sonda antigua
  falló porque exigía contenido aún oculto durante la introducción. Se conservó
  en `lifecycle/results.json` y se actualizó una copia aislada para exigir
  contenido visible e interactivo; resultado válido en
  `lifecycle-updated/bin/Release/net10.0-windows/results.json`. Sin cambios a
  producción para satisfacer una expectativa anterior.
- [x] Portable de artifacts y escritorio con 33 archivos iguales por SHA256;
  ejecutable 0889f1e, anterior conservado íntegro. Sin publicación externa.
- [ ] Lector de pantalla y recorrido humano completo de teclado/hover en ambos temas.
- [ ] Alto contraste, reducir movimiento y DPI reales de Windows.

Los fallos se conservan: `red-native.log`, `red-ui/status.json`,
`red-frame/status.json`, `green-ui/status.json`, `ui-diagnostic/status.json`,
`green-ui-2/status.json`. Los últimos mostraron caché accesible antigua/vacía al
invalidar demasiado pronto; la invalidación diferida pasa la batería. Salidas
válidas: `interactions-final/` y `portable-interactions/`.

Destino: `C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`.
Respaldo: `C:\Users\avalo\Desktop\Silen - preversion anterior 20261017-inicio-52313959`.
EXE SHA256: `52313959EAEE4C21F40503D3EAF9C1DD708C0E2F6ED5D8CB62E5F1C3CF5231CE`.
ZIP SHA256: `0743A1DBF58776912D6FA8E660A7512F5A67608E023EDB72F5A795E04D4DA9C0`.
Manifiesto: `delivery.json` en el mismo directorio de evidencia. Los commits
posteriores a 0889f1e solo actualizan documentación y no cambian el binario.
