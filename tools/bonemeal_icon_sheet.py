"""
The comparison sheet: the six vanilla icons, then the old bonemeal icon, then the
variants, once at 64 px (what the game shows) and once enlarged.

    python tools/bonemeal_icon_sheet.py renders/lhm-70
"""

import os
import sys

from PIL import Image, ImageDraw

VANILLA = "E:/Repositories/valheim/own-profile/BepInEx/rips/icons/"
NAMES = ["piece_workbench", "piece_chest_wood", "charcoal_kiln", "smelter", "piece_cauldron", "piece_banner01"]
VARIANTS = ["subtle", "medium", "strong"]


def small(im):
    return im.convert("RGBa").resize((64, 64), Image.LANCZOS).convert("RGBA")


def row(items, scale, bg=(58, 58, 58, 255)):
    pad = 8
    cell = 64 * scale
    sheet = Image.new("RGBA", (len(items) * (cell + pad) + pad, cell + 2 * pad), bg)
    for k, im in enumerate(items):
        sheet.alpha_composite(im.resize((cell, cell), Image.NEAREST), (pad + k * (cell + pad), pad))
    return sheet


def main():
    folder = sys.argv[1]
    old = Image.open(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "grove_bonemeal_icon.png")).convert("RGBA")
    vanilla = [Image.open(VANILLA + n + ".png").convert("RGBA") for n in NAMES]
    new = [Image.open(os.path.join(folder, "bonemeal_icon_%s.png" % v)).convert("RGBA") for v in VARIANTS]
    items = vanilla + [small(old)] + [small(i) for i in new]
    one = row(items, 1)
    big = row(items, 4)
    rows = [one.resize((one.width * 2, one.height * 2), Image.NEAREST), big]
    width = max(r.width for r in rows)
    out = Image.new("RGBA", (width, sum(r.height for r in rows)), (58, 58, 58, 255))
    y = 0
    for r in rows:
        out.alpha_composite(r, (0, y))
        y += r.height
    out.save(os.path.join(folder, "comparison_sheet.png"))


main()
