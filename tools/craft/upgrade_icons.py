"""
Build-menu icons for the three stowing-post upgrades.

    blender --background --python tools/craft/upgrade_icons.py

The rig is post_icon.py's, unchanged, and that is deliberate rather than lazy: these
three pieces stand in the same build menu as the post itself, and an icon shot under
different lights next to one shot under these reads as a different mod's piece. The
one thing that is not shared is the list of models - post_icon.py globs stow_post_*
and would re-shoot the post's own icons on every run, which is how a rendering tweak
meant for an upgrade quietly changes the piece nobody was working on.

Why a separate file at all rather than a flag on post_icon.py: these come out of
assets/ under their SHIPPING names (stow_rail, stow_perch, hod_jib), not out of
assets/variants/ where the candidates live. variants/ is deliberately excluded from
the csproj's deploy glob, so a model only earns a name here once it has been picked.

The icons are committed; the preview renders beside them are not. They are
regenerable from this script and the .obj is the real source.
"""

import os
import sys
import math

# tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# tools/ on the path for vhbuild, exactly as the other scripts do it.
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy

from vhbuild import clear_scene, tint

ASSETS = os.path.join(ROOT, "assets")

# 128, not 64: a 64px source is already soft the moment the UI scales at all, and the
# file is two kilobytes either way.
SIZE = 128

# Turned rather than photographed square-on. A piece is a thing you are about to
# place, and every one of these is essentially a frame of uprights - dead front-on,
# the rail is a line and the jib is a cross.
YAW = 25.0

MODELS = ["stow_rail", "stow_perch", "hod_jib"]


def stage(radius):
    """Front-on, orthographic, transparent, square. Its own lights, not a preview's."""
    scene = bpy.context.scene
    scene.render.film_transparent = True

    bpy.ops.object.camera_add(location=(0.0, 2.93, radius * 0.62))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"

    # Framed off the model's own size, so the squat rail and the tall jib both fill
    # the slot instead of one of them rattling around in it.
    cam.data.ortho_scale = radius * 2.55
    cam.rotation_euler = (math.radians(78.0), 0.0, math.radians(180.0))
    scene.camera = cam

    # Suns, and gentle ones. Area lights close to a small object blow every channel
    # to white - the tell is dark brown rendering as pale beige.
    bpy.ops.object.light_add(type="SUN", location=(-1.6, 2.0, 1.6))
    key = bpy.context.active_object
    key.data.energy = 2.8
    key.rotation_euler = (math.radians(56.0), 0.0, math.radians(-148.0))

    bpy.ops.object.light_add(type="SUN", location=(1.8, 1.8, -0.6))
    fill = bpy.context.active_object
    fill.data.energy = 1.0
    fill.rotation_euler = (math.radians(106.0), 0.0, math.radians(218.0))

    world = bpy.data.worlds.new("icon")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.62, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.35


def frame(objects):
    """
    Centres the model on the origin and reports how big it is.

    Measured after the yaw is applied: a box turned 25 degrees is wider than the box
    was, and framing off the unturned bounds clips both front corners.
    """
    from mathutils import Vector

    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]

    for obj in objects:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                lo[axis] = min(lo[axis], world[axis])
                hi[axis] = max(hi[axis], world[axis])

    centre = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
    for obj in objects:
        obj.location.x -= centre[0]
        obj.location.y -= centre[1]
        obj.location.z -= centre[2]

    # Half the larger of width and height. Depth is ignored on purpose - an
    # orthographic camera does not care how deep a thing is, and counting it renders
    # a deep piece small.
    return max(hi[0] - lo[0], hi[2] - lo[2]) * 0.5


def main():
    for name in MODELS:
        path = os.path.join(ASSETS, name + ".obj")
        if not os.path.exists(path):
            print("ICON_FAIL %s is not in assets" % name)
            continue

        clear_scene()

        # The same two axis settings the models were exported with, which makes the
        # round trip exact - winding untouched, nothing mirrored.
        bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        if not imported:
            print("ICON_FAIL %s imported nothing" % name)
            continue

        tris = sum(len(o.data.polygons) for o in imported)

        for obj in imported:
            # Added to, never assigned. The importer carries the Y-up to Z-up
            # conversion on the object's own rotation, so setting rotation_euler
            # outright throws it away and lays the piece on its back - which renders
            # as a featureless slab, because what you are looking at is its underside.
            obj.rotation_euler.z += math.radians(YAW)

        bpy.context.view_layer.update()
        radius = frame(imported)

        tint()
        stage(radius)

        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE_NEXT"

        # Standard, not AgX. AgX rolls bright values towards white, and an icon is
        # judged on whether its colours match the game's palette.
        try:
            scene.view_settings.view_transform = "Standard"
        except TypeError:
            pass

        scene.render.resolution_x = SIZE
        scene.render.resolution_y = SIZE
        scene.render.film_transparent = True
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.filepath = os.path.join(ASSETS, name + "_icon.png")
        bpy.ops.render.render(write_still=True)

        print("ICON_OK %s radius=%.2f faces=%d" % (name, radius, tris))


main()
