"""
Textured contact sheets: python tools/remodel/sheet_tx.py

One sheet per piece, 1800 px wide, one row per candidate: the shipping model, round 1 A to C,
round 2 A to C, each beside the vanilla chest (post sheet) and workbench with a 1 m cube.
Plus an overview with every piece beside the chest and workbench, shipping above, picks below.
"""

import json
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "renders", "lhm-69")
TITLES = {"post": "Stowing post", "rail": "Creel rail", "perch": "Spirit perch", "jib": "Hod jib"}
W = 1800
ROW_H = 525
BAR = 44


def font(size):
    for name in ("arialbd.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(os.path.join("C:/Windows/Fonts", name), size)
        except OSError:
            pass
    return ImageFont.load_default()


def row_label(draw, y, text, info):
    draw.rectangle((0, y, W, y + BAR), fill=(22, 24, 22))
    draw.text((12, y + 7), text, font=font(26), fill=(255, 224, 140))
    if info:
        d = info["dim"]
        s = "%d tris   %d parts   %d open edges   %d%% of tris in parts under 4 cm   %.2f x %.2f x %.2f m" % (
            info["tris"], info["parts"], info["open"], round(info.get("thin", 0) * 100), d[0], d[1], d[2])
        draw.text((800, y + 12), s, font=font(20), fill=(225, 225, 225))


def piece_sheet(piece):
    r1 = json.load(open(os.path.join(OUT, "r1_results_%s.json" % piece)))
    r2 = json.load(open(os.path.join(OUT, "r2_results_%s.json" % piece)))
    rows = [("r1", "current", "SHIPPING (as the runtime skins it today)", r1["current"])]
    rows += [("r1", k, "ROUND 1  %s  (workbench skin)" % k.upper(), r1[k]) for k in "abc"]
    rows += [("r2", k, "ROUND 2  %s  (measured rules + hide, stones, straps)" % k.upper(), r2[k]) for k in "abc"]
    head = 60
    sheet = Image.new("RGB", (W, head + len(rows) * (ROW_H + BAR)), (14, 16, 14))
    d = ImageDraw.Draw(sheet)
    d.text((14, 12), "LHM-69  %s   textured with the game's own sheets, eye height 1.7 m, 42 mm, "
           "scale 1.0, 1 m cube" % TITLES[piece], font=font(30), fill=(255, 255, 255))
    for i, (rnd, k, text, info) in enumerate(rows):
        y = head + i * (ROW_H + BAR)
        row_label(d, y, text, None if k == "current" and False else info)
        im = Image.open(os.path.join(OUT, "%s_%s_tile_%s.png" % (rnd, piece, k)))
        sheet.paste(im.resize((W, ROW_H)), (0, y + BAR))
    path = os.path.join(OUT, "%s_sheet_textured.png" % piece)
    sheet.save(path)
    print(path, sheet.size)


def overview():
    a = Image.open(os.path.join(OUT, "overview_shipping.png"))
    b = Image.open(os.path.join(OUT, "overview_picks.png"))
    w = 2600
    sheet = Image.new("RGB", (w, 60 + 2 * (640 + BAR)), (14, 16, 14))
    d = ImageDraw.Draw(sheet)
    d.text((14, 12), "LHM-69  every piece beside the vanilla chest and workbench (post, rail, perch, jib "
           "left to right)", font=font(32), fill=(255, 255, 255))
    for i, (im, text) in enumerate(((a, "SHIPPING, as the runtime skins it today"),
                                    (b, "ROUND 2 PICKS (post B, rail A, perch C, jib C) with bench islands: planks, straps, stones, hide"))):
        y = 60 + i * (640 + BAR)
        d.rectangle((0, y, w, y + BAR), fill=(22, 24, 22))
        d.text((12, y + 7), text, font=font(28), fill=(255, 224, 140))
        sheet.paste(im, (0, y + BAR))
    path = os.path.join(OUT, "overview_sheet.png")
    sheet.save(path)
    print(path, sheet.size)


for p in ("post", "rail", "perch", "jib"):
    piece_sheet(p)
overview()
