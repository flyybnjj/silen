# Silen — butterfly background motion

The user asked for background movement «tipo mariposa». The previous 36-second,
approximately one-percent breathing movement was too subtle behind the blur.
This change keeps BLOOM's existing artwork and gives its membrane a visible
wingbeat rather than replacing the mascot or adding a literal butterfly.

- [x] Curve both sides around a soft vertical hinge; wing tips lift in depth and
  the mesh width closes from 3.5 to 2.12037 units (approximately 39 percent).
- [x] One slow wingbeat every 4 seconds; an 8-second flight loop adds a small
  opposing sway and hover. The cycle closes continuously in the original pose.
- [x] Keep the blurred layer behind static controls, with unchanged text backing.
  Slightly reduce background scale and move it inward so more of the shape is
  visible while it folds. Render active ambient motion at 24 fps.
- [x] Keep existing pause/disposal policies. Reduced motion displays the original
  open pose rather than freezing the wings halfway closed.
- [x] Capture a complete 8-second background cycle in the native app, separately
  from startup. Diagnostic frame capture uses a shorter settle interval plus an
  actual WPF rendering event; existing smoke screenshots keep their old timing.

## Verification

- Real WPF geometry probe: before the change, 2 motion assertions failed; after
  the change, all 6 checks passed. Verified visible closure, depth, exact loop
  reset, finite positions, smooth join and no inverted/collapsed triangles
  across 96 samples. Logs: `artifacts/silen-butterfly-geometry-{red,green}.log`.
- Full Release build/publish: 439 tests passed, one optional live OEM test
  skipped; 0 build warnings/errors. `artifacts/silen-butterfly-build-final.log`.
- Extracted portable: 29 archive files verified; 125 interaction checks passed,
  no system changes. `artifacts/silen-butterfly-portable-final.log`.
- Portable screenshot evidence:
  `artifacts/portable verification 0d73f3cf8a5e4e248237146c58fe7539/smoke/`.
- Native motion capture from the self-contained executable: 84 startup frames
  plus 192 ambient frames, 24 fps, finite geometry; peak measured frame-update
  CPU time 2.0211 ms (not total GPU or display frame timing).
- Visually inspected open and folded sharp poses, native blurred background
  poses at 0 and 2 seconds, and an encoded-video frame.
- `artifacts/silen-butterfly-preview/silen-butterfly-background.mp4`: H.264,
  1224×782, 8 seconds / 192 frames, full decode successful.
- Final `artifacts/Silen-win-x64.zip` SHA-256:
  `3206414D855D6ABB4EA24376D958F81CCE06EDFA6F4C1795336B07B7E6ABE610`.
- No new bitmap, runtime service, external upload, commit or release publication.
