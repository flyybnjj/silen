# Carga y operaciones de Bloom · 2026-10-17

## Resultado

Bloom presenta la espera inicial hasta recibir la identidad enriquecida (marca,
modelo y tipo), CPU, capacidad de RAM y primera muestra de sensores. Una temperatura
inaccesible dice «No disponible». Después se revela Resumen y continúa el diagnóstico;
Aplicar permanece bloqueado hasta obtener un resultado completo actual. No hay
retardos artificiales para alargar la animación. Fallo y cancelación retiran la carga.

La misma malla existente de Bloom se utiliza en carga, operaciones y ambiente. El
aleteo informativo tiene ciclo de 3,6 s y oscilación vertical máxima de 8 unidades.
Se conserva la pausa por ventana oculta, minimizada o sin foco, y la pose estática
con movimiento reducido. No se añade otra malla ni una llamada a IA para los textos.

El optimizador manual experto y Optimizar PC abren diálogos dentro de Silen con
selección independiente y un único Aplicar fijo. El fondo queda inactivo. La
preparación no aplica cambios; se conservan confirmaciones, recuperación y journal.
Cancelar está dentro del ciclo de teclado; Escape solicita cancelar durante el
trabajo. Cerrar espera a que termine la operación en curso.

La limpieza publica recuentos reales de revisados, retirados y pendientes. El
cliente coalesce actualizaciones antes de enviarlas al despachador, descarta callbacks
cerrados y usa «Temporales de tu usuario» en lugar de rutas personales. La cuarentena
no se presenta como espacio liberado: se informa recuperación y liberación por
separado. Un observador defectuoso no puede interrumpir el retiro después de mover
un archivo. Cancelación conserva el resultado parcial y los respaldos existentes.

Bloom informa resultados de optimización cancelados, fallidos o con verificación
incierta, incluso si el servicio los retorna sin excepción. Una verificación
incierta no se cuenta como cambio completado.

Versión, búsqueda de actualizaciones, descarga y error de Silen solo aparecen en
Ajustes, también en modo normal. La búsqueda de fondo conserva su comportamiento.
El pie técnico repetido se retira. La elección explícita de reinicio del PC se conserva.

Referencia de interacción consultada: [AME Wizard, Running a Playbook](https://docs.amelabs.net/using-wizard/running-playbook.html),
selección antes de ejecutar. No se descargan ni ejecutan herramientas externas.

## Verificación

Base: 685 pruebas unitarias correctas, una OEM opcional omitida.
Resultado: **692 correctas**, una OEM opcional omitida; **267 comprobaciones WPF**
con servicios simulados. Compilación .NET 10: cero errores y cero advertencias.
La regresión roja inicial confirmó el panel de actualización indebidamente global.
Siete pruebas nuevas de progreso fallaron antes de implementar el motor y pasan después.

Evidencias locales en `artifacts/bloom-flujos-2026-10-17/`:

- `tests-before.log`, `cleanup-red.log`, `cleanup-green.log`, `tests-after.log`.
- `ui-red/status.json`, `ui-build.log`, `ui-confirmacion/interaction-checks.json`.
- `ui-confirmacion/interaction-fixtures/bloom-carga-{Claro,Oscuro}-{1000,1240}.png`.
- `ui-confirmacion/interaction-fixtures/bloom-{manual,pc}-{Claro,Oscuro}-{1000,1240}.png`.
- `ui-confirmacion/interaction-fixtures/bloom-limpieza-simulada-{Claro,Oscuro}-{1000,1240}.png`:
  ilustran el estado de presentación con recuentos simulados; no acreditan una limpieza real.

Las capturas son del área cliente, con datos de prueba. Se verificaron ambos tamaños,
1240×820 y 1000×680, en ambos temas. Revisión visual en un lote inicial y una
confirmación tras retirar la burbuja que tapaba opciones. La regla de entrada inmediata
de 0889f1e queda sustituida por la espera elegida por el usuario; no comparar sus
milestones de visibilidad como si fueran el mismo flujo.

Una revisión de código independiente detectó y confirmó corregidos la cancelación
por teclado y el resultado genérico de optimizaciones canceladas. No encontró bypass
de confirmaciones ni rutas nuevas expuestas en el progreso.

## Checklist

- [x] Carga, primera muestra y temperaturas ausentes; aplicar bloqueado hasta completar.
- [x] Fallo/cancelación de inicio sin bloqueo permanente y reintento disponible.
- [x] Opciones modales excluyentes, un Aplicar y cierre sin aplicar.
- [x] Cancelar dentro del ciclo de Tab; Escape cancela durante la operación (estructura y regresión).
- [x] Conteos recuperables, progreso coalescido y rechazo de callbacks ya cerrados.
- [x] Cancelación sin excepción, fallo e incertidumbre reflejados en Bloom.
- [x] Versión y errores de actualización solamente en Ajustes.
- [x] Claro/oscuro y ambos tamaños mediante capturas y aserciones de layout.
- [ ] Recorrido completo manual de teclado, hover/foco y lector de pantalla.
- [ ] Alto contraste, DPI, reducir movimiento y fluidez real del aleteo: validación manual pendiente.
- [ ] Aplicación real sobre Windows, punto de restauración real y reinicio: no ejecutados.

No se atribuye mejora de rendimiento del PC a estos cambios de presentación.


## Portable entregado y arranque real

Fuente del ejecutable: `60020a7fc401604889d7a81823636b6a0e6aa927`.
Portable: `artifacts/Silen-win-x64/` y ZIP del mismo nombre. Copia del escritorio:
`C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`; el acceso `Silen.lnk`
conserva ese destino. Se verificaron SHA-256 de los **33 archivos**. La carpeta
anterior se conserva en `Silen - preversion anterior 20261017-Bloom-60020a7`.

- EXE SHA-256: `9A8F5267C091B7D5500AE195C0F416A860D69E6A07367D7154BB6CDC990B4C1D`.
- ZIP SHA-256: `9E016C2E6218665E600E1F8E81AE57EFCBDE2C34A47861FA2C0B1D2D2CED8B40`.
- El ejecutable portable publicado pasa de nuevo las **267 comprobaciones WPF**;
  evidencia en `portable-interaction/interaction-checks.json`.
- Distribución local sin firma de editor; publicación firmada y ayudante en carpeta
  protegida continúan como trabajo separado.

Tres arranques normales del ejecutable del escritorio, sin sonda que omita tareas
de producción, mediante `scripts/measure-startup.ps1`. Tiempos desde el inicio del
proceso; mediana y rango en este equipo:

| Hito | Mediana (s) | Rango (s) |
|---|---:|---:|
| Ventana creada | 1.026 | 0.987–1.053 |
| Identidad y RAM disponibles internamente | 1.168 | 1.136–1.221 |
| Primera muestra | 2.475 | 2.445–2.536 |
| Resumen listo para mostrarse tras retirar la carga | 2.476 | 2.446–2.537 |
| Diagnóstico completo | 12.084 | 12.082–12.139 |
| Todas las tareas iniciales, incluidas consultas externas | 15.545 | 14.911–31.919 |

La visibilidad instrumentada exige `Visibility.Visible`, opacidad ≥0,98 y layout;
no acredita el instante exacto de presentación GPU ni la fluidez del aleteo.
Las consultas externas explican la variación de inicialización global; no se espera
a ellas para retirar la carga de Bloom. No se comparan como mejora con 0889f1e:
ese flujo revelaba la vista previa antes de esperar una muestra y el criterio cambió
por petición del usuario. No se afirma mejora de CPU/RAM ni del rendimiento del PC.
