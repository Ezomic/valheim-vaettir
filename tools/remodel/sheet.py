"""
Contact sheets for LHM-69, one PNG per piece.

    python tools/remodel/sheet.py

Plain Python with Pillow, not Blender: it only lays out tiles the Blender scripts already
rendered. 2000px wide so each wide tile is shown at its full 1000px; the four close-ups
sit in a row underneath. Reads renders/lhm-69/results_<piece>.json.
"""

import json
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "renders", "lhm-69")
TITLES = {"post": "Stowing post", "rail": "Creel rail (upgrade)",
          "perch": "Spirit perch (upgrade)", "jib": "Hod jib (upgrade)"}
ORDER = ["current", "a", "b", "c"]


def font(size):
    for name in ("arialbd.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(os.path.join("C:\\Windows\\Fonts", name), size)
        except OSError:
            pass
    return ImageFont.load_default()


def label(draw, x, y, w, key, info):
    text = "CURRENT (shipping)" if key == "current" else key.upper()
    draw.rectangle((x, y, x + w, y + 44), fill=(24, 26, 24))
    draw.text((x + 12, y + 7), text, font=font(28), fill=(255, 224, 140))
    d = info["dim"]
    stats = "%d tris   %d parts   %d open edges   %.2f x %.2f x %.2f m" % (
        info["tris"], info["parts"], info["open"], d[0], d[1], d[2])
    draw.text((x + 300 if key != "current" else x + 330, y + 12), stats, font=font(20),
              fill=(225, 225, 225))


def build(piece):
    with open(os.path.join(OUT, "results_%s.json" % piece)) as fh:
        res = json.load(fh)

    head, tile_w, tile_h, bar = 64, 1000, 440, 44
    cw, ch = 500, 421
    height = head + 2 * (tile_h + bar) + (ch + bar) + 8
    sheet = Image.new("RGB", (2000, height), (14, 16, 14))
    draw = ImageDraw.Draw(sheet)
    draw.text((16, 12), "LHM-69  %s   eye height 1.7 m, 42 mm, runtime scale 1.0, 1 m cube"
              % TITLES[piece], font=font(32), fill=(255, 255, 255))

    for i, key in enumerate(ORDER):
        x, y = (i % 2) * tile_w, head + (i // 2) * (tile_h + bar)
        label(draw, x, y, tile_w, key, res[key])
        wide = Image.open(os.path.join(OUT, "%s_tile_%s.png" % (piece, key)))
        sheet.paste(wide.resize((tile_w, tile_h)), (x, y + bar))

    y = head + 2 * (tile_h + bar)
    for i, key in enumerate(ORDER):
        x = i * cw
        draw.rectangle((x, y, x + cw, y + bar), fill=(24, 26, 24))
        draw.text((x + 12, y + 8), ("CURRENT" if key == "current" else key.upper())
                  + "  close, %.1f m back" % res[key]["close_dist"], font=font(24),
                  fill=(255, 224, 140))
        close = Image.open(os.path.join(OUT, "%s_close_%s.png" % (piece, key)))
        sheet.paste(close.resize((cw, ch)), (x, y + bar))

    path = os.path.join(OUT, "%s_sheet.png" % piece)
    sheet.save(path)
    print(path, sheet.size)


for p in ("post", "rail", "perch", "jib"):
    build(p)
