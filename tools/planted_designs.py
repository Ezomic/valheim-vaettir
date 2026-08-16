"""
The ancient seed, just planted: what you actually see for the first hour.

    blender --background --python tools/planted_designs.py

Stage one is the state the piece spends most of its life in - you plant it and
then go away and kill forty greydwarfs - so it is the one that has to bear
looking at, and it currently does not. The shipped version stands a 5x5x24cm
emissive box vertically out of a 22cm seed, which is a wick on a mound. It reads
as a candle, and a candle is not a thing that answers to death.

Four replacements, all keeping the shared mound so the base of the piece does not
jump when it grows, and all keeping the shipped brief: small, easy to lose track
of, and deliberately unimpressive, because the whole point is that the later
stages are a reveal. Nothing here should look like it has already happened.

    cracked  the case split, one seam of light in it. Something is starting.
    husk     a closed ridged pod with no light at all. Nothing has started yet.
    curl     buried, with one thick shoot curling up out of the soil.
    ring     set in a circle of small stones. Somebody put this here on purpose.

Rendered from standing height looking down, which is the angle you actually see a
thing on the ground from - not the eye-level side-on view a piece this size would
never be seen at. The metre cube is there to make the point that it is small.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (box, camera, clear_scene, collide, disc, export, finish,
                     limb, orb, reference_cube, render, stage_scene, taper,
                     tint, write_col)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "assets")
ASSETS = os.path.join(SHIPPED, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The one that ships, under the name SaplingPrefab loads for its first stage.
#
# This supersedes stage_one in grove_designs.py, which built the candle. That
# script still owns stages two through four and its mound, and running it will
# overwrite this - see the note at the top of it.
WINNER = "curl"
SHIPPED_STAGE = "grove_sapling_1"


def mound():
    """
    Disturbed earth. Shared by every stage, and unchanged from the shipped one so
    the base does not move as the piece grows through its four models.
    """
    taper(0.28, 0.21, 0.09, (0.0, 0.0, 0.035), "moss", sides=8)

    # No collider, matching the other three stages, whose .col files carry a header
    # and nothing else. SaplingPrefab never reads a .col at all - the piece's
    # collision is the donor's, which is why the sapling is hoverable and
    # destructible without any of these files existing. A box written here would be
    # dead data that implied otherwise.


def cracked():
    """
    The case split down one side, with a single seam of light in the gap.

    The nearest to the shipped idea and the furthest from its silhouette: same
    "something is waking up inside" reading, but the light is *inside* a mass
    rather than standing on top of one. A seam cannot be a candle.
    """
    mound()

    # Two halves of one seed, leaning apart at the top. Slightly different sizes,
    # because a seed split by something growing does not split evenly.
    #
    # Far enough apart to leave a real gap: at 0.055 with a 0.15 radius the halves
    # overlapped straight across the middle and swallowed the seam entirely, which
    # is the same mistake the heartwood icon made and the same fix. A split you
    # cannot see is just a lumpy seed.
    taper(0.115, 0.088, 0.26, (-0.088, 0.0, 0.16), "seed", sides=7, rot_y=-11.0)
    taper(0.104, 0.080, 0.23, (0.090, 0.01, 0.145), "seed", sides=7, rot_y=10.0)

    # The seam, filling the gap and standing proud of both halves towards the
    # viewer - behind them it is occluded by the very thing it is meant to show.
    box((0.055, 0.070, 0.21), (0.0, -0.012, 0.165), "core")

    # Two shards of the case that came away, lying on the mound. They are what
    # says "this split" rather than "this grew in two pieces".
    box((0.075, 0.045, 0.022), (0.13, -0.075, 0.075), "bark", rot_z=28.0, rot_y=14.0)
    box((0.060, 0.038, 0.020), (-0.115, 0.085, 0.070), "bark", rot_z=-36.0)


def husk():
    """
    A closed pod. Ridged, squat, and with no light in it anywhere.

    The argument for it is restraint: the sapling is fed by killing, and a seed
    that already glows before you have killed anything has spent the reveal. The
    argument against is that a brown lump on brown earth is easy to walk past -
    which is either the point or a problem, depending on taste.
    """
    mound()

    orb(0.115, (0.0, 0.0, 0.135), "seed", subdivisions=2, stretch=1.25)

    # Five ridges up the sides. Odd count, so it never presents two flat parallel
    # edges to the camera.
    for i in range(5):
        angle = math.radians(72.0 * i + 14.0)
        box((0.030, 0.030, 0.230),
            (math.cos(angle) * 0.088, math.sin(angle) * 0.088, 0.140), "bark",
            rot_x=math.sin(angle) * 9.0, rot_y=-math.cos(angle) * 9.0)

    # A cap where the ridges meet, so the top is closed rather than open.
    orb(0.058, (0.0, 0.0, 0.245), "bark", subdivisions=1, stretch=0.66)


def curl():
    """
    Mostly buried, with one thick shoot curling up and over out of the soil.

    The only one with a hook in its outline, which makes it the most recognisable
    at a distance and across the four stages - the later ones are all uprights, so
    a curve here reads as "earliest" without needing to be smallest.
    """
    mound()

    # The seed itself, sunk into the mound so only its shoulder shows.
    orb(0.125, (0.0, 0.0, 0.075), "seed", subdivisions=2, stretch=0.85)

    # The shoot. Low pitch and a hard curve is an arc; a high pitch is a stick
    # lying over - which is the lesson the market's root arches paid for.
    limb((0.0, 0.02, 0.115), 0.42, 5, 0.038, 0.016, 12.0, 90.0, 26.0, "bark")

    # The tip, lit, hanging over the mound where the curl points back down.
    orb(0.042, (0.235, 0.03, 0.245), "core", subdivisions=1)

    # One small root breaking the surface, so the seed reads as gripping rather
    # than as set down.
    limb((-0.10, -0.05, 0.055), 0.16, 3, 0.026, 0.010, 62.0, 200.0, 14.0, "bark")


def ring():
    """
    The seed set inside a small circle of stones.

    Says something none of the others do: a person did this deliberately. The mod
    is a ritual - you plant a seed and then feed it deaths - and this is the only
    version whose silhouette admits that up front rather than leaving it to the
    later stages.

    Five stones, not twelve. A dense ring reads as a fire pit.
    """
    mound()

    taper(0.13, 0.105, 0.19, (0.0, 0.0, 0.135), "seed", sides=7, rot_x=5.0)

    # A low ember at the seed's base rather than a flame at its top. Light coming
    # from under something reads as buried; light on top of it reads as a candle,
    # which is the whole reason this set exists.
    disc(0.085, 0.030, (0.0, 0.0, 0.062), "core", sides=9, rot_x=0.0)

    for i in range(5):
        angle = math.radians(72.0 * i + 22.0)
        box((0.075, 0.062, 0.090),
            (math.cos(angle) * 0.195, math.sin(angle) * 0.195, 0.045), "stone",
            rot_z=math.degrees(angle) + 12.0, rot_x=6.0 * (i % 2))


DESIGNS = (
    ("cracked", cracked),
    ("husk", husk),
    ("curl", curl),
    ("ring", ring),
)


def build(label, maker):
    name = "grove_planted_" + label

    clear_scene()
    maker()
    obj = finish(name)

    export(obj, name, ASSETS)
    write_col(os.path.join(ASSETS, name + ".col"))

    # The winner goes out a second time under the stage name the mod loads, so the
    # variant and the shipped piece are the same geometry and cannot drift.
    if label == WINNER:
        export(obj, SHIPPED_STAGE, SHIPPED)
        write_col(os.path.join(SHIPPED, SHIPPED_STAGE + ".col"))

    tris = len(obj.data.polygons)

    clear_scene()
    maker()
    finish("preview")

    # Low emission. These are small and mostly dark, and at the usual strength the
    # lit part is the only thing the eye finds.
    tint(strength=0.6)
    stage_scene()

    # Back and to the side. A metre cube next to a 40cm seed is the whole point -
    # this thing is small - but placed close it simply ate a third of the frame and
    # the subject was the smaller object in its own render.
    reference_cube((1.35, 0.55, 0.5))

    # Standing height, looking down. A 40cm object on the ground is never seen from
    # the side at eye level, and framing it that way flatters a silhouette that has
    # to work from above.
    camera((-0.98, -1.16, 1.32), (0.0, 0.0, 0.13), lens=52)

    render(os.path.join(PREVIEWS, name + ".png"), width=640, height=560)

    print("DESIGN_OK %s tris=%d%s"
          % (name, tris, " [SHIPPED as " + SHIPPED_STAGE + "]" if label == WINNER else ""))


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for label, maker in DESIGNS:
        build(label, maker)


main()
