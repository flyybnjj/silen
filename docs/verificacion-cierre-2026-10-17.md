# Verificación y entrega de pendientes · 2026-10-17

Alcance elegido por el usuario: diagnóstico, verificaciones y portable. Ayudante
protegido y campaña de fallos del actualizador quedan como tareas de publicación.

## Accesibilidad y foco

Al probar con teclas reales en Windows, el botón «Optimizar PC» dibujaba foco,
pero UI Automation solo veía el contenedor TabControl sin sus controles. La
plantilla no renderiza cabeceras: el peer de pestañas esperaba contenedores que
no existen en su árbol visual. La regresión `accessibility-red/status.json`
reproduce la ausencia del botón en el árbol.

PageHost conserva TabControl y la composición, pero expone el contenido como
panel accesible, nombrado según la vista. La navegación lateral mantiene la
selección; no se añaden pestañas visuales. El peer se invalida al cambiar de vista.
Los contenedores de Resumen no consumen Tab sin ofrecer una acción.

La verificación detectó además un problema real con la ventana activa: al abrir
el manual, enfocar su ItemsControl podía desplazar el encabezado hasta Y=-92,8.
Ahora se enfoca la primera acción descendiente y después se conserva el
encabezado mediante BringIntoView. Sin cambios en las acciones o confirmaciones.

La batería WPF posterior (`interactions-verified/interaction-checks.json`) pasó
**255 comprobaciones**, incluyendo el árbol de Resumen, su actualización en RAM
y red, la navegación al manual y la batería anterior. Se repite sobre el portable
de entrega antes de actualizar el escritorio.

## Contraste sobre BLOOM

32 poses deterministas de la animación existente, dos temas y dos tamaños;
metadatos de CPU, RAM instalada y GPU. Mínimo claro **4,8004:1**, oscuro
**7,7255:1**. Ahora se exige ≥4,5:1 en una regresión, además de guardar la evidencia.
No se modifica la ilustración ni se añade el fondo neutro propuesto, porque las
poses ensayadas no lo requieren. No equivalen a todos los fotogramas posibles,
a un ensayo de GPU o a un recorrido con lector de pantalla.

## Sonda del EXE normal

`scripts/measure-startup.ps1` abre el portable sin argumentos por la ruta real
Program.Main/App.OnStartup. Solo en ese proceso define
`SILEN_STARTUP_DIAGNOSTICS_DIRECTORY`; no cambia las variables del usuario.
Con la variable ausente no se observa ni escribe un informe adicional. No se
activa en el ayudante, actualizador ni modos de pruebas con argumentos.

Incluye InitializeAsync, historial y preferencias reales, colector, OEM, GitHub,
inspección de herramientas/Windows Update y carga real de perfiles. Al terminar
el modo de medición guarda tiempos, contadores y fechas de muestras, retira sus
observadores y cierra su propia instancia. No serializa snapshot, claves, rutas
del usuario ni mensajes de error de red. Un fichero existente no se sobrescribe.
La fixture usa directorios únicos y ejecuta dos mediciones para comprobarlo.

Regresión observada antes de implementar: `startup-red/status.json`. Después,
`interactions-source-final/interaction-checks.json`: **256 comprobaciones**.
La batería unitaria mantiene **670 correctas y una OEM opcional omitida**;
compilación de solución sin errores ni advertencias. La revisión independiente
señaló la repetición de la fixture, corregida con subdirectorios únicos.

Un intento de repetir toda la batería antigua en una carpeta ya poblada falló
por su historial persistido de acciones externas simuladas; no se contó como
pasada. La prueba focalizada del nuevo diagnóstico sí repite dos veces su carpeta
padre. Las baterías completas usan una salida nueva, como el protocolo existente.
También se conservó el fallo de compilación por dos builds concurrentes que
compartían temporales WPF; repetido secuencialmente, el build pasó.

## Ciclo de vida de BLOOM

Sonda existente recompilada contra las fuentes actuales: **33 comprobaciones,
cero omisiones**. Inicio sin foco legible, animación activa, pausa al trabajar,
minimizar, ocultar y desactivar durante la introducción; reanudación y cierre con
reloj/cuadros detenidos. Evidencia en `lifecycle-probe/bin/Release/net10.0-windows/results.json`.
No se inicializaron acciones del sistema ni se cambiaron preferencias de Windows.
En este equipo animaciones estaban activadas, alto contraste desactivado y tier
de renderizado 2. Reducir movimiento y alto contraste reales no se ejercitaron.

