"""
Four revisions of the canopy, which is the shelter that was picked.

    blender --background --python tools/post_canopy_designs.py

The heartwood sits on the top rail with something over it. What is not settled is
what *kind* of something - and a roof pitch is not a design, so these vary the thing
the shelter actually is: a cap, a layered roof, a gable end turned to face you, or a
housing closed on three sides that throws the light forward.

Height is the number to watch and it is printed for each. The canopy that was picked
is 1.797m, against 1.43m for the crowns that do not shelter anything. Vanilla walls
are 2m, so it clears one by 20cm - and anything taller than about 1.85m here stops
being furniture you put in a cellar. Two of these deliberately come in lower.

The shared base is the rack, its top rail and a heartwood on the rail, all imported
from post_heartwood.py rather than copied. A second copy of the piece these are
revisions of would be free to drift.
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
# tools/ first: vhbuild.py is vendored here so this repo stands alone. The sibling
# vaettir checkout is kept as a fallback for a working tree that has both.
sys.path.insert(0, os.path.join(os.path.dirname(ROOT), "vaettir", "tools"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (bloom_setup, box, camera, clear_scene, collide, export, finish,
                     orb, reference_cube, render, stage_scene, taper, tint, write_col)

from post_heartwood import GLOW, WOOD_TILT, heartwood, rack

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# Matches the top rail rack() builds when it is not skipped.
RAIL = 1.28


def base(lump_z=RAIL + 0.155, lump=0.95):
    """The rack, its rail and the heartwood. Everything below only adds the shelter."""
    rack(skip_top_rail=True)

    box((1.20, 0.56, 0.10), (0.0, 0.0, RAIL), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, RAIL), (1.20, 0.56, 0.10))

    heartwood((0.0, 0.0, lump_z), size=lump)


def posts(at_x, at_y=-0.115, height=0.30, top=None, radius=0.038):
    """The uprights a shelter stands on. Tapered, because a straight peg is a dowel."""
    z = (RAIL + 0.20) if top is None else top
    for x in at_x:
        taper(radius, radius * 0.80, height, (x, at_y, z), "wood", sides=7,
              tilt=WOOD_TILT)


# --------------------------------------------------------------------------- designs

def hipped():
    """
    A four-sided cap on four corner posts. The lowest of these by a clear margin.

    A hipped roof reads as finished from every side, which matters for a piece that
    may not go against a wall - the two-board pitch has a front and a back, and from
    the side it is two sticks and a line. Squat on purpose: it caps the heartwood
    rather than towering over it.

    Risk: at this height the eaves are close to the lump, so from standing you see
    roof and very little glow.
    """
    base()

    for x in (-0.19, 0.19):
        posts([x], at_y=-0.13, height=0.24, top=RAIL + 0.17)
        posts([x], at_y=0.13, height=0.24, top=RAIL + 0.17)

    # Four boards leaning to a point. The two long sides carry the pitch in y, the two
    # short ends in x, which is what makes it hipped rather than a gable with wings.
    for side in (-1, 1):
        box((0.62, 0.34, 0.035), (0.0, side * 0.135, RAIL + 0.345), "wood",
            rot_x=-side * 32.0, tilt=WOOD_TILT)
        box((0.34, 0.40, 0.035), (side * 0.245, 0.0, RAIL + 0.345), "wood",
            rot_y=side * 32.0, tilt=WOOD_TILT)

    box((0.13, 0.13, 0.06), (0.0, 0.0, RAIL + 0.435), "wood", tilt=2.0)
    collide((0.0, 0.0, RAIL + 0.35), (0.75, 0.50, 0.20))


def eaved():
    """
    The picked canopy, but with real eaves: two courses of boards and a long overhang.

    One course of boards is a lean-to; two, with the lower set proud of the upper, is
    a roof. The overhang also puts the shelf below in shade, which is the only one of
    these where the shelter does something to the rest of the piece.

    Risk: the tallest here, and the overhang is the first thing to clip a wall.
    """
    base()
    posts((-0.20, 0.20))

    for side in (-1, 1):
        # Upper course.
        box((0.44, 0.30, 0.032), (side * 0.195, -0.02, RAIL + 0.405), "wood",
            rot_y=side * 28.0, tilt=WOOD_TILT)

        # Lower course, set outboard and down so its head laps *under* the upper
        # board's foot. Placed inboard of that - as it first was - the two cross
        # instead of lapping, and the roof renders as boards sliding off it.
        box((0.36, 0.36, 0.030), (side * 0.47, -0.02, RAIL + 0.255), "wood",
            rot_y=side * 28.0, tilt=WOOD_TILT)

    box((0.10, 0.34, 0.045), (0.0, -0.02, RAIL + 0.455), "wood", tilt=WOOD_TILT)
    collide((0.0, -0.02, RAIL + 0.32), (1.30, 0.36, 0.28))


def gable():
    """
    The roof turned a quarter, so the triangular gable end faces you.

    A gable is the most building-like shape there is, and turning it means the
    silhouette from the front is a triangle rather than an inverted V - the one
    outline here that is not a roof seen edge-on. The tympanum is boarded, which is
    what gives it a face.

    Risk: turned this way the roof is deep front-to-back and narrow across, so from
    the side it is a wedge and the heartwood is behind timber.
    """
    base()
    posts((-0.185, 0.185), at_y=0.0, height=0.26, top=RAIL + 0.18)

    # Ridge along y, so the boards lean in x.
    for side in (-1, 1):
        box((0.34, 0.52, 0.032), (side * 0.135, -0.02, RAIL + 0.375), "wood",
            rot_y=side * 40.0, tilt=WOOD_TILT)

    box((0.075, 0.56, 0.045), (0.0, -0.02, RAIL + 0.455), "wood", tilt=WOOD_TILT)

    # The gable face, in two boards meeting at the ridge rather than one triangle -
    # a single plate across an opening is a lid, and this has to read as boarding.
    for side in (-1, 1):
        box((0.20, 0.035, 0.13), (side * 0.075, 0.235, RAIL + 0.295), "wood",
            rot_y=side * 40.0, tilt=WOOD_TILT)

    collide((0.0, -0.02, RAIL + 0.36), (0.62, 0.60, 0.20))


def housing():
    """
    Closed at the back and both sides, open at the front: a lantern house.

    The only one that aims the light. Three walls mean the glow comes out forwards
    and the post reads from across a room as a lit opening rather than as a lump with
    a hat. It is also the most obviously *built* - a thing with walls is joinery,
    where a roof on two sticks is a shelter someone threw up.

    Risk: the least of the heartwood is visible, and from behind the post is a closed
    box with no light at all.
    """
    base(lump_z=RAIL + 0.145, lump=0.90)

    # Back wall on -y, where the camera cannot see it, and the light cannot get out.
    box((0.62, 0.045, 0.34), (0.0, -0.185, RAIL + 0.19), "wood", tilt=WOOD_TILT)

    for side in (-1, 1):
        box((0.045, 0.40, 0.34), (side * 0.29, -0.01, RAIL + 0.19), "wood",
            tilt=WOOD_TILT)

    # A shallow pitch rather than a flat top: flat reads as a crate, and the whole
    # argument for a shelter is that it is a roof.
    for side in (-1, 1):
        box((0.40, 0.46, 0.035), (side * 0.165, -0.01, RAIL + 0.395), "wood",
            rot_y=side * 15.0, tilt=WOOD_TILT)

    box((0.10, 0.48, 0.040), (0.0, -0.01, RAIL + 0.425), "wood", tilt=WOOD_TILT)

    # A sill across the bottom of the opening, so the mouth has an edge.
    box((0.62, 0.05, 0.045), (0.0, 0.185, RAIL + 0.045), "iron", tilt=1.0)

    collide((0.0, -0.01, RAIL + 0.22), (0.66, 0.48, 0.44))


DESIGNS = (
    ("stow_post_canopy_hipped", hipped),
    ("stow_post_canopy_eaved", eaved),
    ("stow_post_canopy_gable", gable),
    ("stow_post_canopy_housing", housing),
)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        build()
        obj = finish(name)

        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tint(GLOW)
        stage_scene()
        reference_cube((-1.55, -0.55, 0.50))

        bloom_setup(size=7, threshold=0.62)
        camera((-1.75, 2.85, 1.70), (0.0, 0.0, 0.84), lens=45)
        render(os.path.join(PREVIEWS, name + ".png"), width=720, height=660, bloom=False)

        # Again, closer and cooler. At standing emission a 22cm lump under a roof is a
        # white disc in a shadow, and the shelter is the thing being judged.
        tint(0.80)
        camera((-0.95, 1.72, 1.90), (0.0, 0.0, 1.50), lens=52)
        render(os.path.join(PREVIEWS, name + "_top.png"), width=720, height=580,
               bloom=False)

        heights = [v.co.z for v in obj.data.vertices]
        print("DESIGN_OK %s verts=%d tris=%d height=%.3f"
              % (name, len(obj.data.vertices), len(obj.data.polygons),
                 max(heights) - min(heights)))

    lineup()
    chosen()


# The revision that was picked, exported under the name PostModelFile already names.
# One script owns the shipped mesh; the other four here stay in variants/, which the
# csproj does not copy.
CHOSEN = ("stow_post_canopy", housing)


def chosen():
    name, build = CHOSEN

    clear_scene()
    build()
    obj = finish(name)

    export(obj, name, ASSETS)
    write_col(os.path.join(ASSETS, name + ".col"))

    heights = [v.co.z for v in obj.data.vertices]
    print("CHOSEN_OK %s verts=%d tris=%d height=%.3f"
          % (name, len(obj.data.vertices), len(obj.data.polygons),
             max(heights) - min(heights)))


def lineup():
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
    camera((0.0, 7.40, 1.70), (0.0, 0.0, 0.85), lens=32)
    render(os.path.join(PREVIEWS, "stow_post_canopy_lineup.png"),
           width=1400, height=600, bloom=False)
    print("DESIGN_OK stow_post_canopy_lineup")


main()
