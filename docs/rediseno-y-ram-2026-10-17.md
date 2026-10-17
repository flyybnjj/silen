# Corrección de uso real y rediseño de Silen

Fecha: 2026-10-17. El usuario rechazó la entrega anterior y eligió grafito inicial.

## Fallos reproducidos

1. El inicio anterior renderizaba frases y un panel de aplicación a pantalla completa.
   La regresión roja `red/status.json` comprueba el texto rechazado. Ahora el arranque
   de producción crea una ventana compacta transparente con Bloom; la principal no se
   muestra hasta la identidad y primera muestra. No se añade demora artificial.
2. RAM y red tenía su propio estado sin inicialización. El cambio de vista y el timer
   además exigían CanStartOperation: no leían durante diagnóstico u OEM. La regresión
   `ram-red/status.json` falla sin pulsar Actualizar. Ahora los contadores nativos se
   leen al iniciar, al entrar a la vista y durante el timer, independientemente de
   esas operaciones. El constructor del timer ya lo iniciaba: no era un timer apagado.
3. Un error de procesos eliminaba también RAM válida. Las fuentes son independientes:
   conservar cada lectura válida, retirar la fuente fallida e informar su error local.

## Presentación

La carga solo contiene la mascota, sin texto ni fondo. El PNG comprueba alfa cero en
su esquina y poses diferentes. Escape cancela la carga. Pausa al perder foco u ocultarse
y respeta la reducción de animaciones. La ventana principal conserva confirmaciones,
reversibilidad, journal y elevación por acción.

Grafito se aplica a nuevas preferencias. Se conserva Source Serif 4 y se aumenta el
texto auxiliar a 14 con formateo Display. Las superficies de lectura son opacas; la
ilustración ambiente queda detrás y no atraviesa porcentajes, nombres ni temperaturas.
El tratamiento compartido se aplica a Resumen, RAM y red, Drivers, Ajustes, Historial,
Bloom y los diálogos. Se conserva Claro y el alto contraste de Windows.

Los optimizadores incluyen búsqueda/categoría y un Aplicar fijo. La selección vacía
manual deja de ocupar espacio. Filtrar nunca desmarca opciones; la confirmación sigue
enumerando toda la selección. Se corrige la categoría nula que podía producir una
lista vacía tras actualizar disponibilidad.

Scrollbar visual de 4 unidades, con zona nativa de arrastre. La rueda usa transición
corta de píxeles, mantiene el recorrido de teclado y no reemplaza el scroll lógico de
tablas virtualizadas. El arrastre y el teclado cortan la transición. El modo de reducir
movimiento conserva la rueda nativa.

## Verificación

Evidencia: `artifacts/revision-uso-real-2026-10-17/`. No se aplican ajustes reales a Windows.

- Base: 692 unitarias correctas, una OEM opcional omitida.
- Tras la corrección: 694 unitarias correctas (140 Core, 497 Windows, 57 Gaming), una OEM omitida.
- 276 comprobaciones WPF de las fuentes, incluidas RAM durante análisis/OEM, fuentes lentas,
  fallos independientes y cancelación del semáforo; carga transparente, poses, rueda y filtros.
- Compilación completa con .NET 10: cero errores y advertencias.
- Capturas `confirmacion/interaction-fixtures/rediseno-{0..5}-{Claro,Oscuro}-{1000,1240}.png`.
- Carga: `inicio-transparente-{0,1}.png`; archivos con transparencia, no un fondo negro.
- La revisión independiente detectó filtros nulos y cancelación incoherente tras publicar
  un snapshot. Se corrigieron: categorías vacías vuelven a Todas; cancelar el enriquecimiento
  RAM conserva el diagnóstico completo con mensaje literal y sin iniciar OEM.

La verificación de la rueda prueba el desplazamiento y sus límites, no la sensación
subjetiva ni la fluidez GPU. Lector de pantalla, DPI, alto contraste real y recorrido
humano completo quedan sin marcar. No se atribuye mejora de rendimiento del PC.
