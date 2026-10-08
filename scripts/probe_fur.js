// Focused check of the fur render: work only on the object in the centre of the
// frame, and report the colour spread plus how ragged the silhouette is.
// `sharp` lives in the harness profile's node_modules here; point ROOM_PET_SHARP at it
// (or install sharp normally) instead of hard-coding a machine path.
const sharp = require(process.env.ROOM_PET_SHARP || 'sharp');

const file = process.argv[2];
if (!file) { console.error('usage: node probe_fur.js <png>'); process.exit(2); }

(async () => {
  const { data, info } = await sharp(file).raw().toBuffer({ resolveWithObject: true });
  const { width: W, height: H, channels: ch } = info;
  const px = (x, y) => {
    const i = (y * W + x) * ch;
    return [data[i], data[i + 1], data[i + 2]];
  };

  // "Object" = warm pixels (r >= g >= b with a real warm delta) inside the central
  // 70% box, which excludes the sky and the ground plane.
  const x0 = W * 0.15, x1 = W * 0.85, y0 = H * 0.15, y1 = H * 0.9;
  let n = 0, sr = 0, sg = 0, sb = 0, lum = [], magenta = 0;
  const rows = [];

  for (let y = Math.floor(y0); y < Math.floor(y1); y++) {
    let minX = -1, maxX = -1;
    for (let x = Math.floor(x0); x < Math.floor(x1); x++) {
      const [r, g, b] = px(x, y);
      if (r > 230 && g < 40 && b > 230) magenta++;
      const warm = r >= g && g >= b && r - b > 18;
      if (!warm) continue;
      n++; sr += r; sg += g; sb += b;
      lum.push(0.299 * r + 0.587 * g + 0.114 * b);
      if (minX < 0) minX = x;
      maxX = x;
    }
    if (minX >= 0) rows.push({ y, minX, maxX, w: maxX - minX + 1 });
  }

  if (n === 0) { console.log('no warm object pixels found'); process.exit(1); }

  const mean = (a) => a.reduce((s, v) => s + v, 0) / a.length;
  const sd = (a) => { const m = mean(a); return Math.sqrt(mean(a.map((v) => (v - m) ** 2))); };
  lum.sort((a, b) => a - b);
  const q = (p) => lum[Math.min(lum.length - 1, Math.floor(p * lum.length))];

  console.log(`image ${W}x${H}`);
  console.log(`magenta pixels: ${magenta}`);
  console.log(`warm object pixels: ${n} (${(100 * n / (W * H)).toFixed(1)}% of frame)`);
  console.log(`mean RGB: (${(sr / n).toFixed(0)}, ${(sg / n).toFixed(0)}, ${(sb / n).toFixed(0)})`);
  console.log(`luminance p10=${q(0.1).toFixed(0)} p50=${q(0.5).toFixed(0)} p90=${q(0.9).toFixed(0)} (0-255)`);
  console.log(`luminance spread p90-p10 = ${(q(0.9) - q(0.1)).toFixed(0)}`);

  const band = rows.filter((r) => r.y > rows[0].y + (rows[rows.length - 1].y - rows[0].y) * 0.2
    && r.y < rows[0].y + (rows[rows.length - 1].y - rows[0].y) * 0.8);
  console.log(`silhouette rows: ${rows.length}, mid-band rows: ${band.length}`);
  console.log(`  width  mean=${mean(band.map((r) => r.w)).toFixed(1)} sd=${sd(band.map((r) => r.w)).toFixed(1)}`);
  console.log(`  left   mean=${mean(band.map((r) => r.minX)).toFixed(1)} sd=${sd(band.map((r) => r.minX)).toFixed(1)}`);
  console.log(`  right  mean=${mean(band.map((r) => r.maxX)).toFixed(1)} sd=${sd(band.map((r) => r.maxX)).toFixed(1)}`);
})();
