"""
Four bonemeal models, built like the suite builds things - and judged from renders
before anything is wired.

    blender --background --python tools/bonemeal_designs.py

The road here: six shelved shapes read as random parts, the flour-sack donor was an
identical twin, tints drowned in the cloth, and a bone garnish grafted a vanilla prop,
which the house rule exists to forbid. So: hand-built, one readable silhouette each,
few large overlapping parts, one bevel pass per object before joining, posterised
flat colours, seeded jitter. Rendered close and front-on, because an item is judged
at arm's length in a hand or on the ground, never from a hero orbit.

  A  slump   an open sack slumped sideways, meal spilling at the mouth
  B  tied    an upright sack cinched near the top, ears above the tie
  C  crock   a squat clay pot, meal heaped over the rim
  D  scoop   a wooden scoop lying in a poured pile of meal
"""

import math
import random
import bpy
from mathutils import Vector

OUT = __file__.rsplit("tools", 1)[0] + "assets/previews/"

CLOTH = (0.42, 0.34, 0.24)
CLOTH_DARK = (0.33, 0.26, 0.18)
MEAL = (0.78, 0.74, 0.64)
CLAY = (0.45, 0.30, 0.22)
WOOD = (0.36, 0.26, 0.15)
ROPE = (0.55, 0.44, 0.28)


def flat(name, rgb):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs[0].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def bevel(ob, width=0.008):
    mod = ob.modifiers.new("bevel", "BEVEL")
    mod.width = width
    mod.segments = 1
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(40)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier="bevel")


def jit(scale=2.0):
    return math.radians(random.uniform(-scale, scale))


def sphereish(x, y, z, rx, ry, rz, mat, squash=1.0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(x, y, z))
    ob = bpy.context.active_object
    ob.scale = (rx, ry, rz * squash)
    ob.rotation_euler = (jit(), jit(), jit(6))
    ob.data.materials.append(mat)
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return ob


def cyl(x, y, z, r, depth, mat, sides=9, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=r, depth=depth,
                                        location=(x, y, z), rotation=rot)
    ob = bpy.context.active_object
    ob.data.materials.append(mat)
    bevel(ob)
    return ob


def slump():
    """A: open sack slumped sideways - the mouth and the spill are the silhouette."""
    body = sphereish(0, 0, 0.16, 0.24, 0.19, 0.16, flat("cloth", CLOTH))
    sphereish(0.1, 0.0, 0.26, 0.13, 0.11, 0.1, flat("cloth_dark", CLOTH_DARK))
    # the mouth: a shorter blob leaning off the body's high side
    sphereish(0.2, 0.02, 0.1, 0.1, 0.09, 0.08, flat("cloth", CLOTH))
    # the meal, spilling where the mouth points
    sphereish(0.3, 0.02, 0.05, 0.09, 0.08, 0.05, flat("meal", MEAL))
    sphereish(0.38, 0.04, 0.03, 0.05, 0.05, 0.03, flat("meal", MEAL))


def tied():
    """B: upright sack cinched near the top, ears above the tie."""
    sphereish(0, 0, 0.16, 0.17, 0.15, 0.17, flat("cloth", CLOTH))
    sphereish(0, 0, 0.3, 0.12, 0.11, 0.1, flat("cloth", CLOTH))
    # the cinch - one thin rope ring
    cyl(0, 0, 0.36, 0.065, 0.025, flat("rope", ROPE))
    # the ears above the tie
    sphereish(-0.03, 0.01, 0.42, 0.045, 0.04, 0.05, flat("cloth_dark", CLOTH_DARK))
    sphereish(0.04, -0.01, 0.41, 0.04, 0.035, 0.045, flat("cloth_dark", CLOTH_DARK))


def crock():
    """C: squat clay pot, meal heaped over the rim."""
    cyl(0, 0, 0.11, 0.16, 0.22, flat("clay", CLAY), sides=9)
    cyl(0, 0, 0.225, 0.175, 0.035, flat("clay", CLAY), sides=9)
    sphereish(0, 0, 0.27, 0.14, 0.13, 0.06, flat("meal", MEAL))
    sphereish(0.04, 0.02, 0.3, 0.07, 0.06, 0.035, flat("meal", MEAL))


def scoop():
    """D: wooden scoop lying in a poured pile."""
    sphereish(0, 0, 0.06, 0.26, 0.2, 0.07, flat("meal", MEAL))
    sphereish(0.08, 0.04, 0.1, 0.12, 0.1, 0.05, flat("meal", MEAL))
    # the scoop: a half-open cylinder as the bowl, a stick as the handle
    cyl(-0.08, -0.04, 0.12, 0.06, 0.14, flat("wood", WOOD), sides=7,
        rot=(math.radians(90 + 12), 0, math.radians(30)))
    cyl(-0.2, -0.1, 0.13, 0.02, 0.16, flat("wood", WOOD), sides=7,
        rot=(math.radians(90 + 12), 0, math.radians(30)))


