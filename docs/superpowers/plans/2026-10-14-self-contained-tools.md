# Self-contained Tools Implementation Plan

> **For agentic workers:** Execute independent packaging, native-tools and WPF tasks with test-driven-development; use verification-before-completion before publishing.

**Goal:** Entregar Silen autocontenido con acciones útiles en Mejoras y Gaming sin instalar utilidades externas.

**Architecture:** Windows implementa INativeOptimizationService; App compone controles con OperationLifetime; Gaming resuelve PresentMon verificado incluido; el build copia el binario y sus avisos/licencias.

**Tech Stack:** .NET 10/WPF, registro del usuario, APIs de energía de Windows, archivos locales y consola standalone PresentMon.

**Spec:** docs/superpowers/specs/2026-10-14-self-contained-tools.md.

## Global Constraints

- Los tests no aplican ajustes reales del PC ni limpian temporales reales.
- No scripts remotos, servicios/instaladores externos ni descargas necesarias en el primer uso de Gaming.
- Los ajustes guardan el estado exacto anterior y no sobrescriben cambios externos al deshacer.
- Las acciones comparten bloqueo, cancelación y espera de cierre de la app.
- La limpieza usa vista previa, selección, edad mínima y validación del alcance; no tiene undo.

## Review Focus

- PresentMon ausente/manipulado, carpeta con espacios y dependencias nativas en equipo sin SDK.
- Claves ausentes, valores de tipo inesperado y cambio externo después de aplicar.
- Fallo entre respaldo y mutación, acceso denegado y varias instancias.
- Limpieza cancelada, archivo cambiado/bloqueado y junctions/symlinks bajo Temp.
- Estado vacío, carga de historial y acciones concurrentes de Gaming/Mejoras.

## Tasks

- [x] Empaquetar consola PresentMon standalone oficial y resolverla con verificación local; pruebas de dependencia y help sin ETW.
- [x] Implementar herramientas nativas con respaldo/undo/conflictos y limpieza seleccionada; regresiones con proveedores de fixture.
- [x] Mejoras con controles reales; Gaming sin selector de ejecutable externo; pruebas deterministas de interacción.
- [x] Estilo claro inspirado en Apple, tipografía nativa y transiciones accesibles WPF en todas las secciones/controles.
- [x] Revisión independiente, suite completa, distribución autocontenida y smoke del ejecutable publicado.

## Resultado

251 pruebas correctas y una consulta de red opt-in omitida; 63 comprobaciones de interacción en el ejecutable publicado; revisión independiente sin defectos concretos abiertos, incluida elevación por clic para el error real de permisos ETW. Build/publicación sin errores ni advertencias, ZIP verificado contra el apphost/ensamblado/colector probado. El smoke se ejecutó sin PATH de SDK ni DOTNET_ROOT, con análisis y catálogo por modelo reales y sin aplicar ajustes. Evidencia y límites en `docs/self-contained-verification.md`.
