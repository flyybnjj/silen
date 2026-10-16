# Windows Update, Gaming universal y limpieza directa

**Objetivo:** implementar el paquete solicitado sobre la preversión actual.
Se mantienen tipografía, mascota, animación, colores y compatibilidad de datos.

- [x] Diagnóstico común de Windows Update: pausas locales UX, políticas y estado
  real de wuauserv. DriverUpdateService lo consulta antes de COM; WindowsCollector
  incorpora el estado al informe. Lectura desconocida no equivale a sano.
- [x] Acción explícita de reanudación local/servicio con journal y permisos,
  validación de destinos y estado posterior. No borrar políticas de organización.
  IAutomaticUpdates.Resume está obsoleto y no hace nada desde Windows 10: no usarlo.
- [x] Gaming general: Modo Juego, capturas, control de tráfico en segundo plano,
  energía y preferencia GPU por ejecutable elegido. Nada de promesas de ping/FPS.
- [x] Fortnite solo como perfil plegable instalado, con selección exclusivamente
  del juego. Eliminar el optimizador duplicado y el atajo a restauración de Gaming.
- [x] Temporales: un único botón con tamaño calculado, limpieza directa de los
  temporales elegibles (>=24 h), omisiones y cancelación contabilizadas. Revalidar
  archivos/identidad justo antes de borrar; no ampliar a documentos/descargas/shaders.
- [x] Pruebas primero para estados, permisos, respaldo/Undo, selección de juego,
  limpieza directa y UI; suite .NET10 completa, publicación, portable extraído y
  actualización verificada de la preversión del escritorio.

No se ejecutan reparaciones, cambios de GPU/red ni limpieza en el PC real durante
las pruebas: las mutaciones se verifican con plataformas y archivos controlados.

Resultado: 479 pruebas correctas y una opcional omitida; compilación sin errores
ni advertencias; portable extraído con 164 comprobaciones de interacción y 30
archivos verificados. Preversión del escritorio actualizada con respaldo de la
anterior. Detalles y límite de validación de pausa real en
`docs/windows-update-gaming-limpieza.md`.
