"""
Five carriers for the stowing post, built and rendered for comparison.

    blender --background --python tools/carrier_designs.py

The post no longer teleports its contents. A spirit lifts one stack at a time and
flies it to the chest that asked for it, so there is now a small thing crossing the
storage room that has to be worth watching a dozen times a session.

That last part is the whole brief, and it rules out most of what a "magic helper"
usually looks like. It is seen small, in motion, often against a dim indoor wall,
and always holding something. So:

  * the cargo is part of the silhouette, not an afterthought. Every design here is
    rendered carrying a placeholder crate, because a carrier judged empty is judged
    in a state it is almost never in
  * it has no walk cycle and no body. Same reasoning as Vaettir's spirit - a mod
    with no Unity editor can author motion in code and cannot author an animation
    controller, so anything with legs is out
  * small. Roughly a forearm across. A knee-high spirit ferrying a stack of ore
    would be the largest thing in the room and would own it

Four are Stow's own. The fifth deliberately wears Vaettir's spirit - hoop, motes and
upright heart - so the two mods can be compared side by side and the question "should
spirits read as one idea across these mods, or should each mod have its own" can be
answered by looking rather than by arguing.

Colour is not the comparison. Everything renders in the same warm gold because the
light's colour is one line of config at runtime; what is being chosen here is form.

Rules paid for by Stoker's and the post's rejected models, all still in force:
every part overlaps its neighbour, few large parts beat many small ones, seeded
jitter on everything, and a bevel pass per object before the join.
"""

import os
import sys

# Three levels: tools/stow -> tools -> the repo. These scripts used to sit in their
# own repo's tools/ and two was right there; they are one deeper now, and two would
# quietly write every asset into tools/assets instead of assets.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# tools/ first: vhbuild.py is vendored here so this repo stands alone. The sibling
# vaettir checkout is kept as a fallback for a working tree that has both.
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy
import math

from mathutils import Vector

from vhbuild import (bloom_setup, box, camera, clear_scene, export, finish, limb,
                     material, orb, reference_cube, render, ring, stage_scene, tint,
                     wobble)

ASSETS = os.path.join(ROOT, "assets")

# Rejected designs are not deploy weight. The csproj copies assets\*.obj beside the
# dll, one level only, so a variant folder is excluded by construction - four dead
# carriers are four files the plugin folder never sees.
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# How high the core floats in the render. Chest height: it flies at about the level
# of the chest lids it is filling, which is also the height it is seen at.
HOVER = 1.20

# Emission is deliberately low for these renders. At the strength the spirit
# actually wants in game the cores blow to flat white and take their own geometry
# with them - the first batch was five white blobs and no legible form. The runtime
# light is a separate thing entirely; this number only has to make shape readable.
GLOW = 0.62


# --------------------------------------------------------------------------- parts

def strut(a, b, radius, mat, sides=5, taper_to=None, tilt=2.0):
    """
    A tapering rod between two exact points.

    vhbuild's taper() takes a location and two euler angles, which is fine for
    anything upright and unusable for an arm that has to *land* somewhere - three
    arms meeting a rim need their endpoints, not their inclinations. Everything here
    is built endpoint to endpoint for that reason, and the arms actually touch the
    rim rather than nearly touching it.
    """
    start, end = Vector(a), Vector(b)
    span = end - start

    (jx, jy, jz), _ = wobble(tilt)

    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=radius,
        radius2=radius if taper_to is None else taper_to,
        depth=span.length, location=(start + end) / 2.0)

    obj = bpy.context.active_object

    # to_track_quat points the cone's own +Z down the span. Doing this by hand with
    # euler angles works for struts in a plane and quietly fails for the yoke's
    # hanger, which is off-axis in two directions at once.
    aim = span.to_track_quat("Z", "Y").to_euler()
    obj.rotation_euler = (aim.x + math.radians(jx), aim.y + math.radians(jy),
                          aim.z + math.radians(jz))

    obj.data.materials.append(material(mat))
    return obj


def core(at, scale=1.0):
    """
    The light itself: two nested spheroids, the inner one tighter.

    One orb renders as a ball. Two of different stretch give the glow somewhere to
    fall off to, which is what stops it reading as a painted sphere - the same
    doubling Vaettir's heart uses, at about half the size.
    """
    orb(0.078 * scale, at, "core", subdivisions=2, stretch=1.18)
    orb(0.049 * scale, at, "core", subdivisions=2, stretch=1.44)


def cargo(at, size=0.15):
    """
    A placeholder crate, for the render only.

    Never exported and never joined into the carrier: at runtime the thing being
    carried is the item's own prefab, so a crate baked into the mesh would be a
    carrier permanently holding a crate it is not holding. It exists here because a
    carrier judged empty is judged in a state it is almost never in.
    """
    x, y, z = at
    box((size, size, size * 0.82), at, "wood", tilt=6.0)
    box((size * 1.06, size * 0.16, size * 0.20), (x, y, z + size * 0.22), "iron", tilt=6.0)
    box((size * 0.16, size * 1.06, size * 0.20), (x, y, z - size * 0.20), "iron", tilt=6.0)


