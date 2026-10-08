// Direct two-frame diff, whole frame and central object box.
// `sharp` lives in the harness profile's node_modules here; point ROOM_PET_SHARP at it
// (or install sharp normally) instead of hard-coding a machine path.
const sharp = require(process.env.ROOM_PET_SHARP || 'sharp');

function hue(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  if (d < 1e-6) return -1;
  let h;
  if (mx === r) h = ((g - b) / d) % 6;
  else if (mx === g) h = (b - r) / d + 2;
  else h = (r - g) / d + 4;
  h *= 60;
  return h < 0 ? h + 360 : h;
}

(async () => {
  const [fa, fb] = process.argv.slice(2);
  const A = await sharp(fa).raw().toBuffer({ resolveWithObject: true });
  const B = await sharp(fb).raw().toBuffer({ resolveWithObject: true });
  const { width: W, height: H, channels: ch } = A.info;
  if (W !== B.info.width || H !== B.info.height) { console.log('size mismatch'); process.exit(1); }

  function diff(x0, x1, y0, y1, label) {
    let n = 0, big = 0, sum = 0, hueMoved = 0, hueN = 0, maxd = 0;
    for (let y = y0; y < y1; y++) {
      for (let x = x0; x < x1; x++) {
        const i = (y * W + x) * ch;
        const d = Math.abs(A.data[i] - B.data[i]) + Math.abs(A.data[i + 1] - B.data[i + 1]) + Math.abs(A.data[i + 2] - B.data[i + 2]);
        n++; sum += d; if (d > maxd) maxd = d;
        if (d > 30) big++;
        const ha = hue(A.data[i], A.data[i + 1], A.data[i + 2]);
        const hb = hue(B.data[i], B.data[i + 1], B.data[i + 2]);
        if (ha >= 0 && hb >= 0) { hueN++; if (Math.abs(ha - hb) > 10) hueMoved++; }
      }
    }
    console.log(`[${label}] pixels=${n}`);
    console.log(`   mean |dRGB|=${(sum / n).toFixed(2)}  max=${maxd}`);
    console.log(`   changed >30 : ${big} (${(100 * big / n).toFixed(1)}%)`);
    console.log(`   hue moved >10deg : ${hueMoved}/${hueN} (${(100 * hueMoved / Math.max(1, hueN)).toFixed(1)}% of coloured)`);
  }

  diff(0, W, 0, H, 'whole frame');
  diff(Math.floor(W * 0.3), Math.floor(W * 0.7), Math.floor(H * 0.35), Math.floor(H * 0.75), 'centre object box');
})();
