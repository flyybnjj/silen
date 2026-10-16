# Silen Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Rename the existing Windows portable application to Silen while preserving access to existing reports and undo backups.

**Architecture:** Rename the solution, project folders, assemblies, namespaces, executable, resources, packaging contracts and first-party documentation together. Resolve persistent storage through one shared Core helper that retains an existing legacy root and selects Silen for fresh installations. Keep the actual GitHub repository and the shared update lock identifiers.

**Tech Stack:** C#, WPF, .NET 10, xUnit, PowerShell.

**Spec:** User brief in this chat, 2026-10-15: phase 1 rename before design; inventory and compatibility warning before changes; compile and start. Phase 2 proposes three brand directions and waits for a selection before any visual redesign.

## Global Constraints

- Phase 1 changes names only; preserve layout, typography, colors, logo geometry and icon bytes.
- New executable `Silen.exe`; archive/root `Silen-win-x64.zip` / `Silen-win-x64/`.
- Retain `flyybnjj/flyyoptimizer` as the real external repository identifier.
- Retain `.flyy-update.lock` for cross-version exclusion.
- Reuse `%LocalAppData%\FlyyOptimizer` when present, including when both roots exist; otherwise use `%LocalAppData%\Silen`. Never move or copy user data.
- Document manual first installation from old clients and shortcut changes.
- No release publication, repository migration, system tuning, driver downloads or elevated launch.

## Review Focus

- Existing reports and optimization/Fortnite undo journals remain reachable without duplicate locks.
- All XAML namespaces, embedded resources, elevation targets and project references match renamed assemblies.
- Updater archive, helper, installer and tests agree on new package and executable names while retaining trust checks.
- Documentation does not advertise an unpublished download URL.
- Portable starts from an extracted ZIP without relying on an installed SDK.

### Task 1: Coordinated mechanical rename

**Files:** `Silen.sln`, `src/Silen.*`, `tests/Silen.*`, `scripts/*.ps1`, README, first-party docs and notices. File inventory: `artifacts/silen-rename-inventory.md`.

- [x] Read-only inventory and compatibility audit; communicate scope before writes.
- [x] Baseline tests: 396 passed, 1 skipped.
- [x] Rename tracked names and references, preserving external repository and shared lock.
- [x] Review transformed content and confirm only intended legacy references remain.

### Task 2: Persistent storage compatibility

**Files:** Create `src/Silen.Core/AppDataPaths.cs`, `tests/Silen.Tests/AppDataPathsTests.cs`; update Core project, six storage consumers and history label binding.

- [x] Test fresh, legacy, modern-only and both-root cases in isolated temporary directories, with no migration or writes by the resolver.
- [x] Observe a legacy-root assertion fail before implementing fallback.
- [x] Route all six consumers through the fixed per-process root; show actual location in history.
- [x] Run Core tests and complete solution tests.

### Task 3: Packaging and startup verification

**Files:** `scripts/build.ps1`, `scripts/verify-portable.ps1`, generated ignored artifacts.

- [x] Run solution tests (400 passed, 1 skipped), then `scripts/build.ps1 -SkipTests`: Release build and self-contained portable publication, exit 0; 0 warnings/errors.
- [x] Run `scripts/verify-portable.ps1`: ZIP consistency and app startup/interaction smoke from an extracted path with spaces and without SDK lookup.
- [x] Inspect screenshot and status evidence; compare artwork bytes and visual declarations against baseline.
- [x] Independent review of rename and compatibility changes; resolve material findings.
- [x] Report evidence, retained compatibility identifiers and three text-only brand directions; wait for the user's selection.

## Verification and open decisions

- Portable verification: exit 0, 28 archive files, 125 interaction checks, no applied system changes or gaming capture.
- Evidence: artifacts/silen-build.log; artifacts/silen-portable-verification.log; artifacts/portable verification 24790b0242064cef8a02ddef8430bfe0/smoke/status.json.
- Icon git blob before/after: cd1ba0b7a253307843e73aae6607ffdf1307f52a.
- Independent review: no material issues; corrected data-path wording in user guide, native tools and Fortnite docs.
- Work remains uncommitted in the existing checkout; no repository migration or release publication.
- User follow-up: copy exclusions undecided, and Silen remains provisional while public naming collisions and TikTok availability are assessed. Three text-only identity options prepared; implementation waits for the user's selection.
