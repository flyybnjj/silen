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

## Registro pendiente de completar en esta entrega

- Medición optativa desde el EXE normal y comparación con el host de fases.
- Verificación de teclado posterior a la corrección y ciclo de vida de animación.
- Publicación local, prueba del portable y actualización del escritorio conservando
  la versión anterior. Registrar hash, revisión y ubicación efectiva.
- Estado de adopción y límites manuales actualizados al cerrar.
