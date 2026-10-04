"""
The bonemeal icon, painted to sit beside the vanilla piece icons.

    blender --background --python tools/bonemeal_icon.py -- [subtle|medium|strong|all] [outdir]

The old icon was one flat Principled colour under one sun. Vanilla icons are hand
textured low-poly assets: mid-dark values, a lot of fill light, dabs of tone in the
texture, dirt where the light does not reach, and no black anywhere. This keeps the
mesh and the camera exactly as bonemeal_designs.py frames them and changes only what
is painted on it and how it is lit.

The very dark triangles at the lower right were never a mesh fault: every face is
outward, the OBJ carries one uniform material and the normals agree with the winding.
They were faces turned away from a single sun (n.L between -0.1 and -0.25) with a weak
world term, so nothing lit them. Here the world is a sky over a warm ground, so a face
that looks down or away still gets a bounce.

The render is 1024 px and is reduced afterwards by bonemeal_icon_post.py; filtering at
128 px inside Cycles leaves the dabs as noise.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OBJ = os.path.join(ROOT, "assets", "grove_bonemeal.obj")

# smooth: how far the shading normal moves from the face normal towards the vertex
# normal. 0 is the old flat facets, 1 is a fully rounded sack with a faceted outline.
# amp: tonal variation in the paint. dirt / wear: crevice grime and chalky edges.
# band: how far the tone is pushed into hard painted steps.
VARIANTS = {
    "subtle": dict(smooth=0.45, amp=1.00, dirt=0.40, wear=0.35, band=0.0),
    "medium": dict(smooth=0.70, amp=1.50, dirt=0.70, wear=0.65, band=0.0),
    "strong": dict(smooth=0.90, amp=2.00, dirt=1.00, wear=1.00, band=0.5),
}

BONE = (0.45, 0.41, 0.325)
WARM = (0.38, 0.30, 0.20)
GREY = (0.35, 0.345, 0.33)
DIRT = (0.13, 0.108, 0.085)
CHALK = (0.66, 0.61, 0.49)


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=OBJ, forward_axis="Z", up_axis="Y")
    ob = [o for o in bpy.data.objects if o.type == "MESH"][0]
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


def soften(ob, k):
    me = ob.data
    acc = [Vector((0, 0, 0)) for _ in me.vertices]
    for p in me.polygons:
        for v in p.vertices:
            acc[v] += p.normal * p.area
    normals = [None] * len(me.loops)
    for p in me.polygons:
        for li in p.loop_indices:
            vn = acc[me.loops[li].vertex_index].normalized()
            normals[li] = tuple((p.normal * (1 - k) + vn * k).normalized())
    me.normals_split_custom_set(normals)


class Graph:
    def __init__(self, nt):
        self.nt = nt

    def n(self, kind, inputs=None, **attrs):
        node = self.nt.nodes.new(kind)
        for key, val in attrs.items():
            setattr(node, key, val)
        for name, val in (inputs or {}).items():
            node.inputs[name].default_value = val
        return node

    def link(self, src, dst):
        self.nt.links.new(src, dst)

    def m(self, op, a, b=None, clamp=False):
        node = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                node.inputs[i].default_value = v
            else:
                self.link(v, node.inputs[i])
        return node.outputs[0]

    def mix(self, a, b, fac):
        node = self.n("ShaderNodeMix", data_type="RGBA")
        for sock, v in ((node.inputs["A"], a), (node.inputs["B"], b)):
            if isinstance(v, tuple):
                sock.default_value = (*v, 1)
            else:
                self.link(v, sock)
        if isinstance(fac, (int, float)):
            node.inputs["Factor"].default_value = fac
        else:
            self.link(fac, node.inputs["Factor"])
        return node.outputs["Result"]


def material(name, base, warm, grey, cfg, chalk=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    g = Graph(mat.node_tree)
    g.nt.nodes.clear()
    out = g.n("ShaderNodeOutputMaterial")
    bsdf = g.n("ShaderNodeBsdfPrincipled", {"Roughness": 1.0, "Specular IOR Level": 0.0})
    g.link(bsdf.outputs[0], out.inputs[0])

    tc = g.n("ShaderNodeTexCoord")
    geo = g.n("ShaderNodeNewGeometry")

    def noise(scale, detail, rough, stretch=(1, 1, 1)):
        mp = g.n("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = stretch
        g.link(tc.outputs["Object"], mp.inputs["Vector"])
        nz = g.n("ShaderNodeTexNoise", {"Scale": scale, "Detail": detail, "Roughness": rough})
        g.link(mp.outputs[0], nz.inputs["Vector"])
        return nz.outputs["Fac"]

    blotch = noise(11.0, 4.0, 0.55)
    brush = noise(24.0, 5.0, 0.65, (1.0, 1.0, 0.6))
    grain = noise(70.0, 3.0, 0.6, (1.0, 1.0, 0.45))
    vor = g.n("ShaderNodeTexVoronoi", {"Scale": 46.0}, feature="F1")
    g.link(tc.outputs["Object"], vor.inputs["Vector"])
    dab = g.n("ShaderNodeRGBToBW")
    g.link(vor.outputs["Color"], dab.inputs["Color"])
    vedge = g.n("ShaderNodeTexVoronoi", {"Scale": 46.0}, feature="DISTANCE_TO_EDGE")
    g.link(tc.outputs["Object"], vedge.inputs["Vector"])

    amp = cfg["amp"]
    tone = g.m("ADD", 1.0, g.m("MULTIPLY", g.m("SUBTRACT", blotch, 0.5), 0.46 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", dab.outputs[0], 0.5), 0.34 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", grain, 0.5), 0.26 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", brush, 0.5), 0.30 * amp))
    if cfg["band"] > 0:
        steps = g.m("DIVIDE", g.m("ROUND", g.m("MULTIPLY", tone, 7.0)), 7.0)
        tone = g.m("ADD", g.m("MULTIPLY", tone, 1 - cfg["band"]), g.m("MULTIPLY", steps, cfg["band"]))

    hue1 = g.mix(base, warm, g.m("MULTIPLY", g.m("SUBTRACT", blotch, 0.3), 1.6 * amp, clamp=True))
    hue2 = g.mix(hue1, grey, g.m("MULTIPLY", g.m("SUBTRACT", dab.outputs[0], 0.55), 1.2 * amp, clamp=True))
    comb = g.n("ShaderNodeCombineColor")
    for i in range(3):
        g.link(tone, comb.inputs[i])
    painted = g.n("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY")
    painted.inputs["Factor"].default_value = 1.0
    g.link(hue2, painted.inputs["A"])
    g.link(comb.outputs[0], painted.inputs["B"])

    ao = g.n("ShaderNodeAmbientOcclusion", {"Distance": 0.10})
    ao.inside = False
    gz = g.n("ShaderNodeSeparateXYZ")
    g.link(geo.outputs["Normal"], gz.inputs[0])
    oz = g.n("ShaderNodeSeparateXYZ")
    g.link(tc.outputs["Object"], oz.inputs[0])
    crev = g.m("MULTIPLY", g.m("SUBTRACT", 1.0, ao.outputs["AO"]), 1.9)
    under = g.m("MULTIPLY", g.m("SUBTRACT", 0.15, gz.outputs["Z"]), 1.2, clamp=True)
    low = g.m("MULTIPLY", g.m("SUBTRACT", 0.20, oz.outputs["Z"]), 3.0, clamp=True)
    dirty = g.m("ADD", g.m("ADD", crev, g.m("MULTIPLY", under, 0.7)), g.m("MULTIPLY", low, 0.6))
    stain = g.m("MULTIPLY", g.m("SUBTRACT", noise(4.5, 5.0, 0.6), 0.42), 2.2, clamp=True)
    dirty = g.m("ADD", dirty, g.m("MULTIPLY", stain, 0.6))
    dirty = g.m("MULTIPLY", dirty, cfg["dirt"], clamp=True)
    grimed = g.mix(painted.outputs["Result"], DIRT, dirty)

    bev = g.n("ShaderNodeBevel", {"Radius": 0.012}, samples=8)
    dot = g.n("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    g.link(bev.outputs["Normal"], dot.inputs[0])
    g.link(geo.outputs["Normal"], dot.inputs[1])
    edge = g.m("MULTIPLY", g.m("SUBTRACT", 1.0, dot.outputs["Value"]), 9.0, clamp=True)
    scratch = g.m("SUBTRACT", 1.0, g.m("MULTIPLY", vedge.outputs["Distance"], 38.0, clamp=True))
    wear = g.m("MULTIPLY", g.m("ADD", edge, g.m("MULTIPLY", scratch, 0.25)), cfg["wear"] * chalk * 0.55, clamp=True)
    g.link(g.mix(grimed, CHALK, wear), bsdf.inputs["Base Color"])
    return mat


def stage(outdir, tag):
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

    # the camera of bonemeal_designs.export_asset, untouched
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.location = (0.0, -1.1, 0.21)
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.55
    scene.camera = cam

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    scene.collection.objects.link(sun)
    sun.data.energy = 2.3
    sun.data.angle = math.radians(28)
    sun.data.color = (1.0, 0.97, 0.92)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-25))

    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    g = Graph(world.node_tree)
    bg = world.node_tree.nodes["Background"]
    tc = g.n("ShaderNodeTexCoord")
    sep = g.n("ShaderNodeSeparateXYZ")
    g.link(tc.outputs["Generated"], sep.inputs[0])
    ramp = g.n("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.38
    ramp.color_ramp.elements[0].color = (0.30, 0.22, 0.15, 1)
    ramp.color_ramp.elements[1].position = 0.62
    ramp.color_ramp.elements[1].color = (0.78, 0.79, 0.84, 1)
    g.link(sep.outputs["Z"], ramp.inputs["Fac"])
    g.link(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.46
    scene.render.filepath = os.path.join(outdir, "raw_%s.png" % tag)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = args[0] if args else "medium"
    outdir = args[1] if len(args) > 1 else os.path.join(ROOT, "renders", "lhm-70")
    for v in (list(VARIANTS) if which == "all" else [which]):
        cfg = VARIANTS[v]
        ob = load()
        soften(ob, cfg["smooth"])
        slot = ob.material_slots
        slot[0].material = material("cloth", BONE, WARM, GREY, cfg)
        slot[1].material = (material("rope", (0.42, 0.32, 0.19), (0.36, 0.27, 0.15), (0.38, 0.33, 0.25), cfg, 0.7))
        # clear() would reset every face to slot 0; assigning in place keeps the OBJ's indices
        pale = dict(cfg, dirt=cfg["dirt"] * 0.15, amp=cfg["amp"] * 0.45, wear=cfg["wear"] * 0.3)
        slot[2].material = (material("meal", (1.0, 0.96, 0.86), (0.92, 0.86, 0.72), (0.90, 0.88, 0.82), pale, 0.5))
        stage(outdir, v)
        bpy.ops.render.render(write_still=True)
        bpy.context.scene.render.film_transparent = False
        print("rendered", v)


main()
