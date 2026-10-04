"""
The design-round sheet: six vanilla icons, the old transplant icon and concepts A to E,
at 64 px (what the game shows), at 3x, and with a line of text per concept.

    python tools/transplant_concepts_sheet.py renders/lhm-72b renders/lhm-72/thicket_transplant_old.png
"""

import os
import sys

from PIL import Image, ImageDraw, ImageFont

VANILLA = "E:/Repositories/valheim/own-profile/BepInEx/rips/icons/"
NAMES = ["piece_workbench", "piece_chest_wood", "charcoal_kiln", "smelter", "piece_cauldron", "piece_banner01"]
TAGS = "ABCDE"
BG = (58, 58, 58, 255)
TEXT = [
    "A  Root ball wrapped in sackcloth, cord-tied at the neck, leaves fanning out of the knot.",
    "B  A spade lifting a clod of earth with a sprout growing out of it.",
    "C  A sprout in a carved wooden bucket with iron bands.",
    "D  A bundle of roots tied with cord, a crown of leaves on top.",
    "E  A turf block with stones in its face and a berry bush standing on it.",
    "Pick: B. It is the only one that says the plant is being moved (dug and lifted) rather than potted, the diagonal",
    "spade is a silhouette no vanilla icon has, and it has three clear value steps (leaf, earth, metal and wood).",
    "Safe second: C, the strongest single object at 64 px, but it reads as a potted plant, not a transplant.",
]


def small(im):
    return im.convert("RGBa").resize((64, 64), Image.LANCZOS).convert("RGBA")


def font(size):
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def row(items, labels, scale, gap_after=6):
    pad, cell = 10, 64 * scale
    label_h = 26
    w = len(items) * (cell + pad) + pad + 24
    sheet = Image.new("RGBA", (w, cell + 2 * pad + label_h), BG)
    d = ImageDraw.Draw(sheet)
    x = pad
    for k, (im, lab) in enumerate(zip(items, labels)):
        if k == gap_after:
            x += 24
        sheet.alpha_composite(im.resize((cell, cell), Image.NEAREST), (x, pad))
        d.text((x + 2, pad + cell + 4), lab, fill=(230, 230, 230, 255), font=font(16))
        x += cell + pad
    return sheet


def main():
    folder, old_path = sys.argv[1], sys.argv[2]
    vanilla = [small(Image.open(VANILLA + n + ".png").convert("RGBA")) for n in NAMES]
    old = small(Image.open(old_path).convert("RGBA"))
    new = [small(Image.open(os.path.join(folder, "concept_%s.png" % t)).convert("RGBA")) for t in TAGS]
    labels = [n.replace("piece_", "") for n in NAMES] + ["old"] + list(TAGS)
    items = vanilla + [old] + new
    parts = [row(items, labels, 2), row(items, labels, 3)]
    f = font(20)
    note_h = 30 + 30 * len(TEXT)
    width = max(p.width for p in parts)
    out = Image.new("RGBA", (width, sum(p.height for p in parts) + note_h), BG)
    y = 0
    for p in parts:
        out.alpha_composite(p, (0, y))
        y += p.height
    d = ImageDraw.Draw(out)
    y += 14
    for line in TEXT:
        d.text((14, y), line, fill=(235, 235, 235, 255), font=f)
        y += 30
    out.save(os.path.join(folder, "comparison_sheet.png"))


main()
