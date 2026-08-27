"""
The bonemeal model in candidate colours, one render each.

    blender --background --python tools/bonemeal_tints.py

He kept the shape and called the colour: in game it wears its donor's bone-white
material (only the mesh is swapped), and "very white/gray" is exactly right. These
are the same grove_bonemeal.obj under four flat tints, rendered to be compared;
the picked value becomes the in-game tint.
"""

import math
import os
import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OBJ = os.path.join(ROOT, "assets", "grove_bonemeal.obj")
OUT = os.path.join(ROOT, "assets", "previews")

TINTS = [
    ("b_ivory_warm", (0.78, 0.70, 0.52)),   # the neutral judge's tint
]

# The five shelved shapes from the bonemeal branch, plus the current one -
# recovered rather than redesigned, because they were built and never judged.
MODELS = [
    ("current", os.path.join(ROOT, "assets", "grove_bonemeal.obj")),
    ("heap",    os.path.join(ROOT, "assets", "variants", "grove_bonemeal_heap.obj")),
    ("crock",   os.path.join(ROOT, "assets", "variants", "grove_bonemeal_crock.obj")),
    ("bundle",  os.path.join(ROOT, "assets", "variants", "grove_bonemeal_bundle.obj")),
    ("bindle",  os.path.join(ROOT, "assets", "variants", "grove_bonemeal_bindle.obj")),
    ("cake",    os.path.join(ROOT, "assets", "variants", "grove_bonemeal_cake.obj")),
]


def main():
    os.makedirs(OUT, exist_ok=True)

    name0, rgb = TINTS[0]
    for name, path in MODELS:
        bpy.ops.wm.read_factory_settings(use_empty=True)

        bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
        pieces = [o for o in bpy.data.objects if o.type == "MESH"]

        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs[0].default_value = (*rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = 1.0

        lo = [1e9] * 3
        hi = [-1e9] * 3
        for ob in pieces:
            ob.data.materials.clear()
            ob.data.materials.append(mat)
            for v in ob.bound_box:
                w = ob.matrix_world @ __import__("mathutils").Vector(v)
                for i in range(3):
                    lo[i] = min(lo[i], w[i])
                    hi[i] = max(hi[i], w[i])

        cx = (lo[0] + hi[0]) / 2
        cz = (lo[2] + hi[2]) / 2
        span = max(hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2])

        bpy.ops.object.camera_add(
            location=(cx, lo[1] - span * 2.2, cz + span * 0.35),
            rotation=(math.radians(80), 0.0, 0.0))
        cam = bpy.context.active_object
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = span * 1.5
        bpy.context.scene.camera = cam

        bpy.ops.object.light_add(type="SUN", location=(cx - 2, -4, 5))
        sun = bpy.context.active_object
        sun.data.energy = 2.6
        sun.rotation_euler = (math.radians(55), 0.0, math.radians(-25))

        world = bpy.data.worlds.new("w")
        bpy.context.scene.world = world
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[1].default_value = 0.5

        scene = bpy.context.scene
        scene.view_settings.view_transform = "Standard"
        scene.render.film_transparent = True
        scene.render.resolution_x = 300
        scene.render.resolution_y = 300
        scene.render.filepath = os.path.join(OUT, "bonemeal_model_" + name + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered " + name)


main()
