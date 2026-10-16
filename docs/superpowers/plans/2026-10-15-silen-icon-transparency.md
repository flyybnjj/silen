# Silen — native icon size and transparency

The user showed the icon too small in the taskbar and a black tile against the
white native title bar. The exporter explicitly painted a dark rounded rectangle
and fitted the wide mascot with its original transparent margins inside it.

- [x] Remove the painted background and preserve alpha throughout ICO export.
- [x] Trim transparent input margins and give only the icon a 24-degree optical
  tilt, filling a square without distorting the original mascot or clipping lobes.
- [x] Export exact 16/20/24/28/32/40/48/56/64/96/128/256 px native frames.
- [x] Independently decode all 12 ICO frames: alpha is zero at every corner;
  content fills at least 85% width and 80% height. The 16 px frame has a 16×14 px
  visible extent. `artifacts/silen-icon-alpha-checks.json`.
- [x] Inspect before/after proof at 16, 24 and 32 px on white/dark surfaces.
  `artifacts/silen-icon-comparison.png`.
- [x] Build/publish and inspect icons actually embedded in the executable.
- [x] Verify the extracted portable and native WPF window icon resource.

## Follow-up: legibility at native sizes

The user supplied new screenshots: the transparent photographic icon still
looked indistinct. Merely enlarging it did not preserve the folds at 16/32 px.

- [x] Draw a separate optical vector derivative of the existing folded BLOOM
  silhouette in three solid red tones. Preserve the original app mascot.
- [x] Render SVG Bezier paths directly into every native ICO frame, replacing
  photographic downsampling. Use one SVG artwork source for reproducibility.
- [x] Inspect actual 16/24/32 px frames against white/dark backgrounds. Check
  every frame's transparent corners, occupied extent and transparent fraction.
  `artifacts/silen-icon-comparison.png`, `artifacts/silen-icon-alpha-checks.json`.
- [x] Rebuild/publish with the vector icon; verify Windows-extracted small and
  large executable icons and run the extracted portable's interaction checks.

Final evidence: `artifacts/silen-icon-vector-build.log` (0 warnings/errors),
`artifacts/silen-icon-vector-executable-checks.json` (Windows 16/32 px icons
match the WPF-decoded source frames, transparent corners), and
`artifacts/silen-icon-vector-portable.log` (30 archive files verified, 125
interaction checks passed in a fresh extraction). ZIP SHA256:
`11B23E7E38DB9B8C6227C98179C1F9CBBE58FB010CE38551988C76D45D96E74C`.

No mascot/background, typography, UI layout, compatibility paths or app behavior
change. No Windows-wide cache removal, Explorer restart or pinned-shortcut change.
Existing backend tests are unchanged; run the build plus focused native icon and
portable checks rather than repeating unrelated backend/gaming tests.