def sack():
    """E: a real sack of revolution - a lathe profile, not stacked spheres. The
    silhouette IS the read: belly, cinch, flare above the tie. Slight seeded
    jitter after the spin so the cloth is not machine-perfect."""
    import bmesh

    profile = [(0.0, 0.0), (0.155, 0.0), (0.185, 0.09), (0.175, 0.2),
               (0.135, 0.29), (0.058, 0.34), (0.075, 0.38), (0.05, 0.41),
               (0.0, 0.425)]

    mesh = bpy.data.meshes.new("sack")
    ob = bpy.data.objects.new("sack", mesh)
    bpy.context.collection.objects.link(ob)

    bm = bmesh.new()
    ring = [bm.verts.new((r, 0.0, z)) for r, z in profile]
    # Edges, not bare verts: spin sweeps verts into edges and EDGES into faces,
    # so a vert-only profile spins into an invisible wireframe. Found by the
    # first render showing a floating rope and no sack.
    edges = [bm.edges.new((ring[i], ring[i + 1])) for i in range(len(ring) - 1)]
    # Nine steps, not ten: an even-sided lathe presents a flat face straight at
    # the camera and its quads read as rectangles - the house cylinder rule.
    bmesh.ops.spin(bm, geom=ring + edges, angle=math.tau, steps=9,
                   axis=(0, 0, 1), cent=(0, 0, 0))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.001)
    for v in bm.verts:
        if 0.02 < v.co.z < 0.4:
            v.co.x += random.uniform(-0.018, 0.018)
            v.co.y += random.uniform(-0.018, 0.018)
            v.co.z += random.uniform(-0.008, 0.008)

    # Triangulated, or the lathe's neat rings read as rows of rectangles - which
    # is exactly what they are until the quads are split and jittered.
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(mesh)
    bm.free()

    ob.data.materials.append(flat("cloth", CLOTH))
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.shade_flat()

    # A bmesh-born mesh has NO UV layer, and the OBJ writes its faces without
    # texture coordinates - which the runtime loader drops silently, so the sack
    # body simply did not render in game while the primitives beside it did.
    # Cylinder projection, because a body of revolution is the projection's own
    # case - after scale, before any rotation, per the house rule.
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cylinder_project(scale_to_bounds=True)
    bpy.ops.object.mode_set(mode="OBJECT")

    # the rope at the cinch, and the meal peeking above the flare
    cyl(0, 0, 0.345, 0.075, 0.022, flat("rope", ROPE), sides=9)
    sphereish(0, 0, 0.415, 0.045, 0.045, 0.025, flat("meal", MEAL))


BUILDS = [("e_sack", sack)]

EXPORT = True   # the sack won; every run re-exports the asset and its icon


def export_asset():
    """The picked sack into assets/, plus a fresh icon - front-on, orthographic,
    transparent, 128px, its own exposure, per the icon rules."""
    import os
    root = __file__.rsplit("tools", 1)[0]

    bpy.ops.object.select_all(action="SELECT")
    meshes = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bpy.ops.object.select_all(action="DESELECT")
    joined.select_set(True)
    bpy.ops.wm.obj_export(filepath=os.path.join(root, "assets", "grove_bonemeal.obj"),
                          export_selected_objects=True, export_materials=True,
                          forward_axis="Z", up_axis="Y")
    print("exported grove_bonemeal.obj")

    bpy.ops.object.camera_add(location=(0.0, -1.1, 0.21),
                              rotation=(math.radians(90), 0, 0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.55
    bpy.context.scene.camera = cam
    scene = bpy.context.scene
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.filepath = os.path.join(root, "assets", "grove_bonemeal_icon.png")
    bpy.ops.render.render(write_still=True)
    print("icon written")


def main():
    import os
    os.makedirs(OUT, exist_ok=True)
    for name, build in BUILDS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        random.seed(11)
        build()

        bpy.ops.object.camera_add(location=(0.05, -1.15, 0.35),
                                  rotation=(math.radians(78), 0, 0))
        cam = bpy.context.active_object
        cam.data.lens = 50
        bpy.context.scene.camera = cam

        bpy.ops.object.light_add(type="SUN", location=(-1, -2, 3))
        sun = bpy.context.active_object
        sun.data.energy = 2.4
        sun.rotation_euler = (math.radians(50), 0, math.radians(-25))

        world = bpy.data.worlds.new("w")
        bpy.context.scene.world = world
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[1].default_value = 0.45

        scene = bpy.context.scene
        scene.view_settings.view_transform = "Standard"
        scene.render.film_transparent = True
        scene.render.resolution_x = 340
        scene.render.resolution_y = 340
        scene.render.filepath = OUT + "bonemeal_design_" + name + ".png"
        bpy.ops.render.render(write_still=True)
        print("rendered " + name)

        if EXPORT and name == "e_sack":
            export_asset()


main()
