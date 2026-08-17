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
WINNER = "crock"
SHIPPED_MESH = "grove_bonemeal"
SHIPPED_ICON = "grove_bonemeal_icon.png"

BONE = "bone"


# --------------------------------------------------------------------------- shapes

def sack():
    """
    A cinched cloth sack with two bone shards pushed into it.

    Fourth attempt, and the lesson from the three before it is arithmetic rather than
    art: every failure was a part floating clear of the body. Gathered corners half a
    centimetre above the neck, a bone whose end cap sat outside the sack, a knot
    resting on air. At this scale "touching" is not enough - a 2mm gap is plainly
    visible at 512px and reads as debris orbiting the object.

    So this one is deliberately fewer, larger, heavily overlapping parts, and every z
    span below is written down so the overlaps can be checked rather than eyeballed:

        base orb    0.000 - 0.073
        body orb    0.000 - 0.123
        neck        0.070 - 0.140   (53mm into the body)
        tie         0.125 - 0.139   (inside the neck)
        gather      0.125 - 0.165   (40mm through the tie)

    Few large parts beat many small ones, which was the rule the whole time.
    """
    mat = BONE

    # Body: off-centre so it slumps rather than standing like a jar.
    orb(0.080, (0.012, -0.006, 0.055), mat, subdivisions=2, stretch=0.85)
    orb(0.068, (-0.018, 0.012, 0.032), mat, subdivisions=2, stretch=0.60)

    # One fold plane sunk deep into the body, breaking the curve without adding a
    # part that can come adrift.
    box((0.026, 0.104, 0.070), (0.044, -0.010, 0.058), mat, rot_z=26.0, rot_y=-10.0)

    # Neck, driven 53mm down into the body.
    taper(0.055, 0.026, 0.070, (0.006, 0.000, 0.105), mat, sides=9, spin=False)

    # The tie, sitting inside the neck's span.
    taper(0.031, 0.031, 0.014, (0.006, 0.000, 0.132), mat, sides=11, spin=False)

    # Gathered cloth above it, passing right through the tie rather than perching on
    # top of it, and squared off so it does not read as a cork.
    taper(0.024, 0.048, 0.040, (0.006, 0.000, 0.145), mat, sides=7, spin=False)
    box((0.052, 0.024, 0.020), (0.010, 0.004, 0.160), mat, rot_z=22.0, rot_y=9.0)

    # Shards, rooted at the centre of the body so no amount of rotation can lift the
    # buried end out of it.
    taper(0.016, 0.009, 0.150, (0.020, 0.010, 0.060), mat, sides=7,
          rot_x=36.0, rot_y=34.0)
    taper(0.013, 0.008, 0.120, (0.000, 0.006, 0.055), mat, sides=5,
          rot_x=-42.0, rot_y=-26.0)


def crock():
    """
    A wide-mouthed crock with cloth tied over the mouth and a bone leaning on it.

    The picked shape, and the reason it won is that it is the only candidate that
    reads as one coherent object. Three passes at a cloth sack all came out as
    pottery anyway - stacking primitives on a shared vertical axis produces a surface
    of revolution, which is exactly what a thrown pot is - so the honest move was to
    build the pot on purpose rather than keep failing to avoid it.

    It is also the only shape here with a straight vertical side, which is what makes
    it findable in a row of slots full of curves.

    Every z span is written down, because the recurring failure across five passes was
    never the design, it was a part half a centimetre clear of its neighbour:

        lower body   0.002 - 0.050
        upper body   0.040 - 0.100   (10mm overlap)
        lip          0.091 - 0.109   (9mm into the body)
        cord         0.099 - 0.110   (inside the lip)
        cover        0.097 - 0.127   (12mm over the lip, overhanging the cord)
    """
    mat = BONE

    # Straight-sided with a slight belly, sitting flat. Odd side count so it reads as
    # round rather than presenting a flat face to the camera.
    taper(0.076, 0.084, 0.048, (0.000, 0.000, 0.026), mat, sides=13, spin=False)
    taper(0.084, 0.072, 0.060, (0.000, 0.000, 0.070), mat, sides=13, spin=False)

    # A lip for the cord to bite on. Without it the cloth is tied to nothing and the
    # cover slides visually off the top.
    taper(0.072, 0.078, 0.018, (0.000, 0.000, 0.100), mat, sides=13, spin=False)

    # The cloth: thin and overhanging, not a dome. The previous pass used stretch=0.30
    # and it read as a mushroom cap, which is to say as a lid - a hard thing, which is
    # the one thing cloth must not look like.
    orb(0.088, (0.000, 0.000, 0.112), mat, subdivisions=2, stretch=0.17)

    # The cord, proud of the lip and tucked under the cover's overhang.
    taper(0.080, 0.080, 0.011, (0.000, 0.000, 0.104), mat, sides=13, spin=False)

    # Two cloth ends hanging down the side from under the cover, overlapping both it
    # and the body so they are part of the object rather than tabs stuck on it.
    box((0.024, 0.015, 0.044), (0.076, 0.016, 0.090), mat, rot_y=13.0, rot_z=8.0)
    box((0.018, 0.013, 0.032), (0.066, -0.042, 0.094), mat, rot_y=-9.0, rot_z=-26.0)

    # The bone, rooted well inside the wall so no rotation can lift its buried end
    # out. This is what stops the whole thing being a generic pot.
    #
    # On the +x -y side deliberately. The icon camera stands at azimuth 34 degrees,
    # so anything on the far side is behind the pot in the one picture that has to
    # carry the item's identity - and the first version of this put it there, where it
    # was completely hidden. Same mistake as photographing a piece from behind.
    taper(0.016, 0.009, 0.130, (0.046, -0.030, 0.058), mat, sides=7,
          rot_x=-26.0, rot_y=36.0)

    # One shard at the foot, overlapping the base, so the identity reads even when the
    # leaning bone is hidden behind the pot at an awkward angle.
    box((0.050, 0.014, 0.011), (0.048, -0.044, 0.007), mat, rot_z=28.0)


DESIGNS = (
    ("crock", crock),
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
