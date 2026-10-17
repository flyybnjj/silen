# Pulido de carga de Resumen · 2026-10-17

Cambio de presentación sobre `f377ab0`, sin modificar el colector, el motor de
optimización, permisos, journal, respaldos ni BLOOM. PR 2 e instrumentación del
arranque de producción siguen pendientes; esta entrega no contiene esas mejoras.

## Resultado

- Un panel «Analizando tu equipo»: paso secundario y única barra de diagnóstico,
  ink sobre pista line. Brick conserva acción principal, selección y foco.
- Se ocultan el texto y la barra del pie solo al analizar en Resumen. Otros
  procesos y otras vistas conservan su información de operación.
- «Midiendo… n/10 muestras» bajo el equipo. Sin muestras: guion pequeño muted;
  con muestras: números grandes muted. El promedio terminado pasa a ink.
- Trazas desde cinco muestras, sin rejilla, trazo de dos unidades. Reserva de
  espacio para que métricas y acción no se desplacen entre 0/10 y 5/10.
- Memoria media superior al 90 % al completar: «Memoria alta durante el análisis.
  Revisa las aplicaciones abiertas». Sin aviso provisional ni color de alarma.
- «Disponible al terminar el análisis» junto al botón bloqueado, cuyo texto no
  se atenúa por opacidad. No cambia el bloqueo funcional previo.
- Durante la carga se aplazan los detalles y la elección de producto, ya
  inhabilitados para aplicar; reaparecen completos al terminar. Espaciado de carga
  compacto, sin reducir el cuerpo de texto. Scrollbar local con trazo visual de
  cuatro unidades y área de interacción de catorce; hover y arrastre usan ink.

## Evidencia y límites

Artefactos: `artifacts/ui-carga-2026-10-17/`.

- Antes: pruebas unitarias, 666 correctas y una omitida (OEM opcional).
- Después: `tests-final.log`, 666 correctas y una omitida (OEM opcional);
  compilación en `build-final.log`, sin errores ni advertencias. Resultado WPF
  `final/interaction-checks.json`, 253 comprobaciones correctas.
- Se añadió una prueba que falló por el contador antiguo (`red/status.json`). La
  comprobación adicional de visibilidad completa detectó el recorte de la ayuda
  a 1000×680 (`clip-red/status.json`); el render final la supera.
- Doce capturas: 0/10, 5/10 y completo, claro/oscuro, ventanas de 1240×820 y
  1000×680. Son renders del contenido WPF, sin marco nativo, con datos simulados
  (CPU 19–28 %, memoria 94 %, GPU 4 %); no describen el PC del usuario.
- En carga, `ScrollableHeight = 0` en las ocho combinaciones. Al completar hay
  desplazamiento para el contenido adicional: 235 unidades a 1240×820 y 375 a
  1000×680 en este fixture. No se oculta contenido para forzar que todo quepa.
- Posición de «Optimizar PC» durante 0/10 y 5/10: Y=446, sin desplazamiento en
  ninguno de los temas/tamaños. Al terminar se retira el panel de carga y vuelve
  el contenido completo; no se acredita una transición animada sin saltos.
- Regresiones existentes cubren cancelación, fallo/denegación, proveedor vacío,
  callbacks antiguos y dos análisis seguidos. Ninguna prueba aplica cambios
  reales del sistema ni llama a una IA.
- Revisión independiente del código: sin hallazgos bloqueantes. No ejecutó
  pruebas por su cuenta; señaló que el contraste se registra, sin una aserción
  automática de umbral. La revisión de los valores y renders se hizo en esta entrega.

### Contraste y propuesta sobre la ilustración

Se renderizó el fondo bajo temperatura de CPU, RAM instalada y temperatura de
GPU ocultando sus glifos solo en el fixture; se calculó contraste sRGB/WCAG del
texto muted frente a cada píxel de esos rectángulos. Mínimos de las doce poses:
**4,78:1 claro**, **7,77:1 oscuro**. Método y geometría en
`final/interaction-fixtures/resumen-carga-observaciones.json`.

Estas poses no justifican cambiar la ilustración ni atenuar el texto. Si una
revisión en movimiento descubre un punto por debajo de 4,5:1, propongo un fondo
neutro opaco, local a los metadatos, antes que modificar BLOOM. Esa propuesta
**no se ha implementado** y debe comprobarse con la composición en movimiento.

## Verificación por vista

- [x] Carga, vacío de recursos, muestras parciales y resultado: fixtures WPF.
- [x] Acción bloqueada, explicación y desbloqueo al terminar: geometría y comandos.
- [x] Memoria alta solo al completar; 90 % exacto y desconocido sin aviso.
- [x] Claro/oscuro y ambos tamaños: doce capturas inspeccionadas.
- [x] Selección, error, cancelación y recuperación: regresiones con servicios simulados.
- [x] Alcance: estilos locales de Resumen; ShowGrid conserva true por defecto en
  las demás trazas. Sin cambios en motor, UI de otras vistas ni BLOOM.
- [ ] Recorrido manual de teclado, hover/foco y lector de pantalla.
- [ ] Alto contraste real, escalado DPI y ventana de producción.
- [ ] Movimiento completo, reducir animaciones y pausa al ocultar.
- Recuperación real y medidas de arranque/CPU/RAM: no ejercitadas en este cambio.

No hay nueva afirmación de mejora de rendimiento del PC ni de Silen.
