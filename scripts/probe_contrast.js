// Luminance / hue of the cube versus the sky behind it, to confirm the cube still
// stands out against the new skybox. Also reports the sky region's hue spread.
// `sharp` lives in the harness profile's node_modules here; point ROOM_PET_SHARP at it
// (or install sharp normally) instead of hard-coding a machine path.
const sharp = require(process.env.ROOM_PET_SHARP || 'sharp');

function hsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  let h = 0;
  if (d > 1e-6) {
    if (mx === r) h = ((g - b) / d) % 6;
    else if (mx === g) h = (b - r) / d + 2;
    else h = (r - g) / d + 4;
    h *= 60; if (h < 0) h += 360;
  }
  return { h, s: mx <= 0 ? 0 : d / mx, v: mx };
}

async function load(f) {
  const { data, info } = await sharp(f).raw().toBuffer({ resolveWithObject: true });
  return { data, W: info.width, H: info.height, ch: info.channels };
}

function stats(img, xa, xb, ya, yb) {
  const { data, W, ch } = img;
  const x0 = Math.floor(W * xa), x1 = Math.floor(W * xb);
  const y0 = Math.floor(img.H * ya), y1 = Math.floor(img.H * yb);
  const buckets = new Array(12).fill(0);
  let n = 0, lum = 0, sat = 0, coloured = 0;
  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const i = (y * W + x) * ch;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      n++; lum += 0.299 * r + 0.587 * g + 0.114 * b;
      const c = hsv(r, g, b);
      sat += c.s;
      if (c.v > 0.1 && c.s > 0.15) { coloured++; buckets[Math.min(11, Math.floor(c.h / 30))]++; }
    }
  }
  const occupied = buckets.filter((v) => v > coloured * 0.03).length;
  return { n, lum: lum / n, sat: sat / n, buckets, coloured, occupied };
}

(async () => {
  for (const f of process.argv.slice(2)) {
    const img = await load(f);
    const cube = stats(img, 0.34, 0.66, 0.40, 0.68);   // centre box = the cube
    const sky = stats(img, 0.10, 0.90, 0.0, 0.22);     // top band = sky
    console.log(`--- ${f.split(/[\\/]/).pop()} ---`);
    console.log(`  cube box : luminance=${cube.lum.toFixed(1)}/255  saturation=${cube.sat.toFixed(2)}  hue buckets=${cube.occupied}/12`);
    console.log(`  sky band : luminance=${sky.lum.toFixed(1)}/255  saturation=${sky.sat.toFixed(2)}  hue buckets=${sky.occupied}/12`);
    console.log(`  cube-vs-sky luminance ratio = ${(cube.lum / sky.lum).toFixed(2)}x`);
  }
})();
