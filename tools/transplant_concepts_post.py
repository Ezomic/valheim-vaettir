"""
Reduces the 1024 px concept renders to 128 px icons and a 4x nearest enlargement.

    python tools/transplant_concepts_post.py renders/lhm-72b A B C D E
"""

import os
import sys

from PIL import Image, ImageFilter


def main():
    folder = sys.argv[1]
    for tag in sys.argv[2:]:
        im = Image.open(os.path.join(folder, "raw_%s.png" % tag)).convert("RGBa")
        im = im.filter(ImageFilter.GaussianBlur(1.6)).resize((128, 128), Image.LANCZOS).convert("RGBA")
        im.save(os.path.join(folder, "concept_%s.png" % tag))
        im.resize((512, 512), Image.NEAREST).save(os.path.join(folder, "concept_%s_4x.png" % tag))


main()
