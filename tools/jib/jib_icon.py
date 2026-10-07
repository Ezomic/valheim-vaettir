"""
The hod jib's build-menu icon, rendered 1024 px and reduced to 128 by jib_icon_post.py.

    blender --background --python tools/jib/jib_icon.py -- [outdir]

Lighting and finish follow the LHM-70 bonemeal icon (tools/bonemeal_icon.py on that branch):
a sky over a warm ground as the world, so a face turned down or away still gets a bounce and
nothing goes black, one soft sun, Cycles with matte surfaces, transparent background, Standard
view transform. Where the bonemeal icon paints a procedural surface, this one wears the game's
own textures through the LHM-69 pipeline, because here the point is that the icon matches the
piece: bench planks, the stonecutter's metal, the village weave, the fern.

A three-quarter view from the front, a little above the horizon, framed on the whole piece.
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import jib_check                      # noqa: E402
import bpy                            # noqa: E402
from mathutils import Vector          # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(jib_check.WT, "renders", "lhm-77c")


def stage_icon():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.9

    objs = [o for o in bpy.data.objects if o.type == "MESH"]
    lo, hi = jib_check._bounds(objs)
    centre = Vector(((lo.x + hi.x) / 2.0, (lo.y + hi.y) / 2.0, (lo.z + hi.z) / 2.0))

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(hi.z - lo.z, hi.x - lo.x) * 1.12
    elevation, azimuth = math.radians(12.0), math.radians(-28.0)
    cam.location = centre + Vector((math.sin(azimuth) * math.cos(elevation) * 12.0,
                                    math.cos(azimuth) * math.cos(elevation) * 12.0,
                                    math.sin(elevation) * 12.0))
    aim = bpy.data.objects.new("aim", None)
    scene.collection.objects.link(aim)
    aim.location = centre
    tr = cam.constraints.new(type="TRACK_TO")
    tr.target = aim
    tr.track_axis = "TRACK_NEGATIVE_Z"
    tr.up_axis = "UP_Y"
    scene.camera = cam

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    scene.collection.objects.link(sun)
    sun.data.energy = 4.0
    sun.data.angle = math.radians(28)
    sun.data.color = (1.0, 0.97, 0.92)
    sun.rotation_euler = (math.radians(50), 0, math.radians(150))

    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.38
    ramp.color_ramp.elements[0].color = (0.30, 0.22, 0.15, 1)
    ramp.color_ramp.elements[1].position = 0.62
    ramp.color_ramp.elements[1].color = (0.78, 0.79, 0.84, 1)
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    nt.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.6


def main():
    os.makedirs(OUT, exist_ok=True)
    jib_check.load_model(True)
    jib_check.glow_up()
    stage_icon()
    bpy.context.scene.render.filepath = os.path.join(OUT, "raw_jib_icon.png")
    bpy.ops.render.render(write_still=True)
    print("JIB_ICON_DONE", OUT)


main()
