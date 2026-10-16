# Silen — BLOOM mascot and motion

## User intent

The user supplied an organic red translucent form and clarified that it is the
app's mascot and AI presence. It is not a black serif s transforming into a new
logo. The original was recovered from the ChatGPT conversation **Nombre y marca
app PC**; the attached concept board calls it **01 BLOOM**. Preserve its folded
silhouette and lighting. Keep the separately approved serif wordmark and Lino
interface.

## Implementation

- [x] Recover original user preferences and image, and document retrieval limits:
  user messages and attachment were available; assistant replies were content
  references without readable bodies.
- [x] Restore the exact supplied image with transparency using the built-in image
  tool; embed the 1534×1025 PNG and record the prompt/provenance.
- [x] Replace the approximate sculpture and all monogram morph code with a native
  textured mesh: 2,665 vertices, 5,120 triangles, subtle nonlinear breathing,
  36-second loop, rest poses identical at phase 0 and 1.
- [x] Show the red mascot immediately with a 0.3-second fade, reveal the usable
  interface by 0.95 seconds and settle into a blurred backdrop by 1.8 seconds.
  Input skips the remaining intro. Paper backing follows content visibility so
  the slower artwork transition cannot compromise readability.
- [x] Pause rendering/clock when inactive, hidden, minimized or busy; detach at
  disposal before async closing. Inactive startup presents a readable static app.
  Respect reduced-motion and high-contrast preferences.
- [x] Replace the standalone-s Windows icon with BLOOM in seven native icon sizes.
- [x] Add deterministic mascot preview captures and preserve existing portable
  diagnostic behavior. This work does not introduce an AI service or a fake chat.

## Evidence to complete

- [x] Timeline tests: 37 initial contract checks; two additional legibility
  regressions fail before the paper-backing fix and pass after it, 39 total.
  Logs: `artifacts/silen-mascot-legibility-{red,green}.log`.
- [x] Real WPF lifecycle probe: 32 assertions, no failures or skips; inactive
  startup, real activation/rendering, busy, minimize/restore, hide/show,
  mid-intro deactivation, captures and real close/disposal. Probe lives only in
  ignored artifacts and never initializes/scans/applies settings.
- [x] Full Release tests, build and self-contained portable publication.
- [x] Inspect native sharp/intro/settled captures against original reference.
- [x] Extracted portable: six pages, normal/minimum sizes and interaction suite.
- [x] Encode and inspect a reviewable MP4 from the actual application frames.

## Final verification

- `artifacts/silen-bloom-build-final.log`: 439 passing tests, one optional live
  OEM check skipped; build 0 warnings/errors, publish successful.
- `artifacts/silen-bloom-portable-final.log`: 29 archive files verified and 125
  interaction checks passed from the extracted self-contained executable; six
  pages at normal/minimum sizes, no system changes applied.
- Portable screenshot evidence:
  `artifacts/portable verification 9132cf39605b41149e594564ee2e2612/smoke/`.
- `artifacts/silen-bloom-preview/motion-status.json`: finite geometry, 84 native
  frames at 24 fps, 1.8-second intro, 36-second ambient cycle, no letter
  transformation. Peak measured frame-update CPU time 1.9967 ms; this measures
  the update routine, not total GPU composition or display frame rate.
- Inspected sharp artwork, intermediate intro frames, final backdrop and
  extracted-portable summary, Drivers and History at minimum size. The paper
  backing follows the content fade; no white additive-artwork washout remains.
- `artifacts/silen-bloom-preview/silen-bloom-startup.mp4`: H.264, 1224×782,
  84 frames / 3.5 seconds. Full decode passed; a decoded opening frame was
  visually checked against the native capture.
- `artifacts/Silen-win-x64.zip` SHA-256:
  `56C7D6781A12B76893C6908693E9CE1A73187F016E904E0EF07C48915238CD5E`.
- No commit, push, external release or external upload was performed.

## Integration notes

The first runtime capture found that a relative BitmapImage URI was resolved as
a disk path. The asset now uses a complete WPF pack URI. Visual inspection also
caught additive emissive material washing out colors on the light background;
the texture uses diffuse material with neutral ambient illumination instead.
The restoration retains dark membrane interiors and the original alpha; no
live API, image generator or video codec is shipped with the app. The MP4
encoder, when used, is pinned to a local artifacts-only helper installation.