# --------------------------------------------------------------------------- designs
#
# Each returns where its load rides and how big it is, rather than that being
# declared in a table beside it. The first pass kept the anchors in the DESIGNS
# tuple and the crook's load ended up hanging in mid-air a hand's width from the
# hook, because the shaft's tip is computed by limb() and the table was guessing at
# it. Anything derived from geometry has to come back out of the geometry.

def cradle():
    """
    A light with a basket slung under it.

    The most literal reading of the brief and the one to beat: a rim with something
    sitting in it is how a carried load looks, and the load is inside the outline
    rather than stuck to it. Risk is that a rim under a glow is a table lamp.
    """
    top = HOVER + 0.15
    core((0.0, 0.0, top))

    rim = 0.145
    ring(rim, 0.014, (0.0, 0.0, HOVER - 0.16), "core", major=15, minor=5,
         rot_x=0.0, tilt=1.5)

    # Three, not four. Four arms present two of themselves edge-on from any angle
    # and the basket reads as hanging from two strings.
    for i in range(3):
        rad = math.radians(120.0 * i + 24.0)
        strut((0.0, 0.0, top - 0.06),
              (math.cos(rad) * rim, math.sin(rad) * rim, HOVER - 0.16),
              0.020, "core", taper_to=0.011)

    return (0.0, 0.0, HOVER - 0.15), 0.145


def crook():
    """
    A wisp carrying a bindle: the light at one end, the load swinging off the other.

    The only design whose weight is off to one side, which is what makes it read as
    *carried* rather than as *contained* - a hanging load swings, and the eye reads
    swing as weight. Also the only one where the load is well clear of the glow, so
    it stays legible against a bright doorway.

    yaw=90 is load-bearing. limb() builds its heading as Rz(yaw) @ Rx(pitch), so at
    yaw=0 the whole arc bends through Y - straight at the camera - and the first
    render came out as a stub with a crate floating beside it. At 90 the bend lands
    in X, where it can be seen.
    """
    tip = limb((-0.115, 0.0, HOVER - 0.20), 0.52, 5, 0.026, 0.013,
               4.0, 90.0, 19.0, "core", sides=5)

    # The light is the hand, at the bottom of the shaft, not the hook. Putting it on
    # the hook made it a lamp on a bent post; down here the shape reads as something
    # holding the crook up.
    core((-0.115, 0.0, HOVER - 0.20), scale=0.94)

    hang = (tip.x, tip.y, tip.z - 0.19)
    strut((tip.x, tip.y, tip.z - 0.015), (hang[0], hang[1], hang[2] + 0.06),
          0.012, "core")

    return hang, 0.145


def claw():
    """
    A fist of light closed around the load.

    Fingers curling up and over mean the load is gripped from underneath, so this is
    the one that could carry anything - a rim has a size and a hook has a hanging
    point, and a grip has neither. Risk is that four curled fingers around a bright
    core is a Mistlands seeker egg.
    """
    core((0.0, 0.0, HOVER + 0.10), scale=0.94)

    hold = HOVER - 0.10

    for i in range(4):
        rad = math.radians(90.0 * i + 38.0)

        knuckle = (math.cos(rad) * 0.062, math.sin(rad) * 0.062, HOVER + 0.035)
        curl = (math.cos(rad) * 0.168, math.sin(rad) * 0.168, hold - 0.035)
        point = (math.cos(rad) * 0.108, math.sin(rad) * 0.108, hold + 0.105)

        # Two segments per finger, and the second one turns back up past the load.
        # The first pass used short straight stubs, which read as four little flames
        # sitting on a box rather than as anything holding it.
        strut(knuckle, curl, 0.030, "core", taper_to=0.022)
        strut(curl, point, 0.022, "core", taper_to=0.011)

    return (0.0, 0.0, hold), 0.16


def yoke():
    """
    A bar with the load on one end and the light counterweighting the other.

    The odd one out on purpose: wide rather than tall, and the only silhouette that
    is legible at a glance from across a dark room, which for something that spends
    its life in transit may matter more than how it looks up close. Asymmetric, so
    it also says which way it is going.
    """
    bar = HOVER + 0.02

    # A slight lean, because a perfectly level bar carrying a load on one end is the
    # one arrangement that could not actually happen. Chunkier than the first pass,
    # where 4cm of bar at this distance read as a wire and the whole thing looked
    # like a street lamp.
    box((0.40, 0.055, 0.045), (0.0, 0.0, bar), "core", rot_y=7.0, tilt=2.0)

    core((-0.165, 0.0, bar + 0.075), scale=0.84)

    # Far enough out to clear the core's glow. Inside it, the counterweight simply
    # was not there in the render.
    orb(0.052, (-0.235, 0.0, bar - 0.045), "core", subdivisions=1)

    hang = (0.185, 0.0, HOVER - 0.20)
    strut((0.185, 0.0, bar - 0.045), (0.185, 0.0, hang[2] + 0.06), 0.013, "core")

    return hang, 0.145


