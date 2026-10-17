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

## Registro pendiente de completar en esta entrega

- Medición optativa desde el EXE normal y comparación con el host de fases.
- Verificación de teclado posterior a la corrección y ciclo de vida de animación.
- Publicación local, prueba del portable y actualización del escritorio conservando
  la versión anterior. Registrar hash, revisión y ubicación efectiva.
- Estado de adopción y límites manuales actualizados al cerrar.
