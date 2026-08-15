"""
Icon candidates for the ancient sapling piece.

    blender --background --python tools/sapling_icon.py

The cultivator showed a carrot, because SaplingPrefab.Dress set the name, the
description and the cost but never Piece.m_icon - so the clone kept the donor's
picture. Piece.m_icon is a Sprite, and Valheim builds those from a camera rig in
the editor that does not exist at runtime, so the answer is the same as the
heartwood's: a PNG beside the dll and Sprite.Create over it.

Nothing is modelled here. The four stage meshes already ship in assets/, and
re-modelling the sapling for its own icon would mean two shapes that have to be
kept in step by hand and will not be. Importing the .obj means the icon is by
construction the object you plant - and CLAUDE.md's round trip holds: these were
exported forward_axis="Z", up_axis="Y", and importing with those same two lands
back in Blender's space with winding untouched.

Three candidates, because the honest answer and the legible answer are not
obviously the same one:

    stage 1  what you actually place. A split seed on a mound, and at 48 pixels
             possibly just a pebble.
    stage 3  waist high with the pod closed. A plant with something in it.
    stage 4  the open shell and its four staves. The most silhouette by far,
             and a picture of what you are working towards rather than of what
             the cultivator is about to put down.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from mathutils import Vector

from vhbuild import clear_scene, render, tint

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

# Which stages are worth offering. Stage 2 is left out on purpose: it is stage 3
# with less on it, and two candidates sharing an outline means there is only one
# candidate.
CANDIDATES = (1, 3, 4)


def load_stage(number):
    """
    Bring one stage mesh in, and hand back the object.

    The two axis settings are the exact inverse of the export in vhbuild, so the
    sapling comes back standing up in Blender's Z rather than lying on its face.
    """
    path = os.path.join(ASSETS, "grove_sapling_%d.obj" % number)
    if not os.path.exists(path):
        raise SystemExit("missing %s - run grove_designs.py first" % path)

    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
    fresh = [o for o in bpy.data.objects if o not in before]

    if not fresh:
        raise SystemExit("nothing imported from %s" % path)

    obj = fresh[0]
    bpy.context.view_layer.objects.active = obj
    return obj


def bounds(obj):
    """
    World-space min and max corners.

    Through matrix_world rather than off bound_box directly: the import carries a
    transform, and vhbuild's own note about join() adopting a transform is the same
    trap - anything measured in local space is nonsense until it is converted.
    """
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]

    lo = [min(c[i] for c in corners) for i in range(3)]
    hi = [max(c[i] for c in corners) for i in range(3)]
    return lo, hi


def frame_camera(obj, crop=1.0):
    """
    Orthographic, three quarters on, and fitted to whatever it was handed.

    **Not front on**, which is what CLAUDE.md's icon rule says and what the
    heartwood does. That rule was paid for by a 20cm billet, and it does not
    survive being applied to a two metre plant: dead front on, the sapling's two
    side limbs come out level and symmetrical and the whole thing reads as a
    scarecrow with its arms out. Thirty-five degrees round breaks the symmetry,
    puts the limbs at different lengths, and lets the pod show its depth instead
    of presenting as a flat lampshade.

    Fitted rather than given a number per stage: stage 1 is about 45cm tall and
    stage 4 is nearly two metres, and a shared scale would draw the first as a
    speck. Every icon gets the same square, so each one earns the whole square.

    crop < 1 frames the top of the piece rather than all of it. An icon is a
    symbol and does not owe the object a full-length portrait - most of a grown
    sapling is trunk, and trunk is the least identifying part of it.
    """
    lo, hi = bounds(obj)

    centre_x = (lo[0] + hi[0]) * 0.5
    centre_y = (lo[1] + hi[1]) * 0.5
    height = hi[2] - lo[2]

    span = max(hi[0] - lo[0], height) * crop
    centre_z = hi[2] - (height * crop) * 0.5 if crop < 1.0 else (lo[2] + hi[2]) * 0.5

    target = bpy.data.objects.new("icon_aim", None)
    bpy.context.collection.objects.link(target)
    target.location = (centre_x, centre_y, centre_z)

    # Azimuth off the front, and a little above eye level so the pod is read from
    # slightly over rather than edge on.
    azimuth = math.radians(35.0)
    elevation = math.radians(14.0)
    distance = max(span * 3.0, 2.0)

    bpy.ops.object.camera_add(location=(
        centre_x + math.sin(azimuth) * math.cos(elevation) * distance,
        centre_y - math.cos(azimuth) * math.cos(elevation) * distance,
        centre_z + math.sin(elevation) * distance))

    cam = bpy.context.active_object
    cam.data.type = "ORTHO"

    # A tenth of margin so nothing touches the edge of the square.
    cam.data.ortho_scale = max(span * 1.12, 0.05)

    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"

    bpy.context.scene.camera = cam


def light_for_icon():
    """
    Suns, and gentle ones, on a transparent film.

    Learned on the heartwood icon and not worth learning twice: area lights at
    energy 90 a metre from a small object blow every channel to white, and the
    tell is dark brown rendering as pale beige. A sun has no falloff to get wrong.

    The key comes from the left and slightly above, which is where Valheim's own
    icons are lit from, so a grid of them does not look like it was assembled from
    two different sets.
    """
    scene = bpy.context.scene
    scene.render.film_transparent = True

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


def build(label, number, crop=1.0):
    clear_scene()

    obj = load_stage(number)

    # Barely any emission, and less than the heartwood's 0.9. At that strength the
    # amber clipped straight to pale yellow against the transparent film, which
    # reads as the core being a bulb rather than as the exposure being hot. An icon
    # has to keep its hue - the glow is implied by the colour, not by the channel.
    tint(strength=0.55)

    frame_camera(obj, crop)
    light_for_icon()

    # 128, not 64. Valheim scales icons down and a sharp source survives that far
    # better than one rendered at the size it will be shown at.
    out = os.path.join(PREVIEWS, "sapling_icon_%s.png" % label)
    render(out, width=128, height=128, bloom=False)
    print("DESIGN_OK sapling_icon_%s" % label)

    # And a big one, purely to look at. The 128 is the real thing; this is so a
    # silhouette can be judged without squinting at a thumbnail.
    big = os.path.join(PREVIEWS, "sapling_icon_%s_large.png" % label)
    render(big, width=512, height=512, bloom=False)


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    # Whole-piece candidates, one per stage worth offering.
    for number in CANDIDATES:
        build("stage%d" % number, number)

    # And the pod on its own, off the two stages that have one. Framing on the top
    # third is the only way the amber core gets enough pixels to survive being
    # drawn at inventory size, and the pod is the identifying part - a trunk with
    # two limbs is every plant in the game.
    build("pod3", 3, crop=0.42)
    build("pod4", 4, crop=0.42)


main()
