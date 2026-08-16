"""
Four labels for a chest, built and rendered for comparison.

    blender --background --python tools/label_designs.py

A chest's rule lives on its ZDO, which means it is invisible until you walk up and
hover it. A wall of twenty identical chests is exactly the case where that is not
good enough, and it is the same argument that got the stowing post built instead of
a keybind: prefer a thing in the world.

The constraint that shapes all four: **a label you have to hover is worthless**,
because hovering already tells you. So it has to carry text in world space, and the
way to get that without inventing a text renderer is to ride the vanilla `sign` -
clone it for its Sign component and its TextMesh, then wear a hand-built body, which
is exactly the trade the post makes with `piece_chest_wood`.

That gives every design here the same hard requirement: a flat face, roughly 30cm by
11cm, square to the front of the chest and unobstructed. The pale inset plate in each
render is where the text goes; it is not modelled at runtime, the sign draws there.

The four differ in how they attach, because that is the only real choice left:
hanging off the lid, nailed to the front, framed and set in, or standing on top.

Rendered against a stand-in chest - a plain body and lid at vanilla's rough size.
It is deliberately featureless: this is a comparison of labels, and modelling a
convincing chest would just make it the thing being looked at.
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

from vhbuild import (TINTS, box, camera, clear_scene, export, finish, orb,
                     reference_cube, render, stage_scene, taper, tint)

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# Where the writing goes. Every design has to present this rectangle, flat and facing
# the front, or the sign has nowhere to draw.
FACE_W = 0.30
FACE_H = 0.11

# Front of the stand-in chest. The camera stands on +y, so this is the side it sees.
FRONT = 0.25
CHEST_TOP = 0.50

# Preview only - a pale plate standing in for lettering.
TINTS["slate"] = (0.62, 0.60, 0.55, 1.0)


def writing(at, width=FACE_W, height=FACE_H, depth=0.008):
    """
    The text plate, for the render only.

    Never exported. At runtime this rectangle is empty and the cloned sign's TextMesh
    draws into it, so a plate baked into the mesh would sit behind the words.
    """
    box((width, depth, height), at, "slate", tilt=0.6)


# --------------------------------------------------------------------------- designs

def tag():
    """
    A tag hung off the lid on two short hooks, like a luggage label.

    Reads as a thing that was tied on rather than built in, which suits a rule you
    change your mind about. It also hangs clear of the chest, so it stays legible
    when the chest is against a dark wall. Risk: anything dangling reads as loose,
    and twenty loose tags in a row may read as clutter.
    """
    # Each strap is two overlapping boxes turning the corner of the lid, not one
    # upright pin. Modelled as a single rod the first pass came out as two tacks
    # standing on top of the lid, hooked over nothing - the strap has to be visibly
    # *on* the lid and *down* the front or it is not hanging from anything.
    lid_front = 0.27
    for side in (-1, 1):
        box((0.032, 0.11, 0.013), (side * 0.10, lid_front - 0.05, CHEST_TOP + 0.006),
            "iron", tilt=1.0)
        box((0.032, 0.013, 0.115), (side * 0.10, lid_front + 0.006, CHEST_TOP - 0.05),
            "iron", tilt=1.0)

    # Hangs below the lid, against the body's front face, which is set back from the
    # lid's - so the plate stands proud of the body and clear of the overhang.
    plate = CHEST_TOP - 0.135
    box((FACE_W + 0.03, 0.020, FACE_H + 0.03), (0.0, FRONT + 0.014, plate), "wood")
    writing((0.0, FRONT + 0.026, plate))


def plank():
    """
    A plank nailed flat to the front, with two visible nails.

    The plainest and the most Valheim: it is what someone would actually do. Sits
    inside the chest's own outline, so a row of them stays a row of chests rather
    than becoming a row of signs. Risk: flat against the front, it can disappear into
    the chest at a glance, which is the one thing a label must not do.
    """
    plate = CHEST_TOP - 0.16
    box((FACE_W + 0.05, 0.024, FACE_H + 0.04), (0.0, FRONT + 0.012, plate), "wood")

    for side in (-1, 1):
        # Heads proud of the plank, not flush. A flush nail is a dot.
        orb(0.014, (side * (FACE_W * 0.5 + 0.005), FRONT + 0.026, plate), "iron",
            subdivisions=1)

    writing((0.0, FRONT + 0.026, plate))


def slate():
    """
    A stone tile set into a wooden frame.

    The only one that is not wood on wood, so it separates from the chest by material
    rather than by sticking out - which is what keeps it legible without adding
    silhouette. Four thin boxes for the frame, never a plate behind the tile: a full
    plate across an opening is a lid, and this needs to read as a thing set *into*
    something.
    """
    plate = CHEST_TOP - 0.16
    inner_w, inner_h = FACE_W + 0.02, FACE_H + 0.02
    bar = 0.028

    box((inner_w + bar * 2, 0.030, bar),
        (0.0, FRONT + 0.015, plate + inner_h * 0.5 + bar * 0.5), "wood")
    box((inner_w + bar * 2, 0.030, bar),
        (0.0, FRONT + 0.015, plate - inner_h * 0.5 - bar * 0.5), "wood")
    box((bar, 0.030, inner_h),
        (-inner_w * 0.5 - bar * 0.5, FRONT + 0.015, plate), "wood")
    box((bar, 0.030, inner_h),
        (inner_w * 0.5 + bar * 0.5, FRONT + 0.015, plate), "wood")

    box((inner_w, 0.018, inner_h), (0.0, FRONT + 0.010, plate), "stone")
    writing((0.0, FRONT + 0.021, plate))


def board():
    """
    A small board standing on two posts on top of the chest.

    The only one visible over the top of the chest in front of it, which is the whole
    argument for it: in a room where chests are stacked or you are looking down a row,
    every other design here is edge-on or hidden. Costs the most silhouette, and it is
    the one that will collide with a shelf above.
    """
    plate = CHEST_TOP + 0.17

    for side in (-1, 1):
        taper(0.018, 0.014, 0.16, (side * 0.11, FRONT - 0.06, CHEST_TOP + 0.07),
              "wood", sides=7)

    box((FACE_W + 0.06, 0.026, FACE_H + 0.04), (0.0, FRONT - 0.06, plate), "wood")

    # A cap rail across the top. Without it the board is a slab on two sticks; with
    # it the thing reads as built.
    box((FACE_W + 0.10, 0.040, 0.022),
        (0.0, FRONT - 0.06, plate + FACE_H * 0.5 + 0.03), "wood")

    writing((0.0, FRONT - 0.046, plate))


DESIGNS = (
    ("stow_label_tag", tag),
    ("stow_label_plank", plank),
    ("stow_label_slate", slate),
    ("stow_label_board", board),
)


# --------------------------------------------------------------------------- staging

def chest():
    """
    A stand-in, not a model. Vanilla's rough footprint and nothing else.

    Deliberately featureless: this is a comparison of labels, and a convincing chest
    would become the thing being looked at.
    """
    box((0.90, 0.50, 0.40), (0.0, 0.0, 0.20), "wood", tilt=0.0)
    box((0.94, 0.54, 0.10), (0.0, 0.0, 0.45), "wood", tilt=0.0)


def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        build()
        obj = finish(name)

        # Exported before the chest and the lettering exist, so the mesh is the
        # label alone and its origin is the chest's front face.
        export(obj, name, VARIANTS)

        chest()
        tint()
        stage_scene()
        reference_cube((-1.35, -0.35, 0.50))

        camera((-0.95, 2.05, 1.70), (0.0, 0.0, 0.42), lens=55)
        render(os.path.join(PREVIEWS, name + ".png"), width=720, height=600, bloom=False)

        print("DESIGN_OK %s verts=%d tris=%d"
              % (name, len(obj.data.vertices), len(obj.data.polygons)))

    lineup()


def lineup():
    """Four chests in a row, each wearing one, at the distance you read them from."""
    clear_scene()

    spacing = 1.15
    for index, (name, build) in enumerate(DESIGNS):
        offset = (index - 1.5) * spacing

        before = set(bpy.data.objects)
        build()
        chest()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    finish("lineup")
    tint()
    stage_scene()

    # No reference cube in this one. Four chests at a known size are a better scale
    # cue for a label than a cube is, and the row is already as wide as the frame -
    # fitting a cube beside it meant a lens short enough to bow the row.

    # Further back and lower than the single shots: a label is judged at the distance
    # you would actually be standing when you want to know which chest is which.
    camera((0.0, 4.60, 1.70), (0.0, 0.0, 0.55), lens=37)
    render(os.path.join(PREVIEWS, "stow_label_lineup.png"),
           width=1400, height=560, bloom=False)
    print("DESIGN_OK stow_label_lineup")


main()
