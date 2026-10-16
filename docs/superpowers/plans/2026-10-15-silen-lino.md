# Silen Lino Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Apply the user's selected Lino identity, brick-red details and exact lowercase serif wordmark to the existing Windows app.

**Architecture:** Shared WPF resources own color, control styles and the embedded UI font. A separate Brand.xaml owns traced vector artwork. Restyle the existing window and dialog without altering commands or backend behavior.

**Tech Stack:** C#, WPF, .NET 10, PowerShell, existing xUnit and interaction smoke harness.

**Spec:** `PRODUCT.md` and the user's approved Lino palette and supplied serif wordmark in this chat, 2026-10-15.

## Global Constraints

- Palette `#F4F2EC`, `#292B27`, `#B23B35`; neutral derivatives only. Source Sans 3 is the sole UI font; serif logo is vector artwork.
- No gradients, shadows, glows, glass, decorative pulse art, Unicode icons, invented metrics or performance claims.
- Preserve all existing operations, data compatibility, confirmations, update controls and restore paths.
- Keep Pages and its indexes, direct page ScrollViewers and named navigation sections.
- Changes remain in the existing checkout with the completed, uncommitted rename. No publication, push or commits are requested.

## Review Focus

- All navigation and update controls remain reachable at 1000×680, including expanded errors.
- Fonts and wordmark load in the extracted self-contained portable without installed font dependencies.
- Disabled, checked, focus, error and progress states remain legible and keyboard usable.
- Wordmark reproduces supplied letters, excluding sample-board chrome; no legacy F artwork remains.
- Actual hardware readings, detailed recommendations, restoration and irreversible-cleanup notices remain intact.

### Task 1: Brand artwork and font

- [x] Trace supplied wordmark to reusable WPF/SVG vector artwork and verify a preview.
- [x] Embed official Source Sans 3 Regular/Semibold resources and license.
- [x] Derive a small Windows icon from the same serif s and update the reproducible icon script.

### Task 2: Shared identity and window composition

- [x] Replace cool palette and multiple accent colors with warm neutrals and brick red.
- [x] Restyle all controls and content sections with thin rules and restrained focus states.
- [x] Place text navigation above one broad content column; remove decorative icons and duplicate startup actions.
- [x] Apply the wordmark to main window and confirmation; keep concrete action copy and all existing commands.
- [x] Remove hardcoded blue/amber trace and finding colors.

### Task 3: Verification and delivery

- [x] Run full existing Release tests and WPF build/publication; use meaningful existing interaction checks instead of tests mirroring cosmetic values.
- [x] Run extracted portable smoke: six pages, small/normal windows, empty/live/error/update states, dialog, and all existing interactions without applying real system changes.
- [x] Inspect a batched screenshot matrix and vector fidelity; fix material issues in one batch and confirm if needed.
- [x] Independent code and visual review against this brief.
- [x] Record final design tokens/evidence and deliver portable plus preview.

## Verification evidence

- Full Release suite: 400 passed, 1 optional live OEM test skipped, 0 failed (`artifacts/silen-lino-build.log`).
- Final Release build/publication: exit 0; 0 warnings, 0 errors (`artifacts/silen-lino-build-final.log`).
- Final extracted portable: 29 files verified, 125 interaction checks; no gaming capture or applied system changes (`artifacts/silen-lino-portable-final.log`).
- Screenshot matrix: `artifacts/portable verification 2ebc2f3fb3944d92b3f413068cd72837/smoke/`; final preview `artifacts/silen-lino-preview.png`.
- First visual pass identified a minimum-height History regression caused by fixed equal grid rows; replaced with scrollable stacked sections. An isolated WPF fixture failed before the fix (list 0 px, row 43 px) and passed after it (list 45 px, row 43 px). Logs: `artifacts/silen-history-layout-red.log` and `artifacts/silen-history-layout-green.log`.
- Corrected CPU trace to ink, hid sampling details in the empty state, and replaced obsolete deep-capture offsets with named section anchors.
- Independent initial review plus final verdict: all five listed fixes resolved; no material remaining concern. Font/wordmark/resources, minimum navigation, update errors, tables, dialog and history inspected.
- Brand resources are original traced user artwork; Source Sans 3 static fonts and OFL are embedded/included. No AI-generated raster assets were used.
- Native WPF verification used the existing runtime harness; no web detector applied. No commits, push or external release publication.