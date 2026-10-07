"""
Dim blue-green night set: blender --background --python tools/jib/jib_night.py -- <before.obj> <after.obj> <out dir>
Renders low views (base from the front, mast, top cage) with the Workbench engine, then darkens and
tints them to the in-game night screenshot's look with Pillow (the Workbench engine has no light of
its own to dim, so the exposure is applied to the picture). Day versions are kept beside them.
Blender's Python has no Pillow, so the tint is a second step: python tools/jib/jib_night.py <out dir>
(plain Python) writes the *_night.png beside every day render.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
try:
    import jib_before_after as J   # noqa: E402
except ImportError:
    J = None

VIEWS = {
    "lowbase": ((0.1, -2.6, 0.45), (0.0, 0.0, 0.75), 45),
    "mast": ((-0.2, -2.9, 1.5), (0.0, 0.0, 1.15), 45),
    "cage": ((1.6, -1.9, 3.6), (-0.05, 0.0, 2.85), 50),
}


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    out = a[2]
    os.makedirs(out, exist_ok=True)
    for tag, path in (("before", a[0]), ("after", a[1])):
        J.reset()
        J.load(path)
        J.ground()
        for name, (loc, tgt, lens) in VIEWS.items():
            J.cam(loc, tgt, lens)
            f = os.path.join(out, "%s_%s.png" % (name, tag))
            J.shoot(f, 800, 800)
    print("JIBNIGHT done")


def tint(folder):
    from PIL import Image
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".png") or name.endswith("_night.png"):
            continue
        im = Image.open(os.path.join(folder, name)).convert("RGB")
        px = im.load()
        for y in range(im.height):
            for x in range(im.width):
                r, g, b = px[x, y]
                lum = (0.3 * r + 0.59 * g + 0.11 * b) * 0.20
                px[x, y] = (int(lum * 0.55 + 4), int(lum * 1.0 + 12), int(lum * 0.85 + 14))
        im.save(os.path.join(folder, name.replace(".png", "_night.png")))


if "bpy" in sys.modules:
    main()
else:
    tint(sys.argv[1])
