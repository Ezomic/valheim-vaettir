"""
Build-menu icons for the stowing post.

    blender --background --python tools/post_icon.py

The post is cloned from piece_chest_wood, and a clone brings the donor's Piece.m_icon
with it - so until now the hammer's Furniture tab showed a wooden chest. The piece in
the world has been hand-modelled since d178ca4; only its picture was still the
donor's, which is the worst of both: it looks like a chest you already have, so there
is no reason to click it.

Valheim renders its own piece icons from a camera rig in the editor, and there is no
editor at runtime. A PNG beside the dll and Sprite.Create over it is the whole answer.

The rig is the one item_designs.py established, scaled from a 25cm item to a 1m piece:
orthographic, transparent film, two gentle suns and its own exposure. The preview
pass's lighting and ground plane are both wrong for an icon - a ground plane in an
inventory slot is a grey bar across the bottom, and the sky behind it makes a postage
stamp of the thing.

One deviation, and it is deliberate: the model is yawed 25 degrees rather than shown
square-on. An item is a symbol and reads flat; a *piece* is a thing you are about to
place, and a chest-shaped object photographed dead front-on is a rectangle. Turning
the model rather than the camera keeps the rig itself front-on and orthographic.
"""

import os
import sys
import glob

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# tools/ first: vhbuild.py is vendored here so this repo stands alone. The sibling
# vaettir checkout is kept as a fallback for a working tree that has both.
sys.path.insert(0, os.path.join(os.path.dirname(ROOT), "vaettir", "tools"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import clear_scene, tint

ASSETS = os.path.join(ROOT, "assets")

# 128, not 64. A 64px source is already soft in the slot the moment the UI scales at
# all, and the file is two kilobytes either way.
SIZE = 128

YAW = 25.0


def stage(radius):
    """
    Front-on, orthographic, transparent, square. Its own lights, not the preview's.
    """
    scene = bpy.context.scene
    scene.render.film_transparent = True

    # On +y, looking back along -y. The preview pass stands there too, and it is where
    # the front of every one of these models faces: photographed from -y the rack is
    # its own back panel, which renders as a plain brown box.
    #
    # Tilted 12 degrees down as well, so a little of the top reads. Dead level, a
    # shelf unit is a rectangle - which is the same failure as showing the donor's
    # chest icon, just in a different colour.
    bpy.ops.object.camera_add(location=(0.0, 2.93, radius * 0.62))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"

    # Framed off the model's own size rather than a constant, so a tall rack and a
    # squat barrow both fill the slot instead of one of them rattling around in it.
    cam.data.ortho_scale = radius * 2.55
    cam.rotation_euler = (math.radians(78.0), 0.0, math.radians(180.0))
    scene.camera = cam

    # Suns, and gentle ones. Area lights close to a small object blow every channel to
    # white - the tell is dark brown rendering as pale beige.
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

    Measured after the yaw is applied, because a box turned 25 degrees is wider than
    the box was - framing off the unturned bounds clips both front corners.
    """
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]

    for obj in objects:
        for corner in obj.bound_box:
            world = obj.matrix_world @ __import__("mathutils").Vector(corner)
            for axis in range(3):
                lo[axis] = min(lo[axis], world[axis])
                hi[axis] = max(hi[axis], world[axis])

    centre = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
    for obj in objects:
        obj.location.x -= centre[0]
        obj.location.y -= centre[1]
        obj.location.z -= centre[2]

    # Half the largest of width and height. Depth is deliberately ignored: an
    # orthographic camera does not care how deep the thing is, and including depth
    # makes a deep piece render small.
    return max(hi[0] - lo[0], hi[2] - lo[2]) * 0.5


def main():
    models = sorted(glob.glob(os.path.join(ASSETS, "stow_post_*.obj")))
    if not models:
        print("ICON_FAIL no stow_post_*.obj in assets")
        return

    for path in models:
        name = os.path.splitext(os.path.basename(path))[0]

        clear_scene()

        # The same two axis settings it was exported with, which makes the round trip
        # exact - winding untouched, nothing mirrored.
        bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        if not imported:
            print("ICON_FAIL %s imported nothing" % name)
            continue

        for obj in imported:
            # Added to, never assigned. The importer carries the Y-up to Z-up conversion
            # on the object's own rotation rather than baking it into the mesh, so
            # setting rotation_euler outright throws the conversion away and lays the
            # piece on its back - which renders as a featureless slab, because what you
            # are then looking at is its underside.
            #
            # Blender composes XYZ euler as Rz @ Ry @ Rx, so a z term added to a
            # conversion that lives in x is applied after it: a yaw in world space,
            # which is what is wanted.
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

        print("ICON_OK %s radius=%.2f" % (name, radius))


main()
