"""
Reduces the 1024 px Cycles renders to the 128 px icon, and writes the 4x
nearest-neighbour enlargement beside it.

    python tools/transplant_icon_post.py renders/lhm-72 subtle medium strong

Premultiplied on the way down, or the transparent edge pulls the background's
colour into a dark or pale fringe. A touch of blur first because the vanilla icons
are soft: they are rendered large and shrunk by the game's own filter.
"""

import os
import sys

from PIL import Image, ImageFilter


def reduce(path, out, size=128, blur=0.0):
    im = Image.open(path).convert("RGBa")
    if blur:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    im = im.resize((size, size), Image.LANCZOS)
    im = im.convert("RGBA")
    im.save(out)
    return im


def main():
    folder = sys.argv[1]
    for tag in sys.argv[2:]:
        raw = os.path.join(folder, "raw_%s.png" % tag)
        icon = reduce(raw, os.path.join(folder, "transplant_icon_%s.png" % tag), blur=1.6)
        icon.resize((512, 512), Image.NEAREST).save(os.path.join(folder, "transplant_icon_%s_4x.png" % tag))


main()
