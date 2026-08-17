"""
Bonemeal: candidates for the bag of ground bone you work into a crop.

    blender --background --python tools/bonemeal_designs.py

Four readings of the same thing, built to be compared at icon size, because at 48
pixels the outline is the whole of what tells one item from another in a row of
slots.

    heap    a low mound of meal with shards still in it
    horn    a hollowed horn on its side, spilling
    cake    a pressed brick, cracked across
    bundle  long bones bound together, one split open

**One material group, deliberately.** BonemealPrefab.Visual swaps the mesh onto the
donor's MeshFilter and leaves every component alone, so the renderer still carries
BoneFragments' single material - and OBJ groups map onto sharedMaterials in order,
which means a second group would draw with whatever happened to be bound last. That
is a constraint and it is also the right answer: ground bone wearing the game's own
bone texture matches by construction and survives its updates, which is exactly what
borrowing a material is for.

Modelled lying down rather than standing. An item prefab is placed face-up, and a
placement format carrying only yaw leaves anything built upright staring at the sky.

Nothing ships until one is picked: set WINNER and run again, and that one is written
out under the names BonemealPrefab looks for.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from mathutils import Vector

from vhbuild import (box, clear_scene, export, finish, limb, orb, render,
                     shell, taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "assets")
ASSETS = os.path.join(SHIPPED, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# None until there is a pick. Naming one here is what promotes it out of variants,
# which the build does not copy, into assets, which it does.
WINNER = None
SHIPPED_MESH = "grove_bonemeal"
SHIPPED_ICON = "grove_bonemeal_icon.png"

BONE = "bone"


# --------------------------------------------------------------------------- shapes

def heap():
    """
    A mound of meal with shards standing out of it.

    First pass buried the shards and the whole thing rendered as a grey potato. The
    shards have to *break the outline* to do their job - a lump with texture on it is
    still a lump at 48 pixels, and bone is only bone if you can see a bone.
    """
    mat = BONE

    orb(0.098, (0.000, 0.000, 0.004), mat, subdivisions=2, stretch=0.40)
    orb(0.062, (0.056, -0.026, 0.002), mat, subdivisions=2, stretch=0.38)
    orb(0.054, (-0.054, 0.028, 0.002), mat, subdivisions=2, stretch=0.36)

    # Standing well proud of the pile, and leaning, so the silhouette has spikes in
    # it. Rooted deep enough to overlap the domes - a shard resting on top reads as a
    # separate object dropped there.
    taper(0.017, 0.009, 0.120, (0.030, 0.030, 0.052), mat, sides=5,
          rot_x=64.0, rot_y=22.0)
    taper(0.014, 0.008, 0.098, (-0.038, -0.030, 0.046), mat, sides=5,
          rot_x=-58.0, rot_y=-16.0)
    box((0.092, 0.020, 0.015), (0.006, 0.052, 0.050), mat, rot_z=16.0, rot_y=26.0)


def horn():
    """
    A hollowed horn on its side, spilling.

    Two passes went wrong on one mistake worth writing down: rot_x=90 lays a cylinder
    along **y**, and the segments were being spaced along **x** - so what was meant to
    be one tube end to end came out as three parallel tubes side by side, with
    daylight between them. Laying along x is rot_y=90. The bundle below was right the
    whole time for exactly that reason.
    """
    mat = BONE

    # The mouth, open, and the only part that needs to be a shell.
    shell(0.056, 0.050, 0.058, (-0.092, 0.000, 0.048), mat, sides=11, rot_x=90.0)

    # Body and tip, each overlapping the one before by about a third of its length so
    # the three are one tapering object rather than three touching ones.
    taper(0.030, 0.052, 0.128, (-0.014, 0.004, 0.046), mat, sides=11, rot_y=90.0)
    taper(0.010, 0.030, 0.092, (0.082, 0.012, 0.042), mat, sides=9,
          rot_y=90.0, rot_x=10.0)

    # The spill, running out of the mouth and widening away from it.
    orb(0.044, (-0.124, -0.014, 0.015), mat, subdivisions=2, stretch=0.30)
    orb(0.030, (-0.158, -0.030, 0.010), mat, subdivisions=2, stretch=0.24)


def cake():
    """
    A pressed brick, cracked across.

    The flattest and most man-made of the four, and the only one with a straight edge
    - which is why it is here, because in a row of slots a rectangle is instantly not
    a lump. First pass sat a near-spherical cap on top and it read as a gem on a
    paving slab; the cap is now barely proud of the brick, which is what pressed
    powder actually looks like.
    """
    mat = BONE

    box((0.092, 0.146, 0.048), (-0.050, 0.000, 0.024), mat, rot_z=1.6)
    box((0.088, 0.146, 0.045), (0.048, 0.004, 0.022), mat, rot_z=-2.2)

    # Low and wide. A cap you can see the edge of is a lid; this is a surface.
    orb(0.084, (0.000, 0.000, 0.044), mat, subdivisions=2, stretch=0.10)

    # A shard through the crack, so it is bone rather than chalk.
    taper(0.013, 0.007, 0.086, (0.002, 0.020, 0.062), mat, sides=5,
          rot_x=72.0, rot_y=8.0)

    orb(0.028, (0.006, -0.088, 0.010), mat, subdivisions=2, stretch=0.30)
    box((0.024, 0.019, 0.013), (-0.022, 0.088, 0.008), mat, rot_z=24.0)


def bundle():
    """
    Long bones bound together, one split and spilling.

    First pass used limb() chains and they came out as flat stacked plates - a curved
    tapering chain at 2cm thick over 20cm is mostly bevel, and the join collapsed it.
    Explicit cylinders with knuckles at both ends are cruder and read correctly, which
    is the trade every time.
    """
    mat = BONE

    def shaft(y, z, length, thick, lean):
        # Laid along x: rot_y=90 turns an upright cylinder onto its side. rot_x would
        # lay it along y instead, which is the mistake to make here.
        taper(thick, thick * 0.86, length, (0.000, y, z), mat, sides=7,
              rot_y=90.0, rot_x=lean)
        orb(thick * 1.5, (-length * 0.5, y, z), mat, subdivisions=2, stretch=0.86)
        orb(thick * 1.4, (length * 0.5, y, z), mat, subdivisions=2, stretch=0.84)

    shaft(0.030, 0.024, 0.210, 0.022, 3.0)
    shaft(-0.016, 0.026, 0.196, 0.020, -4.0)
    shaft(0.008, 0.062, 0.178, 0.019, 1.5)

    # The binding: a band wrapping the bundle, which is a short open cylinder laid
    # along the same axis as the shafts. The first pass used an upright box and it
    # read as a fin standing through them - a slab across a bundle is not a binding,
    # it is a signpost. Only the band's outer wall is ever seen, so the shafts sitting
    # inside it cost nothing.
    shell(0.062, 0.062, 0.034, (0.012, 0.006, 0.038), mat, sides=13, rot_y=90.0)


DESIGNS = (
    ("heap", heap),
    ("horn", horn),
    ("cake", cake),
    ("bundle", bundle),
)


# --------------------------------------------------------------------------- scene

def icon_scene(obj):
    """
    Orthographic, three quarters on, transparent, fitted.

    Three quarters rather than front on, which the sapling icon already paid for:
    dead front on flattens a small object into a symmetrical blob, and an icon's
    whole job is an outline you can tell from its neighbours in a grid.

    Its own lighting, too. Suns rather than area lights - an area light a metre from
    a 20cm object blows every channel to white, and the tell is bone rendering as
    flat paper.
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

    azimuth, elevation, distance = math.radians(34.0), math.radians(24.0), 1.6

    bpy.ops.object.camera_add(location=(
        centre[0] + math.sin(azimuth) * math.cos(elevation) * distance,
        centre[1] - math.cos(azimuth) * math.cos(elevation) * distance,
        centre[2] + math.sin(elevation) * distance))

    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span * 1.16

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(-0.6, -1.0, 0.7))
    key = bpy.context.active_object
    key.data.energy = 2.6
    key.rotation_euler = (math.radians(56.0), 0.0, math.radians(-34.0))

    bpy.ops.object.light_add(type="SUN", location=(0.8, -0.9, -0.3))
    fill = bpy.context.active_object
    fill.data.energy = 1.1
    fill.rotation_euler = (math.radians(104.0), 0.0, math.radians(36.0))

    world = bpy.data.worlds.new("icon_world")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.0


def build(label, maker):
    name = "grove_bonemeal_" + label
    winner = label == WINNER

    clear_scene()
    maker()
    obj = finish(name)
    export(obj, name, ASSETS)
    tris = len(obj.data.polygons)

    # The winner is exported a second time under the shipping name, so the item you
    # hold and the picture in the slot are the same geometry and cannot drift.
    if winner:
        export(obj, SHIPPED_MESH, SHIPPED)

    clear_scene()
    maker()
    obj = finish(name)

    tint(strength=0.9)
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
