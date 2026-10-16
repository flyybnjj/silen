# Fortnite Profile Implementation Plan

> **For agentic workers:** Implement independent file-service and WPF tasks with test-driven-development; use verification-before-completion and independent review before publication.

**Goal:** Optimizar PC y ajustes de Fortnite desde Silen con detección, cambios verificables y restauración, sin utilidades externas.

**Architecture:** FortniteGameService detecta/edita/restaura archivos. FortniteProfileService combina el fragmento WinUtil y NativeOptimizationService con pasos seleccionados. FortniteViewModel comparte OperationLifetime; navegación interna enlaza los controles existentes.

**Tech Stack:** .NET 10/WPF, APIs y registro HKCU de Windows, configuración INI, snapshot WinUtil MIT embebido.

**Spec:** docs/superpowers/specs/2026-10-14-fortnite-profile.md.

## Global Constraints

- Ningún test modifica preferencias reales ni archivos reales de Fortnite del usuario.
- No scripts remotos, dependencia de WinUtil externo ni cambios automáticos al analizar/abrir.
- Se conserva el estado original y no se sobrescribe un cambio externo al restaurar.
- Archivo del juego cerrado, normal, acotado y validado; conservar claves desconocidas, encoding, resolución y renderer.
- Potencia existente solo en AC; no crear Ultimate Performance ni tocar firmware/anticheat.

## Review Focus

- Manifiestos rotos/incompletos, selección de carpeta ajena y enlaces hacia archivos externos.
- Configuración modificada después de detectar; cancelación/crash después de respaldar.
- Portátil pasa a batería entre vista previa y aplicación; plan eliminado.
- Claves mixtas, valores GPU con otros tokens y conflicto después de aplicar.
- Clic duplicado/cierre con operación pendiente y navegación que aún abre herramientas externas.

## Tasks

- [x] Recursos WinUtil fijados, licencia, procedencia e investigación Epic.
- [x] Regresión RED/GREEN de Revisar inicio y navegación a controles internos.
- [x] Detección/edición/restauración Fortnite con fixtures y pruebas de conflictos/archivos.
- [x] Perfil Windows+Fortnite, preferencias GPU respaldadas y revalidación AC, con pruebas.
- [x] Interfaz de detección, selección de pasos, Optimizar PC, resultados y deshacer; fixtures sin mutaciones reales.
- [x] Revisión inicial independiente y cierre del agente principal; suite, publicación y smoke de distribución; documentación final y commit.

## Resultado

298 pruebas correctas (una de red opt-in omitida), 82 comprobaciones WPF del paquete y smoke publicado sin SDK correctos. Fortnite real detectado, perfil cargado y ningún ajuste real aplicado durante la verificación. La revisión independiente final se interrumpió por límite de uso; se explican el cierre y sus pruebas en docs/fortnite-review.md. Evidencia y hash del ZIP en docs/fortnite-verification.md.
