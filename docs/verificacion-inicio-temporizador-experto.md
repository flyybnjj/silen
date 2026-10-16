# Verificación: inicio, temporizador, entrada y modo experto

15 de octubre de 2026. SDK .NET 10.0.402 de Codex.

## Entrega

- El diagnóstico normal comienza al abrir, una vez por sesión. Resumen presenta
  los recursos antes del optimizador. Se retiraron los botones de análisis de
  las seis vistas; un diagnóstico inicial fallido permite reintentar.
- El logo silen abre Ajustes. Modo experto persiste como preferencia de la app
  y muestra Optimizador manual. Activarlo no modifica Windows. Los controles
  individuales están ocultos en modo normal; limpieza, perfiles y Undo permanecen.
- La aplicación elegida puede guardar un perfil experimental de temporizador
  mínimo. Se detecta su proceso por ruta completa, se mantiene una solicitud
  propia y se libera al terminar, desactivar el perfil o cerrar Silen.
- FilterKeys y ratón sin aceleración son opciones globales sin preseleccionar.
  SPI guarda sus campos originales antes de escribir y Undo restaura exactamente
  ese estado, conservando cambios externos incompatibles.

## Evidencia

1. Suite completa: **546 pruebas correctas**, cero fallos y una prueba OEM en vivo
   optativa omitida: 105 Core, 384 Windows y 57 Gaming.
2. Regresiones nuevas verificadas antes y después de corregir: cancelación del
   inicio que retenía el controlador; perfiles corruptos; sonidos por pulsación
   de FilterKeys; actualización del temporizador que sobrescribía el estado DNS.
3. Build Release y publicación autocontenida: **cero advertencias y cero errores**.
4. `--interaction-test`: **186 comprobaciones WPF**. Incluye arranque automático
   único, ausencia de botones Scan, logo/Ajustes, modo experto sin mutaciones,
   restauración en modo normal y conservación del resultado DNS.
5. `scripts/verify-portable.ps1`: **33 archivos cotejados** con el ZIP; extracción
   nueva en ruta con espacios, arranque sin SDK en PATH y 186 comprobaciones WPF.
   Las seis vistas se renderizaron a tamaño normal y mínimo. El colector real
   reunió diez muestras y 232 drivers; `AppliedSystemChanges=false`,
   `SerialLookupRequested=false` y `UniversalOptimizationReady=true`.
6. Consulta real de solo lectura: mínimo admitido 0,5 ms, intervalo actual 10 ms;
   ambos ajustes de entrada ya coincidían. No se invocó NtSetTimerResolution ni
   SPI de escritura. Aplicación, liberación, errores y Undo se probaron con
   plataformas simuladas. No se midió una mejora de latencia/FPS en un juego.
7. Revisión independiente: se corrigió la liberación del controlador al cancelar
   el inicio. Sin hallazgos pendientes en temporizador, SPI/journal/Undo ni acceso
   experto revisados. No se añadieron ejecutables o dependencias externas.

Registros locales: `artifacts/timer-input-expert-tests.log`,
`timer-input-expert-app-build.log`, `timer-input-expert-build.log`,
`timer-input-expert-portable.json`, `timer-input-expert-desktop.json` y
`timer-input-readonly.json`.

Evidencia portable:
`artifacts/portable verification ad2e4cc515a4403bada34d6e87b7165d/smoke`.

ZIP SHA256:
`BD5C518E1AEC7346E77428AC1C34CFD2A2A6C1ACF372B6E25B9348CA2E0DED1A`.

Ejecutable SHA256:
`B1ACFBD7EDE1FF9B7FA592C67A21A23174112269F7CE510303AAE624DB052B30`.

El acceso `C:\Users\avalo\Desktop\Silen.lnk` apunta a
`C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`. Se cotejaron los 33 archivos
y se conservó la versión anterior en `Silen - preversion anterior 48975f79`.

## Límites

Silen debe permanecer abierto para mantener el temporizador. Windows 11 no
garantiza que su solicitud afecte a otro proceso. Entrada cambia preferencias
globales hasta restaurar; no ajusta polling USB ni controladores. Consulta
[alcance y fuentes](temporizador-entrada-experto.md).
