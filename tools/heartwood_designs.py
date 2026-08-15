"""
Heartwood: candidates for the thing the spirit folds itself into.

    blender --background --python tools/heartwood_designs.py

The shipped one is a split billet with a lit seam - two halves held apart and the
gold showing between them. It was designed when the heartwood was the spirit's
*heart*, taken out of it. The heartwood is now its *home*, which is close to the
opposite: the thing should read as closed, occupied, and carrying something,
rather than as opened and emptied. At 48 pixels the shipped one also reads as two
brown blobs either side of a pale card, which is the second reason to redo it.

Each candidate below is one closed mass with light escaping from inside it, and
each answers "how does the light get out" differently, because that is the only
part of the silhouette there is room to differentiate at icon size.

    knot    a burl. Lumpy, solid, cracked, light in the cracks.
    pod     a ribbed seed case with one lit band around its waist.
    nest    bark strands wrapped over a glow, light between the wraps.
    lantern a cupped shell with the light sitting visibly in the cup.

These build the world mesh too, not only the icon - the item you hold and the
picture in the slot are rendered from the same geometry, and they must not drift.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from mathutils import Vector

from vhbuild import (box, clear_scene, export, finish, orb, render, ring, shell,
                     taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "assets")
ASSETS = os.path.join(SHIPPED, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The one that ships, under the names HeartwoodPrefab looks for. Everything else
# stays in variants, which the build does not copy.
#
# This supersedes item_designs.py, which built the split billet back when the
# heartwood was the spirit's heart rather than its home. That script is kept for
# its reasoning about icon legibility, all of which still holds.
WINNER = "nest_woven"
SHIPPED_MESH = "grove_heartwood"
SHIPPED_ICON = "grove_heartwood_icon.png"


# --------------------------------------------------------------------------- shapes

def knot():
    """
    A burl: a lump of overgrown grain with the light showing through its splits.

    Three overlapping masses rather than one, so the outline is irregular enough to
    read as grown. The splits are wedges pushed proud of the surface - set flush
    they were swallowed by the mass at any size worth having.
    """
    orb(0.115, (0.0, 0.0, 0.0), "bark", subdivisions=2, stretch=1.12)
    orb(0.086, (0.052, 0.018, 0.030), "bark", subdivisions=2, stretch=0.95)
    orb(0.070, (-0.048, -0.020, -0.028), "bark", subdivisions=2, stretch=1.05)

    # Three splits, large and few. A scatter of thin cracks is mud below about six
    # pixels; three fat ones survive being drawn in a 48 pixel slot.
    box((0.030, 0.150, 0.062), (0.020, -0.020, 0.052), "core", rot_x=8.0, rot_y=14.0)
    box((0.026, 0.140, 0.050), (-0.058, -0.030, -0.010), "core", rot_y=-22.0)
    box((0.024, 0.120, 0.044), (0.078, -0.024, -0.038), "core", rot_x=-12.0)


def pod():
    """
    A seed case: ribbed, closed at both ends, one lit band round its waist.

    The only candidate whose light is a single continuous shape, which makes it the
    most legible of the four and the least interesting. Worth having for exactly
    that reason - an icon that survives being tiny beats one that is clever.
    """
    taper(0.088, 0.030, 0.150, (0.0, 0.0, 0.078), "bark", sides=7)
    taper(0.088, 0.026, 0.130, (0.0, 0.0, -0.068), "bark", sides=7, rot_x=180.0)

    # The lit waist, wider than the case so it is never occluded by it.
    orb(0.098, (0.0, 0.0, 0.006), "core", subdivisions=2, stretch=0.34)

    # Five ribs, running the long way. Odd count so the silhouette never presents a
    # flat pair of parallel edges.
    for i in range(5):
        angle = math.radians(72.0 * i + 9.0)
        box((0.024, 0.024, 0.230),
            (math.cos(angle) * 0.072, math.sin(angle) * 0.072, 0.004), "bark",
            rot_x=math.sin(angle) * 5.0, rot_y=-math.cos(angle) * 5.0)


def band(radius, thickness, rot, mat="bark", major=13, minor=5):
    """
    One wrap round the mass, as a real torus rotated onto an arbitrary axis.

    ring() takes only rot_x, and a set of bands sharing one axis is a stack of
    hoops rather than a weave, so the euler is set afterwards. Straight boxes were
    the first attempt at this and they are the reason the old nest failed: a
    straight box laid across a sphere is a stick balanced on a ball, and four of
    them are four sticks. A torus is the only shape that reads as going *round*
    something.
    """
    obj = ring(radius, thickness, (0.0, 0.0, 0.0), mat, major=major, minor=minor)
    obj.rotation_euler = (math.radians(rot[0]), math.radians(rot[1]),
                          math.radians(rot[2]))
    return obj


def nest_wrap():
    """
    Bound: a small light with four fat bands wrapped round and over it.

    The fix for the first nest, which put a 0.092 core inside 0.048 bands - the
    glow was two thirds of the silhouette and the wrapping was trim on it, so it
    read as a glowing ball with debris stuck to it. Here the mass is 0.062 and the
    bands are 0.030 thick on a 0.104 radius, which puts the wood outside the light
    on every axis and lets the glow show only where the bands do not cover.
    """
    orb(0.062, (0.0, 0.0, 0.0), "core", subdivisions=2, stretch=1.06)

    for rot in ((90.0, 0.0, 0.0), (90.0, 0.0, 58.0),
                (64.0, 34.0, 116.0), (112.0, -28.0, 24.0)):
        band(0.104, 0.030, rot)

    # Two ends left loose and pointing out, which is what separates "wrapped" from
    # "banded" - a parcel has tails, a barrel has hoops.
    taper(0.030, 0.012, 0.110, (0.108, -0.026, 0.062), "bark", sides=5, rot_y=52.0)
    taper(0.026, 0.010, 0.096, (-0.104, 0.030, -0.058), "bark", sides=5, rot_y=-44.0)


def nest_woven():
    """
    Rougher: six thinner wraps at scattered angles, more twig than binding.

    Nearer the word "nest" than nest_wrap is - this is material heaped and tangled
    rather than deliberately tied. Six is the ceiling: at eight the gaps close and
    it becomes a dark ball with no light in it at all, which is the failure mode at
    the opposite end from the first attempt.
    """
    orb(0.058, (0.0, 0.0, 0.0), "core", subdivisions=2, stretch=1.10)

    for rot in ((90.0, 0.0, 0.0), (78.0, 18.0, 44.0), (104.0, -22.0, 88.0),
                (62.0, 40.0, 132.0), (118.0, -36.0, 20.0), (86.0, 12.0, 158.0)):
        band(0.100 + (rot[2] % 17.0) * 0.0009, 0.019, rot, major=11, minor=5)

    for x, y, z, yaw in ((0.104, -0.024, 0.058, 48.0), (-0.098, 0.032, -0.052, -40.0),
                         (0.030, 0.100, -0.070, 14.0)):
        taper(0.024, 0.009, 0.100, (x, y, z), "bark", sides=5, rot_y=yaw)


def nest_cup():
    """
    An actual nest: a woven bowl with the light sitting down in it.

    The literal reading of the word, and the only one where the glow is on top
    rather than inside - which makes it the most legible of the three at inventory
    size, because the bright part is unobstructed. The cost is that it is a bowl
    with something in it rather than something housed, so it says "carried" where
    the other two say "kept".
    """
    # The bowl: three bands of decreasing radius stacked and squashed, so the walls
    # lean inwards the way a woven nest does.
    for i, (radius, height) in enumerate(((0.116, -0.060), (0.104, -0.020),
                                          (0.096, 0.014))):
        obj = ring(radius, 0.026, (0.0, 0.0, height), "bark", major=13, minor=5,
                   rot_x=0.0)
        obj.scale = (1.0, 1.0, 0.62)
        obj.rotation_euler = (0.0, 0.0, math.radians(24.0 * i))

    # A base, so it is a bowl rather than three hoops with a gap under them.
    orb(0.092, (0.0, 0.0, -0.078), "bark", subdivisions=2, stretch=0.46)

    # The occupant, sitting in the cup and proud of the rim.
    orb(0.072, (0.0, 0.0, 0.046), "core", subdivisions=2, stretch=1.12)

    # Two twigs standing off the rim, so the silhouette has something above the
    # light instead of ending flat at it.
    taper(0.022, 0.008, 0.092, (0.086, -0.020, 0.070), "bark", sides=5, rot_y=34.0)
    taper(0.020, 0.008, 0.078, (-0.078, 0.034, 0.062), "bark", sides=5, rot_y=-28.0)


def lantern():
    """
    A cupped shell with the light sitting openly in it.

    The one that breaks the brief on purpose - it is not closed, and the glow is not
    escaping so much as being carried. Kept in the set because "a home" does not
    have to mean "sealed", and a cupped hand is a stronger read of custody than a
    wrapped parcel is.
    """
    shell(0.118, 0.096, 0.150, (0.0, 0.0, -0.030), "bark", sides=9)

    # The base, so the cup is not a tube seen from the side.
    orb(0.110, (0.0, 0.0, -0.098), "bark", subdivisions=2, stretch=0.52)

    # The occupant, sitting proud of the rim.
    orb(0.082, (0.0, 0.0, 0.062), "core", subdivisions=2, stretch=1.18)

    # Two staves rising past the rim, which is what makes it a vessel rather than a
    # bowl - and gives the silhouette something above the light.
    for side in (-1.0, 1.0):
        box((0.028, 0.030, 0.130), (side * 0.092, 0.010, 0.052), "bark",
            rot_y=side * -14.0)


# The nest won, so this pass is three of it. The others stay so the pick can be
# judged against what it beat rather than against nothing.
DESIGNS = (
    ("nest_wrap", nest_wrap),
    ("nest_woven", nest_woven),
    ("nest_cup", nest_cup),
    ("knot", knot),
    ("pod", pod),
    ("lantern", lantern),
)


def icon_scene(obj):
    """
    Orthographic, three quarters on, transparent, fitted.

    Three quarters rather than front on, which is the lesson the sapling icon
    already paid for: dead front on flattens a small object into a symmetrical
    blob, and the whole job of an icon is an outline you can tell from its
    neighbours in a grid.
    """
    scene = bpy.context.scene
    scene.render.film_transparent = True

    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = [min(c[i] for c in corners) for i in range(3)]
    hi = [max(c[i] for c in corners) for i in range(3)]

    centre = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
    span = max(hi[i] - lo[i] for i in range(3))

    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = centre

    azimuth, elevation, distance = math.radians(34.0), math.radians(17.0), 1.6

    bpy.ops.object.camera_add(location=(
        centre[0] + math.sin(azimuth) * math.cos(elevation) * distance,
        centre[1] - math.cos(azimuth) * math.cos(elevation) * distance,
        centre[2] + math.sin(elevation) * distance))

    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span * 1.14

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    scene.camera = cam

    # Suns, not area lights. Area lights a metre from a 20cm object blow every
    # channel to white and the tell is dark brown rendering as pale beige.
    bpy.ops.object.light_add(type="SUN", location=(-0.6, -1.0, 0.7))
    key = bpy.context.active_object
    key.data.energy = 2.8
    key.rotation_euler = (math.radians(56.0), 0.0, math.radians(-34.0))

    bpy.ops.object.light_add(type="SUN", location=(0.8, -0.9, -0.3))
    fill = bpy.context.active_object
    fill.data.energy = 1.0
    fill.rotation_euler = (math.radians(104.0), 0.0, math.radians(36.0))

    world = bpy.data.worlds.new("icon_world")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.0


def build(label, maker):
    name = "grove_heartwood_" + label
    winner = label == WINNER

    clear_scene()
    maker()
    obj = finish(name)
    export(obj, name, ASSETS)
    tris = len(obj.data.polygons)

    # The winner is exported a second time under the shipping name, so the item you
    # hold and the picture in the slot are the same geometry and cannot drift apart.
    if winner:
        export(obj, SHIPPED_MESH, SHIPPED)

    clear_scene()
    maker()
    obj = finish(name)

    # Half strength, so the amber stays amber. At full it clips to a pale cream and
    # stops reading as a colour at all, which is what the shipped icon does.
    tint(strength=0.5)
    icon_scene(obj)

    render(os.path.join(PREVIEWS, name + ".png"), width=128, height=128, bloom=False)
    render(os.path.join(PREVIEWS, name + "_large.png"), width=512, height=512,
           bloom=False)

    # 128, not 64. Valheim scales icons down and a sharp source survives that better
    # than one rendered at the size it will be shown at.
    if winner:
        render(os.path.join(SHIPPED, SHIPPED_ICON), width=128, height=128, bloom=False)

    print("DESIGN_OK %s tris=%d%s" % (name, tris, " [SHIPPED]" if winner else ""))


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for label, maker in DESIGNS:
        build(label, maker)


main()
