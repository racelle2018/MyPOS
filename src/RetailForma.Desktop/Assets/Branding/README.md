# RetailForma RF branding

Owner selected concept 1 (RF Monogram) and approved application to the POS and License Generator on 2026-10-13.

- `retailforma-logo.png`: transparent horizontal RF / RetailForma wordmark.
- `retailforma-icon.png`: transparent square source with white RF on blue rounded square.
- `retailforma.ico`: alpha-preserving Windows icon with 16, 24, 32, 48, 64, 128 and 256 px frames.
- `retailforma-logo.svg`: clean vector reconstruction with separate named RF paths and an editable Inter wordmark.
- `retailforma-icon.svg`: editable vector tile and RF paths; no embedded bitmap.

The SVGs are manually reconstructed from the selected concept, not pixel-identical automatic traces. They remove raster edge noise and scale without pixelation. The wordmark uses Inter Bold; install the bundled Inter font for consistent editing. Owner requested replacement PNG/ICO exports from these SVGs. They are SVGs, not layered PSD files.

Initial concept PNGs were created through the built-in image-generation tool; originals remain under the Codex generated-images folder. Current application assets are rendered from the editable SVG masters using [resvg-js](https://github.com/thx/resvg-js) and the bundled Inter Bold font (no system-font dependency). The transparent logo is 4096x800 and icon PNG 1024x1024. Each ICO frame is rendered directly from the SVG, rather than resized from a bitmap.

Regenerate all current assets from the repository root:

```powershell
npm ci --prefix tools/branding
npm run export --prefix tools/branding
```

The older `tools/Export-BrandIcon.ps1` still packages a supplied PNG as an ICO; use the SVG exporter above for the sharpest repeatable exports.

## Generation prompts

Logo: Export the approved RetailForma RF logo from reference as one production transparent PNG. Preserve the distinctive blue RF monogram (open curved R left, separate F right) and exact RetailForma wordmark. One horizontal lockup: blue RF emblem left, charcoal RetailForma wordmark right. Flat #2878BD and #1F2937, crisp edges, no gradients/textures/shadows, genuine transparent alpha, no white backing. Remove preview captions and smaller icon. Sensible padding. Preserve approved identity; do not redesign.

Icon: Export only the approved app icon from the reference as one square production PNG. Preserve the white RF monogram on a #2878BD blue rounded square, curved open R left and F right. Small even padding and transparent alpha outside corners. No words, labels, background, extra symbols, gradients, shadows or texture. Crisp Windows application icon; do not redesign.

Source of truth: `branding-decisions.md`. Customer company name and report/receipt branding stay separate. Do not replace customer identities or change persisted data/licensing names. Original assets are raster PNG/ICO; editable vector reconstructions are provided separately. AI output is not a trademark-clearance guarantee.
