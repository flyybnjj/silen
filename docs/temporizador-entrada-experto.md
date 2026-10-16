# Temporizador, entrada y controles expertos

## Uso

En Resumen, abre **Elegir juego o aplicación**, elige el ejecutable y prepara
**Optimizar PC**. GPU, temporizador y entrada aparecen como opciones revisables;
temporizador y entrada no se marcan automáticamente. Solo se aplica lo confirmado.

El perfil de temporizador se guarda por ruta completa del ejecutable. Silen
comprueba el proceso cada dos segundos y solicita el mínimo que admite Windows.
Si se pierde la solicitud, la repone. Al terminar el producto, desactivar el
perfil o cerrar Silen, libera su propia solicitud. No cancela las de otras apps.
La solicitud propia se establece incluso si otro programa ya informa el mínimo.
El estado distingue el intervalo solicitado de la lectura posterior de Windows.

Silen debe permanecer abierto. No instala un servicio, tarea ni controlador;
no inyecta código, modifica el juego o cambia HPET, BCD ni políticas globales
del temporizador. Un proceso inaccesible no se da por confirmado. Solo una
instancia controla los perfiles de un usuario. Un archivo dañado se conserva
sin activar sus perfiles. La cancelación de inicio libera el control para reintentar.

Es una opción experimental: Windows 11 no garantiza que una solicitud desde
Silen altere la temporización de otro proceso. Mayor frecuencia puede aumentar
consumo y trabajo de CPU. No demuestra una mejora de FPS o latencia de entrada.

## Entrada de Windows

- **FilterKeys:** habilita el filtro con aceptación 0 ms, demora 130 ms,
  repetición 10 ms y rebote 0 ms. Desactiva el atajo y los sonidos por pulsación,
  conservando los otros indicadores. Cambia aceptación y repetición del teclado;
  no modifica el ratón ni la frecuencia del dispositivo.
- **Ratón sin aceleración:** deja sus dos umbrales y aceleración en cero.
  Conserva sensibilidad y DPI. Los juegos con Raw Input pueden ignorarlo.

Ambas opciones son globales y permanecen hasta restaurarlas, aunque se ofrezcan
al elegir un producto. No hay reactivación automática de estos dos ajustes.
Se consultan y escriben mediante SystemParametersInfo, con persistencia y aviso
a Windows. OptimizationJournal guarda todos los campos originales antes de
escribir y comprueba después el resultado. Undo restaura el estado exacto si no
hay un cambio externo incompatible. Si ya estaban iguales, no se crea un respaldo
ficticio. No se cambian controladores, USB ni prioridades de interrupciones.

## Ajustes y modo experto

Pulsa el logo **silen** para abrir Ajustes. **Modo experto** guarda una preferencia
local y muestra **Optimizador manual**. Este botón abre los controles individuales
de Windows, energía e inicio. Cada control prepara cambios pendientes; solo **Aplicar selección manual** confirma y ejecuta ese lote, independiente del optimizador de Resumen. Activar el modo no aplica ajustes. Limpieza,
restauración y desactivación de perfiles siguen disponibles en modo normal.

Al finalizar un lote aplicado aparece la elección de reiniciar ahora o al cerrar Silen. Puede cancelarse; al cerrar se confirma de nuevo y no se fuerza el cierre de otros programas. Cada opción distingue efectos inmediatos, al reabrir la aplicación o al reiniciar Windows. Un resultado parcial se muestra como tal, sin prometer su beneficio.

Preferencias: `user-preferences.json`; perfiles: `application-timers/profiles.json`,
en la ubicación local de Silen indicada en Historial. La ubicación heredada de
FlyyOptimizer se conserva cuando existe. Los archivos permanecen fuera del portable.

Al abrir normalmente, Silen diagnostica el equipo una vez y muestra los recursos
antes del optimizador. Si falla o se cancela sin datos, ofrece reintentar. Se
retiraron los botones repetidos de análisis. Los modos de prueba usan servicios
simulados o consultas sin aplicar perfiles guardados.

## Fuentes y alcance

- [Microsoft: timeBeginPeriod](https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod): alcance por proceso, comportamiento de Windows 11 y coste energético.
- [NtDoc: NtQueryTimerResolution](https://ntdoc.m417z.com/ntquerytimerresolution) y [NtSetTimerResolution](https://ntdoc.m417z.com/ntsettimerresolution): contratos nativos de los headers phnt de System Informer, unidades de 100 ns y solicitud por proceso. Son API nativas; su efecto se presenta como experimental.
- [Microsoft: FILTERKEYS](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-filterkeys): campos y banderas del filtro.
- [Microsoft: SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfoa): lectura, escritura y notificación de preferencias de entrada.

La consulta de solo lectura en el equipo de desarrollo detectó un mínimo de
0,5 ms y un intervalo actual de 10 ms. Los dos ajustes de entrada ya coincidían
con los valores propuestos. Esta comprobación no invocó NtSetTimerResolution
ni SPI de escritura; no demuestra una solicitud de 0,5 ms en un juego real.
Las pruebas de aplicación, liberación y restauración usan plataformas simuladas.
