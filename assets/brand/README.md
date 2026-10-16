# Silen artwork

`silen-wordmark.svg` and `src/Silen.App/Brand.xaml` contain the lowercase serif
wordmark supplied by the user. `silen-monogram.svg` records its initial `s` for
artwork history; it is no longer used for the Windows icon or startup animation.
The Windows icon uses an optical vector derivative of BLOOM on a genuinely
transparent canvas. `silen-bloom-icon.svg` is its single artwork source;
`scripts/create-brand-icon.ps1` renders those filled Bezier paths directly at
16, 20, 24, 28, 32, 40, 48, 56, 64, 96, 128 and 256 px and writes a multi-frame
ICO. Native title bars and taskbars therefore receive their own exact-size
frames rather than a shrunken photograph.

The icon retains the folded silhouette using three solid red tones. Glass
reflections and dark translucent membranes are omitted at these small sizes,
where they merged into an indistinct blob. An 8-degree optical tilt balances
the lobes within a square. Visible extent is 16×16 px in the smallest frame;
all twelve frames retain transparent corners and at least 30% transparent
canvas. The app mascot, background image and motion retain their existing
artwork and orientation. The original wordmark is unchanged.

`logo-reference.png` is the original 233 × 201 px sample board, retained only to
document the supplied artwork. It is not an application resource and is not the
logo shown in the application. The board border and “1·Wordmark serif” caption
are excluded from every vector and icon.

The artwork was traced directly from the reference, rather than substituted
with a similar font. Its visible letterforms occupy approximately 95 × 36 px.
The trace used the source region `(78, 74, 176, 113)`, normalized the background
and foreground grayscale values (21 and 239), removed subpixel edge noise with
a 0.5 px Gaussian filter, interpolated the silhouette 8×, then used Potrace 1.16
with `--alphamax 1.0 --opttolerance 0.3 --unit 100`.
The Bézier paths were converted to native WPF geometry without flattening.
The small raster source limits how precisely the original pre-rasterized curves
can be recovered; letter shapes and spacing are preserved at source scale.
The wordmark is artwork and adds no second UI font family.

The original artwork was provided by the user. Potrace is a build-time tracing
tool only, downloaded from <https://potrace.sourceforge.net/>; it is not bundled
with the application.

## UI typeface

Source Serif 4 Regular and Semibold static TTF files are unmodified files from
Adobe's official Source Serif repository, pinned revision
`80d3f8894c09c937bebfa9011247d2e1c79fd6f4`:

- <https://github.com/adobe-fonts/source-serif/tree/80d3f8894c09c937bebfa9011247d2e1c79fd6f4/TTF>
- License: `licenses/SourceSerif4-OFL.txt` (SIL Open Font License 1.1)
- Runtime font family: `Source Serif 4`
- WPF URI: `pack://application:,,,/Silen;component/Assets/Fonts/#Source Serif 4`

The font files live in `src/Silen.App/Assets/Fonts` and are embedded WPF
resources. The project explicitly includes only `SourceSerif4-*.ttf`; the earlier
Source Sans source files remain historical inputs and are not app resources.
A native WPF typeface check resolves both embedded faces, their actual weights,
Spanish punctuation/diacritics and numbers without relying on installed fonts.

The original wordmark has no identified font metadata. Times New Roman,
Georgia, Gelasio and Source Serif 4 were compared against its traced letterforms.
Source Serif 4 carries the same serif character through all UI roles; it is not
claimed to be the exact original font, and the traced wordmark remains intact.

## BLOOM mascot

The user clarified that the organic red shape is Silen's AI mascot, not a
transformation of the letter s. The original reference was recovered from the
ChatGPT conversation **Nombre y marca app PC**. Its image board labels the
concept **01 BLOOM**. `bloom-reference.jpeg` preserves that board for provenance;
it is not shipped as an app resource.

`src/Silen.App/Assets/Brand/silen-bloom.png` is a transparent 1534×1025 px
restoration of the supplied cropped reference, made with the built-in image
tool. Its alpha is retained; the dark membrane interiors belong to the mascot.
The exact editing prompt is recorded in `bloom-restoration-prompt.txt`.
The runtime uses the restored artwork on a textured WPF membrane instead of
approximating its silhouette with a different 3D sculpture. At the user's
request, the sides slowly fold and open like butterfly wings every four seconds,
with gentle hovering across an eight-second flight loop. No runtime image
service or model is required.
