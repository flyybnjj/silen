# Silen — serif typography and composition

## User intent

The user confirmed that BLOOM and its butterfly background motion are right,
and asked to improve typography and general composition from the first view.
They want the letterforms throughout the app to resemble the Silen logo.
Keep the existing mascot, animation, palette, functions and original wordmark.

## Design

- Source Serif 4 Regular/Semibold throughout all app-owned UI: headings, body,
  buttons, tables, navigation, tooltips and confirmation. The source image has
  no identified font; this is a close serif match, not an exact-font claim.
- Preserve the original traced wordmark. Compare Times New Roman, Georgia,
  Gelasio and Source Serif 4 against it before choosing the embedded family.
- A 205 px text sidebar separates navigation from the main work area. Preserve
  all six pages, indices, commands, keyboard navigation and named section anchors.
- First view: «Conoce tu equipo», concise purpose, one «Analizar equipo» action.
  Collapse the duplicate Summary header only while this welcome is displayed.
- Hierarchy: welcome 46/54, page 34/44, section 20/28, body 15/24, metadata 13/20.
  Use lining figures and tabular metrics. Keep footer update controls reachable.
- Keep the actual storage path in History's tooltip while labeling its meaning
  «Guardado en este equipo». No data paths or saved-user compatibility change.

## Work and verification

- [x] Embed original static Adobe font files at pinned revision
  `80d3f8894c09c937bebfa9011247d2e1c79fd6f4`, include OFL and update notices.
  Explicitly package only Source Serif files, leaving earlier font source files
  as unshipped historical inputs.
- [x] Native font probe resolves both actual embedded weights and Spanish
  punctuation, diacritics and numbers. Complete assembly-qualified font URIs
  avoid fallback when WPF resources are loaded from a separate test assembly.
- [x] First batched native review: empty, live, six pages, normal/minimum sizes,
  dialog, Gaming options, update-ready and expanded restart-error states.
  Fresh output `artifacts/silen-serif-review-final`, all 125 checks pass.
- [x] One correction batch: include the font on tooltips and simplify the History
  storage label while retaining its actual location. No further visual variants.
- [x] Full Release tests/build and self-contained portable publication.
- [x] Final extracted-portable verification and bounded visual confirmation.

## Final evidence

- `artifacts/silen-serif-build-final.log`: 439 tests passed, one optional live OEM
  test skipped; 0 build warnings/errors, self-contained publish successful.
- `artifacts/silen-serif-portable-final.log`: 30 archive files verified and 125
  interaction checks passed. Six views rendered at normal/minimum sizes, with
  no applied system changes or gaming capture.
- Extracted-portable evidence:
  `artifacts/portable verification 1e252e602f2f48fcab357bf79c6c7e11/smoke/`.
- Final bounded visual confirmation: welcome, History minimum and update-ready
  minimum. No further edits needed. Preview: `artifacts/silen-serif-preview.png`.
- Font verification: `artifacts/silen-serif-font-verification.log` resolves
  Source Serif 4 Regular and Semibold from the embedded resource, with actual
  matching weights, Spanish punctuation/diacritics and numbers.
- Font SHA-256: Regular
  `E5A4EE6A3D87BB9024796BE390C6771E2A0EB1883DAE25EFFAF57CA01668E24B`;
  Semibold `36DB62940CB5728B12B1802476DC7FCF4C6C519A7BDD476BA23A4E555FC4655F`.
- Final `artifacts/Silen-win-x64.zip` SHA-256:
  `60F9E10A16A53CE3E80C2E8D20D6840F521082AECEBE0709C1895590359D4283`.

Initial interaction verification caught an assertion tied to the old literal
update-status copy; it now checks actual idle/readiness/version behavior. A rerun
in the same fixture directory then loaded earlier fixture actions (10 instead of
the fresh expected 5); the verification was repeated in a new output directory,
without changing product history or backend behavior.

No new animation assets, services, system adjustments, commit or external release.
