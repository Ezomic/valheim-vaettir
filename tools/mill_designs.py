"""
The bone mill: candidates for the thing that grinds bone into bonemeal.

    blender --background --python tools/mill_designs.py

Four grinders with deliberately unlike outlines, because a buildable piece is judged
from across a room at eye height rather than in a slot:

    quern   a low stone runner on a stone bed, turned by a wooden handle
    edge    a stone wheel standing on its rim in a ring track, the biggest silhouette
    trough  a long stone trough with a roller in it, the only horizontal one
    stamp   a frame of posts dropping stone heads into a mortar, the only tall frame

Two material groups, stone and wood, which is what vanilla pieces carry. Nothing here
paints anything: the names are group names, and at runtime each group is skinned with a
material lifted off a vanilla prefab, so the shapes are ours and the surfaces are the
game's. TINTS exist so a render says something about form and nothing more.

Rules this pass is written against, all of them paid for on earlier models:

  * every part overlaps its neighbour by a real margin. A 2mm gap is visible and reads
    as debris orbiting the object, which cost four passes on the bonemeal item.
  * few large parts. Heaps of small ones read as confetti and detach.
  * a hoop or band is one thin cylinder, not a ring of blocks. The staves sit inside it
    and only its outer wall is ever seen.
  * odd side counts, so a cylinder shows an edge to the camera and reads as round
    rather than as a box with the corners knocked off.

Nothing ships until one is picked: set WINNER and run again.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (box, camera, clear_scene, collide, disc, export, finish, orb,
                     reference_cube, render, ring, shell, stage_scene, taper, tint,
                     write_col)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "assets")
ASSETS = os.path.join(SHIPPED, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

WINNER = None
SHIPPED_MESH = "grove_mill"

STONE = "stone"
WOOD = "wood"


# --------------------------------------------------------------------------- shapes

def quern():
    """
    A hand quern: a stone runner sitting on a stone bed, turned by a wooden handle.

    The squat one. Its whole outline is a wide low cylinder with a stick out of the top,
    which is the least like the other three and the most obviously a mill.
    """
    # Bed, wide and slightly belled so it does not read as a drum.
    taper(0.62, 0.58, 0.30, (0.0, 0.0, 0.15), STONE, sides=13, spin=False)
    collide((0.0, 0.0, 0.15), (1.24, 1.24, 0.30))

    # The runner, overlapping the bed by a third of its height so the two are one stone
    # object with a seam rather than a lid resting on a tub.
    taper(0.56, 0.60, 0.20, (0.0, 0.0, 0.36), STONE, sides=13, spin=False)
    collide((0.0, 0.0, 0.36), (1.20, 1.20, 0.20))

    # The eye it is fed through, sunk in rather than sitting on.
    taper(0.16, 0.19, 0.09, (0.0, 0.0, 0.47), STONE, sides=11, spin=False)

    # Handle: a post through the runner, leaning, with a grip across the top. Rooted
    # 12cm into the stone so no amount of jitter lifts its foot clear.
    taper(0.050, 0.042, 0.62, (0.40, 0.10, 0.62), WOOD, sides=7, rot_x=9.0, rot_y=-7.0)
    box((0.30, 0.070, 0.070), (0.44, 0.12, 0.90), WOOD, rot_z=12.0)

    # Four feet, short and fat, tying it to the ground.
    for x, y in ((0.44, 0.30), (-0.44, 0.30), (0.44, -0.30), (-0.44, -0.30)):
        box((0.16, 0.16, 0.14), (x, y, 0.05), WOOD)


def edge():
    """
    An edge runner: a stone wheel standing on its rim, rolling round a ring track.

    The big one. A vertical disc most of a metre across is a silhouette nothing else in
    a base has, and it says grinding without a label.

    First pass failed on two things worth writing down. The track had a solid slab under
    it as thick as the ring, and the two merged with the wheel into a single grey dome,
    so the wheel stopped reading as a wheel. And the pivot post sat at the centre on the
    same line as the wheel, so it drew in front of it as a plank across the middle.

    The fix is what an edge runner actually is: the wheel rides the near side of the
    track, not the centre, and the post it sweeps around stands behind it.
    """
    # Track: a thin ring on a thin floor. Both flat enough to read as ground rather than
    # as a mass the wheel is buried in.
    ring(0.62, 0.070, (0.0, 0.0, 0.07), STONE, major=21, minor=7, rot_x=0.0)
    disc(0.64, 0.07, (0.0, 0.0, 0.035), STONE, sides=17, rot_x=0.0)
    collide((0.0, 0.0, 0.05), (1.36, 1.36, 0.10))

    # The wheel, on the near side of the track and standing in it: its foot at 0.09
    # against a track top of 0.105, so it sits in the groove rather than on the floor.
    taper(0.42, 0.42, 0.16, (0.0, -0.34, 0.51), STONE, sides=17, rot_x=90.0)
    collide((0.0, -0.34, 0.51), (0.84, 0.20, 0.84))

    # Axle from the wheel's hub back to the post, which is what makes it sweep a circle.
    taper(0.050, 0.050, 0.74, (0.0, -0.17, 0.51), WOOD, sides=7, rot_x=90.0)

    # The pivot post, behind the wheel so the wheel is the thing you see.
    taper(0.085, 0.065, 1.02, (0.0, 0.0, 0.51), WOOD, sides=7)

    # A cap and a brace arm, up out of the wheel's way.
    box((0.20, 0.20, 0.11), (0.0, 0.0, 1.02), WOOD)
    box((0.09, 0.52, 0.09), (0.0, -0.22, 0.94), WOOD, rot_x=18.0)


def trough():
    """
    A stone trough with a roller in it and a handle at one end.

    The only horizontal one, and the only one wider than it is deep, so in a row of
    pieces it is the long low shape.
    """
    # Trough: a floor and two walls rather than a hollowed box, because a full plate
    # across the top is a lid and there is nothing to hollow a box with here.
    box((1.30, 0.54, 0.14), (0.0, 0.0, 0.14), STONE)
    box((1.30, 0.11, 0.30), (0.0, 0.26, 0.26), STONE)
    box((1.30, 0.11, 0.30), (0.0, -0.26, 0.26), STONE)
    box((0.11, 0.54, 0.30), (0.62, 0.0, 0.26), STONE)
    box((0.11, 0.54, 0.30), (-0.62, 0.0, 0.26), STONE)
    collide((0.0, 0.0, 0.22), (1.40, 0.64, 0.44))

    # The roller, lying along the trough and overlapping both end walls.
    taper(0.21, 0.21, 1.10, (0.0, 0.0, 0.30), STONE, sides=15, rot_y=90.0)

    # Its axle, out through both ends, and a crank on one side.
    taper(0.045, 0.045, 1.54, (0.0, 0.0, 0.30), WOOD, sides=7, rot_y=90.0)
    box((0.07, 0.07, 0.34), (0.74, 0.0, 0.44), WOOD, rot_y=14.0)
    box((0.24, 0.070, 0.070), (0.80, 0.10, 0.58), WOOD, rot_z=8.0)

    # Legs under the trough, fat and short.
    for x in (0.50, -0.50):
        box((0.16, 0.44, 0.16), (x, 0.0, 0.06), WOOD)


def stamp():
    """
    A frame of posts dropping weighted stone heads into a mortar.

    The tall one, and the only one whose outline is mostly empty air between uprights,
    which is what makes it readable against a wall.
    """
    # Mortar block, low and heavy.
    taper(0.44, 0.40, 0.34, (0.0, 0.0, 0.17), STONE, sides=13, spin=False)
    collide((0.0, 0.0, 0.17), (0.88, 0.88, 0.34))

    # Two uprights and a crossbeam, each overlapping deeply where they meet.
    box((0.13, 0.13, 1.36), (0.46, 0.0, 0.68), WOOD)
    box((0.13, 0.13, 1.36), (-0.46, 0.0, 0.68), WOOD)
    box((1.10, 0.12, 0.14), (0.0, 0.0, 1.30), WOOD)
    collide((0.0, 0.0, 0.68), (1.06, 0.18, 1.36))

    # Two stamps hanging from the beam at different heights, so it reads mid-stroke
    # rather than parked.
    for x, drop in ((0.19, 0.30), (-0.19, 0.12)):
        taper(0.052, 0.052, 0.78, (x, 0.0, 0.72 + drop), WOOD, sides=7)
        taper(0.16, 0.19, 0.24, (x, 0.0, 0.40 + drop), STONE, sides=11, spin=False)

    # Braces from the uprights down to the mortar, which is what stops the frame
    # reading as two poles that happen to stand near a pot.
    box((0.42, 0.10, 0.10), (0.30, 0.0, 0.30), WOOD, rot_y=32.0)
    box((0.42, 0.10, 0.10), (-0.30, 0.0, 0.30), WOOD, rot_y=-32.0)


DESIGNS = (
    ("quern", quern),
    ("edge", edge),
    ("trough", trough),
    ("stamp", stamp),
)


# --------------------------------------------------------------------------- scene

def look(name):
    """
    Eye height, three metres back, with a one metre cube beside it.

    Never a hero orbit. The question a buildable piece has to answer is what it looks
    like when you walk past it, and the cube is there because a render with nothing to
    measure against silently makes everything the same size.
    """
    stage_scene()
    reference_cube((1.35, 0.25, 0.5))
    camera((1.05, -2.85, 1.70), (0.0, 0.0, 0.62))
    tint()
    render(os.path.join(PREVIEWS, name + ".png"), width=760, height=620, bloom=False)


def build(label, maker):
    name = "grove_mill_" + label
    winner = label == WINNER

    clear_scene()
    maker()
    obj = finish(name)
    export(obj, name, ASSETS)
    write_col(os.path.join(ASSETS, name + ".col"))
    tris = len(obj.data.polygons)

    if winner:
        export(obj, SHIPPED_MESH, SHIPPED)
        write_col(os.path.join(SHIPPED, SHIPPED_MESH + ".col"))

    clear_scene()
    maker()
    obj = finish(name)
    look(name)

    # A buildable piece is capped at 10,000 triangles and gets placed in rows, so the
    # number is multiplied by however many you build rather than paid once.
    flag = "  OVER BUDGET" if tris > 10000 else ""
    print("DESIGN_OK %s tris=%d%s%s" % (name, tris, " [SHIPPED]" if winner else "", flag))


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for label, maker in DESIGNS:
        build(label, maker)


main()