def hoop():
    """
    Vaettir's spirit, shrunk and given something to carry.

    Same hoop, motes and upright heart, at 0.62 scale - a knee-high spirit ferrying
    ore would be the largest thing in the storage room. The load rides below on a
    short tether of two motes, which is the only addition: a hoop with a crate
    wedged through it would break the one shape the design is known by.

    Here to make the family-resemblance question answerable by looking. Picking it
    means spirits are one idea across the mods and Stow carries a copy of the
    meshes; picking any other means each mod has its own.
    """
    scale = 0.62
    orbit = 0.34 * scale

    orb(0.150 * scale, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.30)
    orb(0.104 * scale, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.55)

    for i in range(7):
        rad = math.radians(360.0 / 7.0 * i + 12.0)
        orb(0.040 * scale, (math.cos(rad) * orbit, 0.0, HOVER + math.sin(rad) * orbit),
            "core", subdivisions=1, tilt=0.0)

    ring(orbit, 0.010, (0.0, 0.0, HOVER), "core", minor=5, tilt=0.0)

    # Two motes, tight against the hoop and against each other. Spaced further out
    # they stopped reading as a tether and became litter falling off the bottom.
    orb(0.030, (0.0, 0.0, HOVER - orbit - 0.035), "core", subdivisions=1)
    orb(0.023, (0.0, 0.0, HOVER - orbit - 0.090), "core", subdivisions=1)

    return (0.0, 0.0, HOVER - orbit - 0.155), 0.13


DESIGNS = (
    ("stow_carrier_cradle", cradle),
    ("stow_carrier_crook", crook),
    ("stow_carrier_claw", claw),
    ("stow_carrier_yoke", yoke),
    ("stow_carrier_hoop", hoop),
)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        at, size = build()
        obj = finish(name)

        # Exported before the crate exists, so the mesh is the carrier alone.
        export(obj, name, VARIANTS)
        cargo(at, size)

        tint(GLOW)
        stage_scene()

        # Set well back and to the side. Resolution does not help here - Blender
        # fits its 36mm sensor to the longer image dimension, so a wider render at
        # the same lens crops the top instead of revealing more width. The cube fits
        # because the camera moved back and the lens came down, not because the
        # picture got bigger.
        reference_cube((-0.30, -1.00, 0.50))

        # Eye height, a couple of metres back. Not an orbit and not a hero angle:
        # this is seen by someone standing in their storage room watching it go past.
        bloom_setup(size=6, threshold=0.92)
        camera((-1.55, 2.45, 1.70), (0.0, 0.0, HOVER - 0.04), lens=46)
        render(os.path.join(PREVIEWS, name + ".png"), width=720, height=600, bloom=False)

        print("DESIGN_OK %s verts=%d tris=%d"
              % (name, len(obj.data.vertices), len(obj.data.polygons)))

    lineup()


def lineup():
    """
    All five in a row beside the post they belong to.

    The single shot that decides it. Judged apart, five glowing objects all look
    fine; judged in a row at the same scale against the actual piece, two of them
    turn out to be the same silhouette and one turns out to be too small to see.
    """
    clear_scene()

    spacing = 0.72
    for index, (name, build) in enumerate(DESIGNS):
        offset = (index - 2) * spacing

        before = set(bpy.data.objects)
        at, size = build()
        cargo(at, size)
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    finish("lineup")

    # Imported before tint(), or it stays the flat white of an untextured import and
    # takes over the frame. Same two axis settings it was exported with, which makes
    # the round trip exact.
    post = os.path.join(ASSETS, "stow_post_rack.obj")
    if os.path.exists(post):
        bpy.ops.wm.obj_import(filepath=post, forward_axis="Z", up_axis="Y")
        for obj in bpy.context.selected_objects:
            # Out past one end of the row and set back. Dead centre - where it was -
            # put a metre of timber directly in front of the middle design.
            obj.location += Vector((2.62, -1.50, 0.0))

    tint(GLOW)
    stage_scene()

    # The post at one end, the cube at the other, and the lens short enough that
    # both fit. The camera looks down -y here, so world +x lands on the left of the
    # frame - the row reads hoop, yoke, claw, crook, cradle from left to right.
    reference_cube((-2.62, -0.90, 0.50))

    bloom_setup(size=6, threshold=0.92)
    camera((0.0, 5.60, 1.70), (0.0, 0.0, 1.08), lens=33)
    render(os.path.join(PREVIEWS, "stow_carrier_lineup.png"),
           width=1400, height=600, bloom=False)
    print("DESIGN_OK stow_carrier_lineup")


main()
