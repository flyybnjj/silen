# GitHub updates implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Auto-download published GitHub releases and apply them after an explicit restart request.

**Architecture:** Windows services own trusted metadata, streaming download and the portable installer. The existing bundled EXE runs a validated helper job after the main process exits. A WPF view-model presents progress and coordinates restart with the existing operation lifetime.

**Tech Stack:** .NET 10, WPF, HttpClient, System.Text.Json, System.IO.Compression, SHA-256; no additional end-user runtime.

**Spec:** `docs/superpowers/specs/2026-10-14-github-updates.md`.

## Global constraints

- Public repository `flyybnjj/flyyoptimizer`; exact asset `Silen-win-x64.zip`.
- Automatic download, explicit **Reiniciar y actualizar** only; diagnostics never auto-check.
- Keep `Silen.exe` plus `Archivos`, original local reports and optimization journals.
- Stable newer version, HTTPS official repo and server-provided SHA-256 required.
- No source comments, embedded credentials, implicit elevation or external scripts.
- No real Windows optimization or replacement of the user installation in tests.

## Review focus

- A release with an unknown URL, missing digest or duplicate asset must not become executable update data.
- A damaged, oversized or malicious ZIP must not write outside its isolated stage.
- A locked installation, stale parent PID or failed restart must preserve a usable previous executable.
- Closing during a download must finish cancellation and never expose a partial ZIP as ready.
- Startup checks, downloads and errors must leave normal PC operations usable; restart must respect busy/closing state.

## Task 1: Windows release, cache and installer services

**Files:** create `src/Silen.Windows/AppUpdateModels.cs`, `AppUpdateService.cs`, `PortableAppUpdater.cs` and focused tests in `tests/Silen.Windows.Tests/`.

**Shared interfaces:**

```csharp
public sealed record AppRelease(Version Version, string Tag, string AssetUrl, long Size, string Sha256);
public sealed record AppUpdateProgress(long Received, long Total);
public sealed record PreparedAppUpdate(AppRelease Release, string ArchivePath);
public interface IAppUpdateService
{
    Task<AppRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
    Task<PreparedAppUpdate> DownloadAsync(AppRelease release, IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default);
}
public sealed class AppUpdateService : IAppUpdateService
{
    public AppUpdateService();
}
public static class PortableAppUpdater
{
    public static System.Diagnostics.ProcessStartInfo CreateStartInfo(PreparedAppUpdate update);
    public static Task ApplyFromJobAsync(string jobPath, CancellationToken cancellationToken = default);
}
```

`CreateStartInfo` stages the current self-contained EXE and bounded job, forwarding only `--apply-update <jobPath>` via ArgumentList. It does not start a process. `ApplyFromJobAsync` validates its helper/cache context, re-fetches trusted metadata for that exact tag, waits for the original parent (PID + start time), checks paths/archive, installs with backup/rollback, restarts the app, and reports failure by throwing with a useful message. Testable internal seams cover filesystem application, parent wait and restart without touching the installed app. The helper must not recursively invoke app auto-check UI.

- [x] Write tests for the five review classes owned by these services and observe failures.
- [x] Implement bounded network metadata parsing, semantic comparison, cache and streaming SHA-256 download.
- [x] Implement safe ZIP inspection/extraction and install rollback; cover restart failure and blocked files.
- [x] Run focused tests, inspect diff and request spec/security review.

## Task 2: WPF integration and release docs

**Files:** create `src/Silen.App/AppUpdateViewModel.cs`; modify `ViewModelServices.cs`, `MainViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml.cs`, `InteractionSmokeChecks.cs`, App csproj and README.

Use injected `IAppUpdateService` and an injected `Func<PreparedAppUpdate, ProcessStartInfo>` for tests. The app view-model exposes installed version, state/status/progress, retry/check command, ready package and a restart command/event. The root view-model owns cancellation on close and only allows restart when idle. MainWindow starts the prepared helper and then closes through the normal close lifecycle; failure to start leaves the main app open and retryable. The helper branch is handled before normal UI creation. Only normal zero-argument and `--drivers` startup calls the auto-check/download flow.

- [x] Write fixture interaction cases for no update, auto-download, ready/restart, busy gating, errors, cancellation and closing; never start real helper in these cases.
- [x] Implement a compact sidebar update area using existing theme tokens, live status and a visible restart action only when verified ready.
- [x] Handle helper CLI early, surface update failure clearly, preserve 105 existing checks.
- [x] Set version 1.1.0 and update README download/bootstrap instructions and release notes.
- [x] Render actual WPF states and request review.

## Task 3: Verify and publish

- [ ] Review both tasks for compliance and code quality; fix important findings before release.
- [ ] Run solution tests once all code is stable, publish a fresh self-contained bundle, verify ZIP and standalone smoke.
- [ ] Use an explicit SourceRevisionId matching the final release commit when publishing; verify ProductVersion and exact asset digest.
- [x] Check code and artifact names for secrets and source comments; keep artifacts excluded from Git.
- [ ] Push source to main, create v1.1.0 as draft, upload the verified ZIP, compare GitHub digest, then publish as latest.
- [ ] Verify unauthenticated download link; explain that 1.0 needs one manual upgrade.

## Execution ledger

- Ruling: user authorization to implement and publish this app continues; no new approval gate is introduced for this requested feature.
- Ruling: ship 1.1.0 including the already committed Edge fix; 1.0.1 was prepared but never published.
- Edge baseline: 325 tests pass, one network test skipped; extracted portable passed 105 interactions and native scan. Read-only native permission probe reproduced denied HKCU Policies parent. Source commit `2cef6fc`.
