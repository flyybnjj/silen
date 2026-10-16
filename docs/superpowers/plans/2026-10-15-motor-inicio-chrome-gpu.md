# Motor principal: inicio, Chrome y estado GPU

Objetivo: implementar mejoras funcionales a partir de la investigación en
`docs/research/2026-10-15-motor-optimizacion-guias.md`. Conservar apariencia,
navegación, respaldos históricos y la única acción principal de optimización.
Ejecución local en esta sesión; no importar scripts ni añadir dependencias.

- [x] Inicio: probar que las entradas registradas se ofrecen individualmente sin
  preselección; las entradas de seguridad reconocidas se excluyen. Aplicar solo
  una entrada revisada conserva tipo/texto en el journal y Undo la restaura.
  Cambio externo entre revisión y ejecución debe omitirse sin tocar el nuevo valor.
  La actividad efectiva no se infiere de Run; se indica como sin verificar incluso
  si Windows conserva una entrada que tiene deshabilitada en StartupApproved.
  Añadir `DisableStartupCheckedAsync(id, expectedCommand, token)` al contrato y
  servicio nativo; usarlo en la ejecución principal con el comando revisado.
- [x] Chrome: pruebas con registro simulado y archivo local de fixture. Ausente
  o sin permisos queda no disponible; presente aplica DWORD 0 y Undo recupera
  valor anterior exacto/ausencia. Añadir grupo `chrome-background` y comprobar
  registro App Paths antes de aplicar. No preseleccionar.
- [x] GPU: pruebas del estado inicial, estado ya alto rendimiento, lectura
  desconocida y valores adicionales preservados. Añadir consulta al contrato
  nativo y usarla al preparar la selección del PC.
- [x] Verificación: tests .NET 10 completos, compilación sin advertencias,
  prueba del portable autocontenido e interacciones WPF. Probar controles nuevos
  con fixtures, sin aplicar cambios reales al equipo. Actualizar artifacts y
  copia del escritorio conservando la anterior.
- [x] Documentación: explicar efectos, próximo inicio de sesión, limitaciones
  de detección de inicio y restauración. Guardar evidencia y hashes finales.

Riesgos cubiertos: selección antigua, permisos perdidos, producto desinstalado,
políticas previas con tipo inesperado, restauración después de desinstalar y
entrada de inicio que cambió antes de escribirse. No se ejecuta ningún comando
de las entradas ni los navegadores detectados.
