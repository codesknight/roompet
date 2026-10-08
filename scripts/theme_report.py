#!/usr/bin/env python
"""Classify a screenshot of the forest runner by its themed palette.

Pure-stdlib PNG reader (no Pillow) so it runs in the project venv as-is.
"""
import struct
import sys
import zlib


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a png"
    pos, idat, w, h, ct = 8, b"", 0, 0, 0
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, _bd, ct = struct.unpack(">IIBB", chunk[:10])
        elif typ == b"IDAT":
            idat += chunk
        elif typ == b"IEND":
            break
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = {0: 1, 2: 3, 4: 2, 6: 4}[ct]
    stride = w * ch
    out = bytearray()
    prev = bytearray(stride)
    i = 0
    for _ in range(h):
        ft = raw[i]
        i += 1
        line = bytearray(raw[i:i + stride])
        i += stride
        if ft == 1:
            for x in range(ch, stride):
                line[x] = (line[x] + line[x - ch]) & 255
        elif ft == 2:
            for x in range(stride):
                line[x] = (line[x] + prev[x]) & 255
        elif ft == 3:
            for x in range(stride):
                a = line[x - ch] if x >= ch else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 255
        elif ft == 4:
            for x in range(stride):
                a = line[x - ch] if x >= ch else 0
                b = prev[x]
                c = prev[x - ch] if x >= ch else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 255
        out += line
        prev = line
    return w, h, ch, out


def main(path):
    w, h, ch, px = read_png(path)
    print(f"image {w}x{h}")

    def at(x, y):
        o = (y * w + x) * ch
        return px[o], px[o + 1], px[o + 2]

    tests = {
        "grass green":   lambda r, g, b: g > 120 and g > r + 30 and g >= b - 20,
        "dirt path":     lambda r, g, b: r > 140 and 70 < g < 200 and b < 130 and r > g + 40,
        "wood / bark":   lambda r, g, b: r > 120 and 60 < g < 170 and b < 110 and r > b + 60,
        "fruit red":     lambda r, g, b: r > 170 and g < 120 and b < 120,
        "fruit orange":  lambda r, g, b: r > 200 and 90 < g < 170 and b < 90,
        "sky blue":      lambda r, g, b: b > 150 and b > r + 30 and g > r,
        "dark shadow":   lambda r, g, b: r < 60 and g < 60 and b < 60,
    }
    counts = {k: 0 for k in tests}
    total = 0
    for y in range(0, h, 3):
        for x in range(0, w, 3):
            r, g, b = at(x, y)
            total += 1
            for name, test in tests.items():
                if test(r, g, b):
                    counts[name] += 1

    print(f"sampled {total} pixels")
    for name, n in sorted(counts.items(), key=lambda kv: -kv[1]):
        print(f"  {name:14s} {n:7d}  {100.0 * n / total:5.2f}%")

    # Centre column sample: what the player is actually looking down.
    print("centre column (top to bottom):")
    for frac in (0.08, 0.25, 0.45, 0.65, 0.85):
        y = int(h * frac)
        r, g, b = at(w // 2, y)
        print(f"  y={frac:4.2f}  rgb({r:3d},{g:3d},{b:3d})")


if __name__ == "__main__":
    main(sys.argv[1])
