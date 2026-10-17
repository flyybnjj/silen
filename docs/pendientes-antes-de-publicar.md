# Pendientes antes de publicar Silen

Estos puntos no se acreditan con los simuladores ni con las mediciones de rendimiento.

- [ ] **U-01: interrupciones reales del actualizador.** En un entorno descartable, provocar la terminación del proceso tras cada movimiento de archivo/directorio y probar la salida inmediata de la nueva versión. Comprobar qué versión queda arrancable, conservación de la anterior y recuperación de la transacción. Los tests actuales simulan fallos y el acuse de arranque; no ejecutan una campaña real de crashes o cortes de energía. Referencia: [Fase 2 del motor](motor-fase2-2026-10-16.md).
- [ ] **Ayudante elevado en instalación protegida.** Decidir la distribución separada y firmada en una carpeta no escribible por el usuario (por ejemplo Program Files), autorización y mantenimiento del componente. La restricción a System32 de S-01 sigue vigente, pero no convierte la carpeta portable en una ubicación protegida. Es una propuesta de publicación, sin implementación en el PR 1 de rendimiento; estimación previa de 5–10 días más certificado y mantenimiento, pendiente de diseño detallado.
