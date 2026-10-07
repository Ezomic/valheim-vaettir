"""
Reduces the 1024 px Cycles render to the 128 px icon, premultiplied on the way down so the
transparent edge does not pull a dark or pale fringe in, with a touch of blur first because
the vanilla icons are soft. Same reduction as tools/bonemeal_icon_post.py on LHM-70.

    python tools/jib/jib_icon_post.py renders/lhm-77c assets/hod_jib_icon.png
"""

import os
import sys

from PIL import Image, ImageFilter


def main():
    folder, target = sys.argv[1], sys.argv[2]
    im = Image.open(os.path.join(folder, "raw_jib_icon.png")).convert("RGBa")
    im = im.filter(ImageFilter.GaussianBlur(1.6))
    im = im.resize((128, 128), Image.LANCZOS).convert("RGBA")
    im.save(target)
    im.resize((512, 512), Image.NEAREST).save(os.path.join(folder, "hod_jib_icon_4x.png"))


main()
