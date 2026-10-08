// Check an animated rainbow glow: hue diversity among the object pixels, and how
// much the frame changed between two captures taken at different times.
const sharp = require('C:/Users/yanhong.liu/.dsh/profiles/node_modules/sharp');

function rgb2hue(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  if (d < 1e-6) return -1; // grey
  let h;
  if (mx === r) h = ((g - b) / d) % 6;
  else if (mx === g) h = (b - r) / d + 2;
  else h = (r - g) / d + 4;
  h *= 60;
  return h < 0 ? h + 360 : h;
}

async function load(file) {
  const { data, info } = await sharp(file).raw().toBuffer({ resolveWithObject: true });
  return { data, W: info.width, H: info.height, ch: info.channels };
}

function hueStats(img) {
  const { data, W, H, ch } = img;
  const buckets = new Array(12).fill(0);
  let saturated = 0, total = 0;
  for (let y = Math.floor(H * 0.15); y < H * 0.9; y++) {
    for (let x = Math.floor(W * 0.15); x < W * 0.85; x++) {
      const i = (y * W + x) * ch;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      const mx = Math.max(r, g, b), mn = Math.min(r, g, b);
      total++;
      // Only strongly coloured pixels count as "the glow".
      if (mx < 50 || (mx - mn) / mx < 0.25) continue;
      saturated++;
      const h = rgb2hue(r, g, b);
      if (h >= 0) buckets[Math.min(11, Math.floor(h / 30))]++;
    }
  }
  return { buckets, saturated, total };
}

(async () => {
  const [a, b] = process.argv.slice(2);
  if (!a || !b) { console.error('usage: node probe_rainbow.js <pngA> <pngB>'); process.exit(2); }

  const A = await load(a), B = await load(b);

  console.log('=== hue histogram over strongly-coloured pixels (30 deg buckets) ===');
  console.log('  bucket :   0-30  30-60 60-90 90-120 120-150 150-180 180-210 210-240 240-270 270-300 300-330 330-360');
  for (const [name, img] of [['A', A], ['B', B]]) {
    const s = hueStats(img);
    const pct = s.buckets.map((v) => String(Math.round(100 * v / Math.max(1, s.saturated))).padStart(5));
    console.log(`  ${name} %   : ${pct.join(' ')}`);
    const occupied = s.buckets.filter((v) => v > s.saturated * 0.02).length;
    console.log(`  ${name}: ${s.saturated} coloured px (${(100 * s.saturated / s.total).toFixed(1)}% of sample), ${occupied}/12 hue buckets populated`);
  }

  // Frame-to-frame difference proves the glow is animated.
  if (A.W !== B.W || A.H !== B.H) { console.log(`size mismatch ${A.W}x${A.H} vs ${B.W}x${B.H}`); process.exit(1); }
  let changed = 0, changedHue = 0, n = 0;
  for (let p = 0; p < A.W * A.H; p++) {
    const i = p * A.ch;
    const dr = Math.abs(A.data[i] - B.data[i]);
    const dg = Math.abs(A.data[i + 1] - B.data[i + 1]);
    const db = Math.abs(A.data[i + 2] - B.data[i + 2]);
    n++;
    if (dr + dg + db > 30) changed++;
    const ha = rgb2hue(A.data[i], A.data[i + 1], A.data[i + 2]);
    const hb = rgb2hue(B.data[i], B.data[i + 1], B.data[i + 2]);
    if (ha >= 0 && hb >= 0 && Math.abs(ha - hb) > 10) changedHue++;
  }
  console.log('\n=== animation between the two captures ===');
  console.log(`  pixels with |dRGB| > 30 : ${changed} (${(100 * changed / n).toFixed(1)}%)`);
  console.log(`  pixels whose hue moved >10 deg : ${changedHue} (${(100 * changedHue / n).toFixed(1)}%)`);
})();
