"""
The Transplant entry's icon: a hand-made bush with mixed berries.

    blender --background --python tools/transplant_icon.py

The live-photograph route failed twice - once as a mud clod, once washed pale -
because a camera pointed at vanilla foliage cards obeys nobody. This is the suite's
answer everywhere else: build the shape, flat posterised colours, orthographic and
front-on with a transparent film, 128px. The berries are deliberately BOTH red and
blue: the tool moves raspberries, blueberries and mushrooms alike, and the icon
lying about that scope is what killed the raspberry-item version.
"""

import math
import random
import bpy

OUT = __file__.rsplit("tools", 1)[0] + "assets/thicket_transplant.png"


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def flat(name, rgb):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs[0].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def blob(x, y, z, r, mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, z))
    ob = bpy.context.active_object
    ob.data.materials.append(mat)
    return ob


def main():
    clear()
    random.seed(7)

    leaf_dark = flat("leaf_dark", (0.10, 0.22, 0.08))
    leaf_mid = flat("leaf_mid", (0.16, 0.34, 0.11))
    leaf_lit = flat("leaf_lit", (0.26, 0.48, 0.16))

    # A BUSH: wider than tall, sitting on the ground, no trunk - the first pass
    # had a stem and a round crown and read as a lollipop tree ("make it look
    # like a fuckin bush"). Overlapping blobs, darker low and outer, lighter on
    # the top-front where the light would sit.
    blob(0.0, 0.0, 0.45, 0.62, leaf_dark)
    blob(-0.75, 0.05, 0.38, 0.5, leaf_dark)
    blob(0.75, -0.02, 0.4, 0.52, leaf_dark)
    blob(-1.1, 0.1, 0.3, 0.36, leaf_dark)
    blob(1.12, 0.08, 0.3, 0.37, leaf_dark)
    blob(-0.4, -0.12, 0.62, 0.46, leaf_mid)
    blob(0.42, -0.1, 0.65, 0.47, leaf_mid)
    blob(-0.85, -0.08, 0.5, 0.34, leaf_mid)
    blob(0.9, -0.1, 0.52, 0.32, leaf_mid)
    blob(0.0, -0.22, 0.78, 0.42, leaf_lit)
    blob(-0.45, -0.2, 0.72, 0.3, leaf_lit)
    blob(0.5, -0.22, 0.7, 0.28, leaf_lit)

    # Orthographic, front-on, its own exposure, transparent film - the icon rules.
    bpy.ops.object.camera_add(location=(0.0, -6.0, 0.55),
                              rotation=(math.radians(90), 0.0, 0.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 3.1
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(-2.0, -4.0, 5.0))
    sun = bpy.context.active_object
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(55), 0.0, math.radians(-20))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT" if hasattr(bpy.types, "SceneEEVEE") else "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"   # AgX rolls colour to white
    scene.render.film_transparent = True
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.filepath = OUT
    bpy.ops.render.render(write_still=True)
    print("icon written to " + OUT)


main()
