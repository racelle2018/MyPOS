import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { Resvg } from '@resvg/resvg-js';

const assets = new URL('../../src/RetailForma.Desktop/Assets/Branding/', import.meta.url);
const font = fileURLToPath(new URL('../../src/RetailForma.Desktop/Assets/Fonts/Inter-Bold.ttf', import.meta.url));
const logo = readFileSync(new URL('retailforma-logo.svg', assets));
const icon = readFileSync(new URL('retailforma-icon.svg', assets));
function render(svg, width) {
  const renderer = new Resvg(svg, {
    fitTo: { mode: 'width', value: width },
    font: { fontFiles: [font], loadSystemFonts: false, defaultFontFamily: 'Inter' }
  });
  return renderer.render().asPng();
}
writeFileSync(new URL('retailforma-logo.png', assets), render(logo, 4096));
writeFileSync(new URL('retailforma-icon.png', assets), render(icon, 1024));

// Render each frame from vector geometry, not from a downscaled raster master.
const sizes = [16, 24, 32, 48, 64, 128, 256];
const frames = sizes.map(size => render(icon, size));
const header = Buffer.alloc(6 + 16 * frames.length);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(frames.length, 4);
let offset = header.length;
frames.forEach((frame, index) => {
  const entry = 6 + index * 16;
  header[entry] = header[entry + 1] = sizes[index] === 256 ? 0 : sizes[index];
  header.writeUInt16LE(1, entry + 4);
  header.writeUInt16LE(32, entry + 6);
  header.writeUInt32LE(frame.length, entry + 8);
  header.writeUInt32LE(offset, entry + 12);
  offset += frame.length;
});
writeFileSync(new URL('retailforma.ico', assets), Buffer.concat([header, ...frames]));
console.log('Exported transparent logo 4096x800, icon 1024x1024 and 7 vector-rendered ICO frames (16-256 px).');
