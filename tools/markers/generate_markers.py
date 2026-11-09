"""Generate the printable V1 marker cards and the copies Unity tracks at runtime.

Standard library only, deterministic per marker id, so re-running gives byte-identical files.

Writes:
  unity-child-app/Assets/_Project/Resources/Markers/<ID>.bytes   PNG bytes Vuforia builds targets from
  unity-child-app/Assets/_Project/Resources/Markers/catalog.json  ids and printed width
  docs/markers/<ID>.png                                           the same image, for printing

Usage (from the repo root):  python -I tools/markers/generate_markers.py
To add a marker, append its id to MARKERS, run again, and commit every changed file.
"""

import hashlib
import json
import random
import struct
import sys
import zlib
from pathlib import Path

MARKERS = ["MK_SINK", "MK_SOAP", "MK_RUB", "MK_TOWEL"]  # Must match TaskDefinition markerIds.
PRINTED_WIDTH_METERS = 0.18  # Print each card exactly 18 cm wide, or tracking scale is wrong.

WIDTH, HEIGHT = 1200, 900  # 4:3; Vuforia accepts up to 2048 px per side.
FRAME = 28
LABEL_HEIGHT = 150
PALETTE = [0, 0, 45, 95, 150, 205, 255]  # Black weighted twice for strong corners.

# 5x7 bitmap font for marker ids: A-Z, 0-9 and underscore.
FONT = {
    "A": "01110 10001 10001 11111 10001 10001 10001", "B": "11110 10001 10001 11110 10001 10001 11110",
    "C": "01110 10001 10000 10000 10000 10001 01110", "D": "11110 10001 10001 10001 10001 10001 11110",
    "E": "11111 10000 10000 11110 10000 10000 11111", "F": "11111 10000 10000 11110 10000 10000 10000",
    "G": "01110 10001 10000 10111 10001 10001 01111", "H": "10001 10001 10001 11111 10001 10001 10001",
    "I": "01110 00100 00100 00100 00100 00100 01110", "J": "00111 00010 00010 00010 00010 10010 01100",
    "K": "10001 10010 10100 11000 10100 10010 10001", "L": "10000 10000 10000 10000 10000 10000 11111",
    "M": "10001 11011 10101 10101 10001 10001 10001", "N": "10001 11001 10101 10011 10001 10001 10001",
    "O": "01110 10001 10001 10001 10001 10001 01110", "P": "11110 10001 10001 11110 10000 10000 10000",
    "Q": "01110 10001 10001 10001 10101 10010 01101", "R": "11110 10001 10001 11110 10100 10010 10001",
    "S": "01111 10000 10000 01110 00001 00001 11110", "T": "11111 00100 00100 00100 00100 00100 00100",
    "U": "10001 10001 10001 10001 10001 10001 01110", "V": "10001 10001 10001 10001 10001 01010 00100",
    "W": "10001 10001 10001 10101 10101 10101 01010", "X": "10001 10001 01010 00100 01010 10001 10001",
    "Y": "10001 10001 01010 00100 00100 00100 00100", "Z": "11111 00001 00010 00100 01000 10000 11111",
    "0": "01110 10001 10011 10101 11001 10001 01110", "1": "00100 01100 00100 00100 00100 00100 01110",
    "2": "01110 10001 00001 00010 00100 01000 11111", "3": "11110 00001 00001 01110 00001 00001 11110",
    "4": "00010 00110 01010 10010 11111 00010 00010", "5": "11111 10000 11110 00001 00001 10001 01110",
    "6": "00110 01000 10000 11110 10001 10001 01110", "7": "11111 00001 00010 00100 01000 01000 01000",
    "8": "01110 10001 10001 01110 10001 10001 01110", "9": "01110 10001 10001 01111 00001 00010 01100",
    "_": "00000 00000 00000 00000 00000 00000 11111",
}

REPO = Path(__file__).resolve().parents[2]
UNITY_DIR = REPO / "unity-child-app/Assets/_Project/Resources/Markers"
PRINT_DIR = REPO / "docs/markers"


