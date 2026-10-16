# Verificación del motor principal: inicio, Chrome y GPU

15 de octubre de 2026. SDK .NET 10.0.402 del entorno de Codex.

La investigación y las fuentes se conservan en
[Guías y repositorios del motor](research/2026-10-15-motor-optimizacion-guias.md).
No se añadieron dependencias ni utilidades externas. Se amplió el motor nativo
existente y se conservaron los controles, navegación e identidad visual.

## Resultado funcional

- Optimizar PC ofrece entradas registradas en HKCU Run como elecciones manuales,
  con comando visible, permiso comprobado, respaldo previo y restauración exacta.
  Se excluyen entradas reconocibles de seguridad/anti-cheat/componentes Windows.
  El comando se vuelve a comprobar bajo el bloqueo del journal antes de aplicar;
  si cambió, no se elimina el nuevo comando.
- La presencia en Run se presenta como actividad sin verificar: Windows puede
  tener esa entrada deshabilitada en Configuración/Administrador de tareas.
  No se modifica StartupApproved ni se afirma consumo por su sola presencia.
- Chrome en segundo plano es una opción sin preseleccionar. Se comprueba App Paths
  y ejecutable local; se respeta una política HKLM existente y se guarda el valor
  HKCU original exacto, incluso ausencia o un tipo distinto. Puede afectar apps
  web/extensiones; no cierra ventanas o procesos en ejecución.
- La preferencia GPU por ejecutable detecta alto rendimiento ya presente. El
  optimizador no reescribe valores ya aplicados ni sus campos adicionales. Esto
  no garantiza qué GPU utiliza cada aplicación ni un aumento de FPS.

## Evidencia

1. Pruebas nuevas observadas en rojo antes de implementar: nueve casos iniciales,
   y regresiones adicionales de comando nulo, pérdida de permisos y presencia en
   Run con StartupApproved deshabilitado. La suite específica termina con 14 casos
   correctos, incluidos valores previos ausentes, DWORD y texto expandible.
2. Suite completa .NET 10: **530 pruebas correctas**, cero fallos, una prueba OEM
   en vivo optativa omitida. Resultado final: 105 Core, 368 Windows, 57 Gaming.
3. `scripts/build.ps1 -SkipTests`: cero advertencias y cero errores; portable
   autocontenido win-x64 publicado con 33 archivos.
4. `scripts/verify-portable.ps1`: ZIP extraído en una ruta nueva con espacios,
   **178 comprobaciones WPF correctas**, arranque sin SDK en PATH,
   `AppliedSystemChanges=false` y `UniversalOptimizationReady=true`.
5. Revisión independiente: respaldo, restauración, políticas Chrome, comando
   revisado y estado GPU sin otros hallazgos. Se resolvió la distinción entre
   presencia en Run y actividad efectiva con estado explícito y regresión.
6. Comprobación real de solo lectura: 20 opciones generales y una entrada de
   inicio ofrecida, ninguna preseleccionada. Chrome no disponible con permisos
   actuales, indicado antes de aplicar. Journal sin nuevas entradas; no se invocó
   Apply. Los cambios y Undo se verificaron con registro y servicios simulados.

Registros: `artifacts/optimizer-guides-{tests,build}.log`,
`optimizer-guides-{portable,desktop,live}.json`, `optimizer-guides-focused.log`.
La carpeta de evidencia del JSON portable contiene las capturas WPF de los seis
paneles y del resultado de inicio/Chrome con fixtures.

ZIP SHA256:
`6C0CEADF0AA4EA51C2C502790421022EF7D94B5D89F21EC0F20C7FAB0E81F384`.

Escritorio: `C:\Users\avalo\Desktop\Silen.lnk` apunta a
`C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`. Se cotejaron los 33 archivos
y se conservó la copia anterior. Ejecutable SHA256:
`CB15A90C6E41E5F460BEB0ED1769C28CF5B7AED3B85B0770AB70A06AA515BA2E`.
