# Inicio automático, temporizador, entrada y menú experto

Solicitudes del usuario: detectar el equipo al abrir sin botones Analizar;
temporizador mínimo automático para la app/juego elegido; FilterKeys y controles
de entrada; Ajustes desde el logo con optimizador manual solo en modo experto.

Diseño: conservar identidad y los seis índices de vista. El logo abre Ajustes;
el acceso lateral redundante se oculta sin desplazar Historial/Bloom. Preferencia
Modo experto desactivada inicialmente y persistente. Su botón Optimizador manual
revela los controles individuales existentes. Limpieza, restauración y perfiles
de temporizador permanecen accesibles en Ajustes para todos.

- [x] Inicio: `InitializeAsync(autoScan: true)` en ejecución normal, una detección
  por sesión con progreso y cancelación. Retirar botones Scan de cabecera,
  bienvenida y Drivers. Mostrar recursos antes del optimizador; reintento solo
  cuando falta diagnóstico. Tests de una única ejecución y ausencia de botones.
- [x] Entrada nativa: SPI_GET/SETFILTERKEYS con perfil de aceptación 0 ms,
  repetición 130/10 ms, sin BounceKeys, atajo accidental ni sonidos por pulsación.
  Preservar los demás indicadores.
  SPI_GET/SETMOUSE para aceleración y umbrales cero, sin cambiar sensibilidad/DPI.
  Opciones manuales sin preseleccionar; son globales de Windows hasta restaurar,
  aunque se ofrezcan al elegir un producto. Añadir tipos estrictos al journal,
  respaldo de todos los campos antes de SPI, lectura posterior y Undo exacto.
- [x] Temporizador por aplicación: servicio nativo con NtQueryTimerResolution y
  NtSetTimerResolution, mínimo consultado y estado real (no fijar 0,5 ms). Perfil
  local persistente por ruta .exe confirmada. Comprobar proceso por ruta completa
  cada dos segundos, reponer solicitud propia si se pierde, liberarla al terminar
  el juego, desactivar perfil o cerrar Silen. No liberar solicitudes ajenas.
  Una instancia controladora por usuario; no servicios, tareas, drivers, inyección,
  cambios de HPET, BCD ni GlobalTimerResolutionRequests. Silen debe estar abierto.
  Windows 11 no garantiza efecto en otros procesos; etiquetar experimental.
- [x] UI: opciones por producto en la selección principal y botón para desactivar
  su temporizador. Ajustes muestra perfiles guardados (incluidos productos
  desinstalados) para quitarlos. Guardar Modo experto como preferencia local;
  ocultar controles de mutación manual fuera del modo experto. No ocultar Undo.
- [x] Verificación: tests con SPI/procesos/timer simulados, API de consulta real
  solo lectura, suite .NET10, build limpio, portable e interacciones WPF.
  Actualizar escritorio con copia anterior preservada.

Riesgos a probar: timer ya mínimo por otra app no implica solicitud propia;
desconexión/reinicio del proceso; ruta igual solo por nombre; proceso inaccesible;
solicitud perdida; desactivar perfil de un exe eliminado; cierre/cancelación;
perfil corrupto y múltiples instancias; Undo que preserva cambios externos de
entrada; configuraciones ya iguales sin respaldo ficticio; reintento de escaneo.

Fuentes: documentación oficial Microsoft de timeBeginPeriod, FILTERKEYS y
SystemParametersInfo; contratos NtQuery/NtSetTimerResolution documentados por
NtDoc a partir de los headers phnt de System Informer. No se copian utilidades.
Valores hallados en lectura del PC: FilterKeys 0/130/10/0 ms y ratón 0/0/0.
FilterKeys cambia aceptación/repetición de teclado, no polling USB ni ratón.