class Canvas:
    def __init__(self, width, height, value=255):
        self.w, self.h = width, height
        self.px = bytearray([value]) * (width * height)

    def rect(self, x0, y0, x1, y1, v):
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, self.w), min(y1, self.h)
        for y in range(y0, y1):
            row = y * self.w
            self.px[row + x0:row + x1] = bytes([v]) * max(x1 - x0, 0)

    def circle(self, cx, cy, r, v):
        for y in range(max(cy - r, 0), min(cy + r + 1, self.h)):
            dx = int((r * r - (y - cy) ** 2) ** 0.5)
            self.rect(cx - dx, y, cx + dx + 1, y + 1, v)

    def triangle(self, pts, v):
        (ax, ay), (bx, by), (cx, cy) = pts
        def edge(px, py, qx, qy, x, y):
            return (qx - px) * (y - py) - (qy - py) * (x - px)
        area = edge(ax, ay, bx, by, cx, cy)
        if area == 0:
            return
        for y in range(max(min(ay, by, cy), 0), min(max(ay, by, cy) + 1, self.h)):
            for x in range(max(min(ax, bx, cx), 0), min(max(ax, bx, cx) + 1, self.w)):
                w0, w1, w2 = edge(bx, by, cx, cy, x, y), edge(cx, cy, ax, ay, x, y), edge(ax, ay, bx, by, x, y)
                if (w0 >= 0 and w1 >= 0 and w2 >= 0) or (w0 <= 0 and w1 <= 0 and w2 <= 0):
                    self.px[y * self.w + x] = v

    def text(self, label, x, y, scale, v):
        for ch in label:
            rows = FONT[ch].split()
            for ry, row in enumerate(rows):
                for rx, bit in enumerate(row):
                    if bit == "1":
                        self.rect(x + rx * scale, y + ry * scale, x + (rx + 1) * scale, y + (ry + 1) * scale, v)
            x += 6 * scale

    def png(self):
        raw = bytearray()
        for y in range(self.h):
            raw.append(0)  # filter: none
            for g in self.px[y * self.w:(y + 1) * self.w]:
                raw += bytes((g, g, g))

        def chunk(kind, data):
            body = kind + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

        header = struct.pack(">IIBBBBB", self.w, self.h, 8, 2, 0, 0, 0)  # 8-bit RGB
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")


def draw_marker(marker_id):
    rng = random.Random(int(hashlib.sha256(marker_id.encode()).hexdigest(), 16))
    c = Canvas(WIDTH, HEIGHT)
    art_bottom = HEIGHT - FRAME - LABEL_HEIGHT

    # Dense, non-repeating, high-contrast shapes give Vuforia many distinct corners to track.
    for _ in range(170):
        v = rng.choice(PALETTE)
        kind = rng.random()
        x, y = rng.randrange(FRAME, WIDTH - FRAME), rng.randrange(FRAME, art_bottom)
        if kind < 0.45:
            c.rect(x, y, min(x + rng.randrange(18, 170), WIDTH - FRAME), min(y + rng.randrange(18, 150), art_bottom), v)
        elif kind < 0.7:
            r = rng.randrange(10, 70)
            if FRAME + r <= x <= WIDTH - FRAME - r and FRAME + r <= y <= art_bottom - r:
                c.circle(x, y, r, v)
        else:
            pts = [(min(max(x + rng.randrange(-110, 110), FRAME), WIDTH - FRAME - 1),
                    min(max(y + rng.randrange(-110, 110), FRAME), art_bottom - 1)) for _ in range(3)]
            c.triangle(pts, v)

    # Frame plus a label strip, so adults can tell cards apart and the edge reads clearly.
    c.rect(0, 0, WIDTH, FRAME, 0)
    c.rect(0, HEIGHT - FRAME, WIDTH, HEIGHT, 0)
    c.rect(0, 0, FRAME, HEIGHT, 0)
    c.rect(WIDTH - FRAME, 0, WIDTH, HEIGHT, 0)
    c.rect(FRAME, art_bottom, WIDTH - FRAME, HEIGHT - FRAME, 255)
    c.rect(FRAME, art_bottom, WIDTH - FRAME, art_bottom + 8, 0)
    scale = min(16, (WIDTH - 2 * FRAME - 80) // (6 * len(marker_id)))
    text_w = 6 * scale * len(marker_id) - scale
    c.text(marker_id, (WIDTH - text_w) // 2, art_bottom + 8 + (LABEL_HEIGHT - 8 - 7 * scale) // 2, scale, 0)
    return c.png()


def guid(path):
    return hashlib.md5(("smartlife:" + path).encode()).hexdigest()


def write_meta(path, importer):
    rel = path.relative_to(REPO).as_posix()
    if importer == "folder":
        body = f"fileFormatVersion: 2\nguid: {guid(rel)}\nfolderAsset: yes\nDefaultImporter:\n"
    else:
        body = f"fileFormatVersion: 2\nguid: {guid(rel)}\nTextScriptImporter:\n"
    body += "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():  # Keep any GUID Unity already assigned.
        meta.write_text(body, encoding="utf-8", newline="\n")


def main():
    for marker_id in MARKERS:
        if any(ch not in FONT for ch in marker_id):
            sys.exit(f"Marker id '{marker_id}' may only use A-Z, 0-9 and _.")

    UNITY_DIR.mkdir(parents=True, exist_ok=True)
    PRINT_DIR.mkdir(parents=True, exist_ok=True)
    write_meta(UNITY_DIR, "folder")

    for marker_id in MARKERS:
        png = draw_marker(marker_id)
        (PRINT_DIR / f"{marker_id}.png").write_bytes(png)
        target = UNITY_DIR / f"{marker_id}.bytes"
        target.write_bytes(png)
        write_meta(target, "text")
        print(f"{marker_id}: {len(png) // 1024} KB")

    catalog = UNITY_DIR / "catalog.json"
    catalog.write_text(json.dumps({"printedWidthMeters": PRINTED_WIDTH_METERS, "markers": MARKERS}, indent=2) + "\n",
                       encoding="utf-8", newline="\n")
    write_meta(catalog, "text")


if __name__ == "__main__":
    main()