## Arranque real y medición final

Tres arranques normales del EXE de entrega: básicos visibles a **1,944
[1,907–1,966] s**; trabajo completo a **14,872 [14,729–15,847] s**. Incluye
historial real, OEM, herramientas, GitHub y carga de perfiles. GitHub falló y
había cero perfiles en las tres tomas; no acredita éxito de red ni perfiles
activos. El host abreviado termina a 11,971 s y no sustituye esta medición.

El análisis contra f377ab0 bajó de **14,489 [14,473–14,780] a 12,174
[12,103–12,197] s** en el lote final pareado. Una subida de CPU durante la
sesión de automatización motivó otra comparación, sin cambiar el binario,
tras cerrar dicha conexión: 15,115 [14,854–15,194] a 12,328 [12,127–12,348] s;
CPU 0,495 [0,443–0,833] frente a 0,443 [0,417–0,547] %. Se conservan ambos
lotes separados. Dos tomas sin foco fueron excluidas y conservadas. No se
demuestra mejora de CPU/RAM ni se atribuye el ruido a una causa nativa probada.
Protocolo, fases y datos: `docs/rendimiento-pr2-2026-10-17.md`.

## Teclado real, portable y respaldo

Automatización de teclas reales en Resumen claro: Tab llega a detalles y acción
principal, foco visible; Space abre selección de producto y su búsqueda está
accesible. Evidencia: `keyboard-accessibility.txt` y
`keyboard-product-selector.jpg`. Es un recorrido parcial automatizado, no
evaluación humana completa ni ensayo con lector de pantalla o tema oscuro.

Build final con .NET SDK 10.0.402: cero errores y cero advertencias. El EXE
autocontenido pasó **256 comprobaciones de interacción** en salida nueva
(`delivery-interactions/interaction-checks.json`). No se alteró código después
de compilar; los commits posteriores solo documentan.

- Revisión de código embebida: `ca8c663addb723f23662f1d165b2399bc7444c0d`.
- EXE: `artifacts/Silen-win-x64/Silen.exe`.
- SHA256 EXE: `6EBD770299F74C5B164FDA47E7A75A68F900EDEA1C232FE88540070D17D61808`.
- ZIP: `artifacts/Silen-win-x64.zip`.
- SHA256 ZIP: `7D2E21AF3FBE664222A396AFB9EE8D5CE4D5A54D9AB496A4093EE6EC9C1955F8`.
- Destino: `C:\Users\avalo\Desktop\Silen - preversion\Silen.exe`.
- Versión anterior conservada íntegra en
  `C:\Users\avalo\Desktop\Silen - preversion anterior 20261017-8BAE8874`.

Se compararon por SHA256 los **33 archivos** antes de sustituir la carpeta,
rechazando rutas fuera del escritorio y puntos de reanálisis; ninguna instancia
del portable estaba en uso. El acceso `Silen.lnk` ya apuntaba al destino correcto
y no se modificó. Manifiesto: `delivery.json`. Preversión local; sin publicación
externa ni cambios en firmas o política de actualizaciones.

## Checklist de cierre

- [x] 670 pruebas unitarias; una OEM opcional omitida, sin omisiones ocultadas.
- [x] 256 comprobaciones del portable final; build sin errores ni advertencias.
- [x] 32 poses de contraste y 33 comprobaciones del ciclo de vida de movimiento.
- [x] Tres medidas pareadas por lote y tres arranques normales; diez muestras
  conservadas con fechas reales y acciones bloqueadas hasta resultado completo.
- [x] Entrega en artifacts y escritorio, versión anterior preservada y hashes comprobados.
- [x] DESIGN.md y su Estado de adopción actualizados con evidencia y límites.
- [ ] Recorrido humano completo de teclado, lector de pantalla y hover en ambos temas.
- [ ] Alto contraste real de Windows, reducir movimiento y escalado DPI del sistema.
- Recuperación real, instalación protegida del ayudante y fallos reales del
  actualizador: fuera del cierre elegido; no se presentan como probados.

Los ficheros de evidencia citados sin prefijo están en
`artifacts/faseB-pr2-2026-10-17/`. No se aplicaron optimizaciones al PC del usuario.
