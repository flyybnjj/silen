# Cierre de pendientes aprobados · 2026-10-17

Base limpia: `4c34a67`; control de rendimiento solicitado: `f377ab0`.
Se continúa en la rama de trabajo existente `codex/windows-mvp`, conservando
artefactos anteriores y un commit por grupo. No publicar ni aplicar ajustes al PC.

## Alcance aprobado y criterios

1. **PR 2 del diagnóstico:** tras identidad/RAM/unidades, iniciar la observación
   y un único trabajador de inventario (drivers/PnP, inicio, procesos, red).
   Máximo dos tareas por análisis; notas y listas privadas por tarea, unión antes
   de publicar el snapshot. Progreso tipado serializado y contador monótono.
   Diez muestras, esperas de un segundo y fechas de adquisición reales intactas.
   Presupuesto cooperativo global de 35 s; cancelación del usuario diferenciada
   del agotamiento del presupuesto. Esperar ambas tareas al cancelar/fallar: no
   abandonar tareas que sigan consultando el equipo. WMI conserva timeout de 5 s;
   una llamada nativa en curso puede tardar en observar la cancelación.
2. **Regresión:** demostrar solapamiento con proveedor bloqueado, límite de dos
   tareas, errores por proveedor, cancelación/unión, presupuesto global, diez
   muestras y progreso. Mantener bloqueo de Aplicar hasta diagnóstico completo.
3. **Medir:** tres repeticiones alternadas control/candidato con el protocolo
   existente y misma configuración de publicación. Mediana/rango del análisis,
   ventana, CPU y memoria. Conservar PR 2 solo con mejora total observable;
   rangos solapados no bastan. Medir hitos con la sonda anterior y arranque
   completo, incluyendo InitializeAsync, OEM, GitHub y perfiles, identificando
   toda sustitución por seguridad. No confundir sonda con producción.
4. **Verificación de interfaz:** cerrar pruebas ejecutables de teclado/foco y
   estados de movimiento sin alterar BLOOM. No acreditar lector de pantalla,
   alto contraste real o cambios globales de Windows que no se ensayen.
   El fondo neutro local para metadatos es condicional a contraste insuficiente;
   no incorporarlo sin evidencia.
5. **Entrega:** actualizar evidencia y Estado de adopción; compilar y probar
   portable. Actualizar artifacts y preversión del escritorio conservando la
   anterior. No borrar respaldos ni reemplazar un ejecutable en uso.

## Pendientes de publicación separados

La instalación protegida del ayudante requiere concretar distribución y firma;
el usuario eligió «Primero diagnóstico, verificaciones y portable»: queda fuera
de esta entrega. No inferir que existe certificado privado.
Las pruebas de interrupción del actualizador se harán únicamente en carpetas
descartables si se incluyen, nunca sobre la instalación o datos del usuario.
Sin reinicios, modificaciones de registro/servicios ni cambios globales del PC.

## Registro de ejecución

- Inicio: árbol limpio verificado; control exportado con git archive para no
  cambiar HEAD ni la rama activa. Baseline unitario/publicación en curso.
- Baseline: 666 pruebas correctas, una OEM opcional omitida. PR 2: 670 correctas
  y la misma omitida; compilación sin errores ni advertencias.
- Dos regresiones iniciales fallaron antes de implementar: ausencia de
  solapamiento y consultas sin presupuesto global. Pasan después. La prueba
  antigua de progreso asumía que ninguna de las cuatro primeras etapas podía
  contener muestras; se adapta al orden concurrente manteniendo 14 pasos
  monótonos, básicos previos al inventario y diez muestras reales.
- Tres repeticiones alternadas del portable: control mediana 14,503 s
  [14,380–14,774]; candidato 11,864 s [11,863–12,076]. Se conserva PR 2 por
  mejora observable. Arranque con dependencias reales y verificación WPF en curso.
