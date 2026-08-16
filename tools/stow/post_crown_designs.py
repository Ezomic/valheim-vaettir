"""
Four revisions of the crown, which is the treatment that was picked.

    blender --background --python tools/post_crown_designs.py

The heartwood goes at the top of the post, in the rail, where the spirit leaves
from. That much is settled. What is not settled is how it is held, and that is the
whole of the difference between the four here - they share a rack, a rail height and
a lump, and differ only in the top 30cm.

Which means the comparison shot has to be the top 30cm. A post is 1.33m tall and the
eye-height view that decided the silhouette renders the crown about eighty pixels
across, where all four look identical. So each design gets the standing view *and* a
detail row, and the detail row is the one that decides it.

The risk named when crown was chosen is the one to beat: a glowing lump above a shelf
is a finial, and a finial is decoration rather than a part. Each of these argues
against that differently - by sinking it in, by growing timber around it, by
sheltering it, or by making the gap around it obviously deliberate.

rack() and heartwood() are imported from post_heartwood.py rather than copied. These
are revisions of that piece, and a second copy of it would be free to drift.
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

# Where the top of the post sits. Every revision below works at this height, so it is
# named once rather than repeated four times with a chance of drifting.
RAIL = 1.28


# --------------------------------------------------------------------------- designs

def sunken():
    """
    Mortised into a solid rail, so only the dome of it clears the timber.

    The strongest answer to the finial risk: you cannot perch something that is
    visibly *inside* the wood. Light spills out of the mortise around the lump rather
    than radiating from a ball, which reads as the timber being lit from within.

    Costs the most - it is the least visible from across the room, and from directly
    in front you see a rail with a glow in it rather than a heartwood.
    """
    rack(skip_top_rail=True)

    # A solid rail, deeper than standard, with the lump let into it from above. Two
    # blocks front and back rather than one with a hole: a hole means a boolean, and
    # a boolean on a bevelled box is a mess of shards.
    for y in (-0.175, 0.175):
        box((1.20, 0.21, 0.20), (0.0, y, RAIL + 0.02), "wood", tilt=WOOD_TILT)
        collide((0.0, y, RAIL + 0.02), (1.20, 0.21, 0.20))

    for x in (-0.42, 0.42):
        box((0.36, 0.56, 0.20), (x, 0.0, RAIL + 0.02), "wood", tilt=WOOD_TILT)
        collide((x, 0.0, RAIL + 0.02), (0.36, 0.56, 0.20))

    heartwood((0.0, 0.0, RAIL + 0.03), size=1.05)

    # A worn iron lip around the mouth of the mortise, which is what stops the opening
    # reading as a hole someone forgot to finish.
    for y in (-0.10, 0.10):
        box((0.34, 0.035, 0.045), (0.0, y, RAIL + 0.125), "iron", tilt=1.0)
    for x in (-0.155, 0.155):
        box((0.035, 0.24, 0.045), (x, 0.0, RAIL + 0.125), "iron", tilt=1.0)


def horns():
    """
    The rail's ends grow up into two timber horns that cradle it from either side.

    Timber holding timber, so nothing about it reads as hardware bolted on. The horns
    give the post a silhouette above the shelf line, which is the one thing a rack
    otherwise lacks - it is a flat rectangle from any distance.

    Risk: two upright prongs either side of a glowing ball is close to an altar, and
    this is a piece of storage furniture.
    """
    rack(skip_top_rail=True)

    box((1.20, 0.56, 0.10), (0.0, 0.0, RAIL), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, RAIL), (1.20, 0.56, 0.10))

    # Tapered and leaned inward, each in two segments so the lean is a curve rather
    # than a bend. One straight prong is a stick; the taper is what makes it grown.
    for side in (-1, 1):
        taper(0.075, 0.055, 0.20, (side * 0.235, 0.0, RAIL + 0.12), "wood",
              sides=7, rot_y=-side * 9.0, tilt=WOOD_TILT)
        taper(0.055, 0.032, 0.17, (side * 0.185, 0.0, RAIL + 0.29), "wood",
              sides=7, rot_y=-side * 26.0, tilt=WOOD_TILT)

    heartwood((0.0, 0.0, RAIL + 0.24), size=1.05)

    # One strap under the lump, tying horn to horn through it. Without it the ball
    # sits in the gap rather than being held in it.
    box((0.44, 0.06, 0.030), (0.0, 0.0, RAIL + 0.135), "iron", tilt=1.0)


def canopy():
    """
    Sheltered under a small pitched cover on two posts, light spilling out beneath.

    A roof over something is the clearest way a building says *this matters and it
    must stay dry*, and it turns the crown from an ornament into a housing. It is also
    the only revision where the light is thrown downward onto the top shelf, which is
    the shelf you actually reach into.

    Risk: tallest of the four by a long way, and a post that no longer fits under a
    1.8m ceiling is a post you cannot put in a cellar.
    """
    rack(skip_top_rail=True)

    box((1.20, 0.56, 0.10), (0.0, 0.0, RAIL), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, RAIL), (1.20, 0.56, 0.10))

    heartwood((0.0, 0.0, RAIL + 0.16), size=0.95)

    for x in (-0.20, 0.20):
        taper(0.038, 0.030, 0.30, (x, -0.115, RAIL + 0.20), "wood", sides=7,
              tilt=WOOD_TILT)

    # Two boards leaning against each other, not one plate: a flat plate across the
    # top is a lid, and a lid is what a canopy must not be.
    for side in (-1, 1):
        box((0.46, 0.30, 0.035), (side * 0.205, -0.02, RAIL + 0.395), "wood",
            rot_y=side * 26.0, tilt=WOOD_TILT)

    box((0.09, 0.32, 0.045), (0.0, -0.02, RAIL + 0.445), "wood", tilt=WOOD_TILT)
    collide((0.0, -0.02, RAIL + 0.40), (0.95, 0.32, 0.12))


def suspended():
    """
    Hanging in an open square cut through the rail, held on three iron pins.

    The gap is the design: air all the way round the lump says it is held rather than
    seated, and you can see the shelf behind it through the opening. That is the one
    thing none of the others do, and it is the most obviously deliberate - nothing
    accidental has daylight around it.

    Risk: the thinnest, and the pins are small enough that at distance it reads as a
    ball floating in a hole, which is either the best or the worst of these depending
    on how much magic the piece is allowed.
    """
    rack(skip_top_rail=True)

    # The rail in four pieces around a square void, rather than a rail with a hole.
    for x in (-0.415, 0.415):
        box((0.37, 0.56, 0.13), (x, 0.0, RAIL + 0.01), "wood", tilt=WOOD_TILT)
        collide((x, 0.0, RAIL + 0.01), (0.37, 0.56, 0.13))

    for y in (-0.205, 0.205):
        box((0.50, 0.15, 0.13), (0.0, y, RAIL + 0.01), "wood", tilt=WOOD_TILT)
        collide((0.0, y, RAIL + 0.01), (0.50, 0.15, 0.13))

    heartwood((0.0, 0.0, RAIL + 0.015), size=0.92)

    # Three pins, not four. Four present two of themselves edge-on from any angle and
    # the lump reads as sitting on a pair of rails.
    for i in range(3):
        angle = math.radians(120.0 * i + 20.0)
        reach = 0.20
        box((abs(math.cos(angle)) * reach + 0.05, abs(math.sin(angle)) * reach + 0.05,
             0.022),
            (math.cos(angle) * reach * 0.5, math.sin(angle) * reach * 0.5,
             RAIL + 0.015),
            "iron", rot_z=math.degrees(angle), tilt=1.0)


DESIGNS = (
    ("stow_post_crown_sunken", sunken),
    ("stow_post_crown_horns", horns),
    ("stow_post_crown_canopy", canopy),
    ("stow_post_crown_suspended", suspended),
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
        camera((-1.75, 2.85, 1.70), (0.0, 0.0, 0.80), lens=45)
        render(os.path.join(PREVIEWS, name + ".png"), width=720, height=640, bloom=False)

        # Again, closer, and cooler. At the emission the standing shot wants, a
        # 23cm lump fills a tight crop with pure white and has no form at all -
        # which is the whole thing being judged here.
        tint(0.80)
        camera((-0.95, 1.62, 1.82), (0.0, 0.0, 1.42), lens=55)
        render(os.path.join(PREVIEWS, name + "_top.png"), width=720, height=560,
               bloom=False)

        tris = len(obj.data.polygons)
        flag = "  OVER 10k" if tris > 10000 else ""
        print("DESIGN_OK %s verts=%d tris=%d%s"
              % (name, len(obj.data.vertices), tris, flag))

    lineup("stow_post_crown_lineup", 1.95, (0.0, 7.40, 1.70), (0.0, 0.0, 0.80), 32,
           1400, 580)



def lineup(name, spacing, at, aim, lens, width, height):
    clear_scene()

    for index, (label, build) in enumerate(DESIGNS):
        offset = (index - 1.5) * spacing

        before = set(bpy.data.objects)
        build()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    finish(name)
    tint(GLOW)
    stage_scene()

    bloom_setup(size=7, threshold=0.62)
    camera(at, aim, lens=lens)
    render(os.path.join(PREVIEWS, name + ".png"), width=width, height=height,
           bloom=False)
    print("DESIGN_OK " + name)


main()
