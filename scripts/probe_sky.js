// Compare the sky region before/after the skybox swap, and describe its palette.
// `sharp` lives in the harness profile's node_modules here; point ROOM_PET_SHARP at it
// (or install sharp normally) instead of hard-coding a machine path.
const sharp = require(process.env.ROOM_PET_SHARP || 'sharp');

async function load(f) {
  const { data, info } = await sharp(f).raw().toBuffer({ resolveWithObject: true });
  return { data, W: info.width, H: info.height, ch: info.channels };
}

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

// Sky band = top share of the frame (above the cube and the ground),
// excluding the left/right 10% where the horizon glow is strongest.
function skyStats(img, topShare) {
  const { data, W, H, ch } = img;
  const y1 = Math.floor(H * topShare);
  const buckets = new Array(12).fill(0);
  let n = 0, sr = 0, sg = 0, sb = 0, satSum = 0, vSum = 0, coloured = 0;
  for (let y = 0; y < y1; y++) {
    for (let x = Math.floor(W * 0.1); x < W * 0.9; x++) {
      const i = (y * W + x) * ch;
      const r = data[i], g = data[i + 1], b = data[i + 2];
      n++; sr += r; sg += g; sb += b;
      const { h, s, v } = hsv(r, g, b);
      satSum += s; vSum += v;
      if (v > 0.1 && s > 0.15) { coloured++; buckets[Math.min(11, Math.floor(h / 30))]++; }
    }
  }
  return { n, mean: [sr / n, sg / n, sb / n], buckets, coloured, sat: satSum / n, val: vSum / n };
}

function diffRegion(A, B, y1) {
  const { W, H, ch } = A;
  let sum = 0, n = 0, big = 0;
  for (let y = 0; y < y1; y++) {
    for (let x = 0; x < W; x++) {
      const i = (y * W + x) * ch;
      const d = Math.abs(A.data[i] - B.data[i]) + Math.abs(A.data[i + 1] - B.data[i + 1]) + Math.abs(A.data[i + 2] - B.data[i + 2]);
      sum += d; n++; if (d > 30) big++;
    }
  }
  return { mean: sum / n, pct: 100 * big / n };
}

(async () => {
  // 1 arg = wide sky-only shot; 3 args = before / after / wide.
  const args = process.argv.slice(2);
  const before = args.length === 3 ? args[0] : null;
  const after = args.length === 3 ? args[1] : null;
  const wide = args.length === 3 ? args[2] : (args.length === 1 ? args[0] : null);
  if (!before && !wide) {
    console.error('usage: node probe_sky.js <wide.png> | node probe_sky.js <before.png> <after.png> <wide.png>');
    process.exit(2);
  }

  if (before && after) {
    const A = await load(before), B = await load(after);
    console.log('=== sky region: default procedural skybox  ->  DreamySky ===');
    const sa = skyStats(A, 0.32), sb = skyStats(B, 0.32);
    console.log(`  before  mean RGB (${sa.mean.map((v) => v.toFixed(0)).join(', ')})  sat=${sa.sat.toFixed(2)} val=${sa.val.toFixed(2)}`);
    console.log(`  after   mean RGB (${sb.mean.map((v) => v.toFixed(0)).join(', ')})  sat=${sb.sat.toFixed(2)} val=${sb.val.toFixed(2)}`);
    const d = diffRegion(A, B, Math.floor(A.H * 0.32));
    console.log(`  changed: mean |dRGB|=${d.mean.toFixed(1)}, ${d.pct.toFixed(1)}% of sky pixels moved >30`);
    console.log(`  before hue buckets: ${sa.buckets.map((v, k) => (v > sa.coloured * 0.03 ? k * 30 + '' : '')).filter(Boolean).join(',') || '(flat)'}`);
    console.log(`  after  hue buckets: ${sb.buckets.map((v, k) => (v > sb.coloured * 0.03 ? k * 30 + '' : '')).filter(Boolean).join(',') || '(flat)'}`);
  }

  if (wide) {
    const W = await load(wide);
    const s = skyStats(W, 1.0);
    console.log('\n=== wide sky-only shot (whole frame) ===');
    console.log(`  mean RGB (${s.mean.map((v) => v.toFixed(0)).join(', ')})  saturation=${s.sat.toFixed(2)}  value=${s.val.toFixed(2)}`);
    console.log('  hue histogram (30 deg buckets, % of coloured px):');
    console.log('   ' + s.buckets.map((v, k) => `${k * 30}:${(100 * v / Math.max(1, s.coloured)).toFixed(0)}%`).join('  '));
    const occupied = s.buckets.filter((v) => v > s.coloured * 0.03).length;
    console.log(`  populated hue buckets: ${occupied}/12`);
  }
})();
