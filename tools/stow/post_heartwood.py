"""
The stowing post with its heartwood showing.

    blender --background --python tools/post_heartwood.py

The post costs a heartwood and there is nowhere on it you can see one. That is a
small dishonesty in a mod that otherwise puts its state in the world - the rule is on
the hover text, the leftovers are in the post, the trips are a light crossing the
room - and it wastes the one ingredient that is actually interesting to look at.

It also answers a question the spirit currently begs. A light rises off the post every
time you close it and nothing on the post explains why. Put the heartwood in the piece
and the spirit has somewhere to come from.

All four keep the rack's silhouette, because that is the design already chosen and the
question here is narrower: *where does the heartwood go*. They differ in where the
light sits and therefore in what the post reads as - a machine with a power source, a
hearth, a tree with sap in it, or a stand holding something precious.

The heartwood is its own material group, "core", which the runtime skins with the same
borrowed glow the carrier wears. So the piece is four groups now rather than three,
and the lump is genuinely the same substance as the spirit that comes off it.

Modelled rather than imported from Vaettir's grove_heartwood.obj, for the reason the
carrier meshes are copied: Vaettir is the mod that knows about Stow, and reaching into
a sibling repo would reverse that. It is matched to its size - about 27cm - not to its
geometry.
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# tools/ first: vhbuild.py is vendored here so this repo stands alone. The sibling
# vaettir checkout is kept as a fallback for a working tree that has both.
sys.path.insert(0, os.path.join(os.path.dirname(ROOT), "vaettir", "tools"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (bloom_setup, box, camera, clear_scene, collide, export, finish,
                     orb, reference_cube, render, stage_scene, taper, tint, write_col)

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# Carpentry, not forest. A bench is built by someone with a square, so the jitter is
# a third of what a growing thing gets - enough to kill the machined look, not enough
# to make the joiner look drunk.
WOOD_TILT = 1.2

GLOW = 1.75


def heartwood(at, size=1.0, tilt=3.0):
    """
    A faceted lump, about 27cm at full size - Vaettir's own heartwood measures
    x -0.151..0.124, y -0.130..0.122, z -0.117..0.118.

    Two nested spheroids like the spirit's heart, so the glow has somewhere to fall
    off to. A single orb reads as a painted ball however brightly it is lit.
    """
    orb(0.115 * size, at, "core", subdivisions=2, stretch=0.92, tilt=tilt)
    orb(0.074 * size, at, "core", subdivisions=2, stretch=1.25, tilt=tilt)


def rack(skip_top_rail=False, skip_plinth=False):
    """
    The chosen post, unchanged: a pigeonhole rack, tall and flat and wide.

    Two parts can be left out because two of the treatments below replace them, and
    a treatment that merely *adds* to a finished piece is a bolt-on rather than a
    design.
    """
    if not skip_plinth:
        box((1.10, 0.52, 0.14), (0.0, 0.0, 0.07), "stone", tilt=WOOD_TILT, hit=True)
        collide((0.0, 0.0, 0.07), (1.10, 0.52, 0.14))

    box((0.12, 0.46, 1.16), (-0.49, 0.0, 0.70), "wood", tilt=WOOD_TILT)
    box((0.12, 0.46, 1.16), (0.49, 0.0, 0.70), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.70), (1.10, 0.46, 1.16))

    # Back board on -y. The preview camera stands on +y, and six open pigeonholes
    # photographed from behind are indistinguishable from a crate.
    box((1.10, 0.08, 1.10), (0.0, -0.21, 0.68), "wood", tilt=WOOD_TILT)

    for z in (0.44, 0.86):
        box((0.98, 0.44, 0.07), (0.0, 0.02, z), "wood", tilt=WOOD_TILT)
    for x in (-0.17, 0.17):
        box((0.07, 0.42, 0.78), (x, 0.02, 0.68), "wood", tilt=WOOD_TILT)

    if not skip_top_rail:
        box((1.20, 0.56, 0.10), (0.0, 0.0, 1.28), "wood", tilt=WOOD_TILT, hit=True)
        collide((0.0, 0.0, 1.28), (1.20, 0.56, 0.10))

    box((1.16, 0.06, 0.05), (0.0, 0.24, 0.44), "iron", tilt=WOOD_TILT)
    box((1.16, 0.06, 0.05), (0.0, 0.24, 0.86), "iron", tilt=WOOD_TILT)
    box((1.14, 0.54, 0.06), (0.0, 0.0, 0.16), "iron", tilt=WOOD_TILT)


# --------------------------------------------------------------------------- designs

def crown():
    """
    Set into the top rail, in the open, directly above the pigeonholes.

    The spirit comes off the top of the post, so this is the one where you can see
    where it came from. Highest and most visible, which is also the risk: a glowing
    lump on top of a shelf is a finial, and a finial is decoration rather than a part.
    Two iron straps hold it down to argue otherwise.
    """
    rack(skip_top_rail=True)

    # The rail splits either side rather than running through, so the lump is set
    # *into* the piece rather than perched on a continuous surface.
    for x in (-0.375, 0.375):
        box((0.45, 0.56, 0.10), (x, 0.0, 1.28), "wood", tilt=WOOD_TILT)
        collide((x, 0.0, 1.28), (0.45, 0.56, 0.10))

    box((0.34, 0.50, 0.05), (0.0, 0.0, 1.245), "iron", tilt=WOOD_TILT)

    heartwood((0.0, 0.0, 1.305))

    # Slim, and set at the lump's waist rather than running its full depth. At 30cm
    # deep they photographed as a grey slab standing behind the heartwood, which read
    # as a box the lump was sitting in front of.
    for x in (-0.128, 0.128):
        box((0.028, 0.14, 0.17), (x, 0.0, 1.30), "iron", tilt=1.0)


def hearth():
    """
    Low in the plinth, glowing up through the compartments from underneath.

    Turns the post into a hearth: the light rakes up the inside faces of the
    pigeonholes and every shelf gets an edge. The quietest of the four when you are
    not using it, and the one whose light does most for the rest of the model.
    Risk: at knee height it is behind whatever is standing in front of the post.
    """
    rack(skip_plinth=True)

    # A stone kerb in two halves with the lump bedded between them, so the plinth
    # reads as built around it rather than as having it stuck to the front.
    for x in (-0.36, 0.36):
        box((0.38, 0.52, 0.16), (x, 0.0, 0.08), "stone", tilt=WOOD_TILT)
        collide((x, 0.0, 0.08), (0.38, 0.52, 0.16))

    box((0.36, 0.52, 0.06), (0.0, 0.0, 0.03), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.03), (0.36, 0.52, 0.06))

    heartwood((0.0, 0.03, 0.15), size=1.05)

    # A low iron bar across the mouth. Without it the lump looks like it fell out.
    box((0.40, 0.05, 0.045), (0.0, 0.25, 0.10), "iron", tilt=1.0)


def spine():
    """
    A seam of heartwood up the centre divider, between the two ranks of pigeonholes.

    The light comes out sideways into every compartment at once, so the post reads as
    a piece of timber with something still alive in it rather than as a machine with a
    lamp on. The only one where the heartwood is a *line* and not a lump, which makes
    it the most distinct silhouette of the four - and the least like a power source,
    for better or worse.
    """
    rack()

    # The middle divider is replaced by the seam, so the shelves meet it rather than
    # it being laid over them.
    for z in (0.60, 1.02):
        heartwood((0.0, 0.06, z), size=0.62, tilt=5.0)

    box((0.10, 0.30, 0.80), (0.0, 0.0, 0.68), "core", tilt=1.5)

    for x in (-0.085, 0.085):
        box((0.05, 0.42, 0.80), (x, 0.02, 0.68), "wood", tilt=WOOD_TILT)

    box((0.16, 0.06, 0.05), (0.0, 0.23, 0.30), "iron", tilt=1.0)
    box((0.16, 0.06, 0.05), (0.0, 0.23, 1.10), "iron", tilt=1.0)


def socket():
    """
    Held out on an iron bracket off the side, like a lantern.

    The only treatment where the heartwood is visibly a separate object that was put
    there - it could be taken down again, which is the truth of it: you spent one, and
    it is sitting in a cradle. Also the only one that lights the room rather than the
    post, because it stands clear of the timber.

    Risk: a bracket sticking out is the first thing to clip a wall, and this post is
    meant to go in a storage room with things on both sides of it.
    """
    rack()

    # Arm out of the right-hand upright, braced underneath. One rotated box is a
    # stick; a brace is what makes it read as carrying weight.
    box((0.22, 0.07, 0.06), (0.585, 0.0, 1.02), "iron", tilt=1.0)
    taper(0.030, 0.022, 0.24, (0.545, 0.0, 0.90), "iron", sides=7, rot_y=34.0, tilt=1.0)

    # An open cradle: three fingers, never a cup. A closed cup is a lid, and the
    # point is that the lump is visible from underneath as well.
    for i in range(3):
        angle = math.radians(120.0 * i + 30.0)
        box((0.035, 0.035, 0.16),
            (0.665 + math.cos(angle) * 0.085, math.sin(angle) * 0.085, 1.10),
            "iron", rot_x=math.degrees(math.sin(angle)) * 0.18,
            rot_y=-math.degrees(math.cos(angle)) * 0.18, tilt=1.0)

    heartwood((0.665, 0.0, 1.15), size=0.92)


DESIGNS = (
    ("stow_post_crown", crown),
    ("stow_post_hearth", hearth),
    ("stow_post_spine", spine),
    ("stow_post_socket", socket),
)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        build()
        obj = finish(name)

        # Into variants/ until one is picked. The csproj copies assets\*.obj one level
        # only, so a candidate cannot reach the plugin folder by accident.
        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tint(GLOW)
        stage_scene()
        reference_cube((-1.55, -0.55, 0.50))

        bloom_setup(size=7, threshold=0.62)
        camera((-1.75, 2.85, 1.70), (0.0, 0.0, 0.78), lens=45)
        render(os.path.join(PREVIEWS, name + ".png"), width=720, height=620, bloom=False)

        tris = len(obj.data.polygons)
        flag = "  OVER 10k" if tris > 10000 else ""
        print("DESIGN_OK %s verts=%d tris=%d%s"
              % (name, len(obj.data.vertices), tris, flag))

    lineup()


def lineup():
    """All four in a row, at the distance you stand at to use one."""
    clear_scene()

    spacing = 1.95
    for index, (name, build) in enumerate(DESIGNS):
        offset = (index - 1.5) * spacing

        before = set(bpy.data.objects)
        build()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    finish("lineup")
    tint(GLOW)
    stage_scene()
    bloom_setup(size=7, threshold=0.62)
    camera((0.0, 7.40, 1.70), (0.0, 0.0, 0.80), lens=32)
    render(os.path.join(PREVIEWS, "stow_post_heartwood_lineup.png"),
           width=1400, height=580, bloom=False)
    print("DESIGN_OK stow_post_heartwood_lineup")


# Guarded so the crown revisions can import rack() and heartwood() from here rather
# than keeping a second copy of the piece they are revisions of. Blender runs a
# --python script as __main__, so this still builds everything when run directly.
if __name__ == "__main__":
    main()
