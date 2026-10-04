"""
The Transplant icon, painted to sit beside the vanilla piece icons.

    blender --background --python tools/transplant_icon_paint.py -- [subtle|medium|strong|all] [outdir]

transplant_icon.py built the bush from three flat Principled colours under one sun.
That is the clean 3D-model look. Vanilla icons are hand textured: muted mid-dark
values, plenty of fill light, dabs of tone in the texture, grime where the light does
not reach, and no black. The geometry, the camera and the transparent film are the
same as transplant_icon.py; what changes is what is painted on the blobs and how
they are lit (a sky over a warm ground instead of one sun).

For foliage the paint is leaf dabs: dark green undergrowth low and deep in the
crevices, muted mid greens, and a warm yellow-green on the lit tops and edges where
vanilla would put a chalky wear. There is no soil in this model and the silhouette
is not to change, so none is added.

Rendered at 1024 px and reduced by transplant_icon_post.py; filtering at 128 px
inside Cycles leaves the dabs as noise.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

VARIANTS = {
    "subtle": dict(smooth=0.40, amp=1.00, dirt=0.45, wear=0.35, band=0.0),
    "medium": dict(smooth=0.65, amp=1.50, dirt=0.75, wear=0.60, band=0.0),
    "strong": dict(smooth=0.85, amp=2.00, dirt=1.00, wear=0.90, band=0.5),
}

# linear colours: (base, shifted-warm, shifted-grey)
DARK = ((0.070, 0.115, 0.040), (0.140, 0.150, 0.040), (0.080, 0.100, 0.070))
MID = ((0.140, 0.205, 0.070), (0.260, 0.275, 0.070), (0.170, 0.195, 0.115))
LIT = ((0.200, 0.300, 0.080), (0.400, 0.410, 0.085), (0.220, 0.270, 0.130))
UNDER = (0.018, 0.030, 0.012)
WEAR = (0.480, 0.480, 0.150)


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


def material(name, cols, cfg, seed):
    base, warm, grey = cols
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
        mp.inputs["Location"].default_value = (seed, seed * 0.7, seed * 1.3)
        g.link(tc.outputs["Object"], mp.inputs["Vector"])
        nz = g.n("ShaderNodeTexNoise", {"Scale": scale, "Detail": detail, "Roughness": rough})
        g.link(mp.outputs[0], nz.inputs["Vector"])
        return nz.outputs["Fac"]

    blotch = noise(2.6, 4.0, 0.55)
    brush = noise(12.0, 5.0, 0.65, (1.0, 1.0, 0.7))
    grain = noise(38.0, 3.0, 0.6, (1.0, 1.0, 0.5))
    vor = g.n("ShaderNodeTexVoronoi", {"Scale": 20.0}, feature="F1")
    g.link(tc.outputs["Object"], vor.inputs["Vector"])
    dab = g.n("ShaderNodeRGBToBW")
    g.link(vor.outputs["Color"], dab.inputs["Color"])
    vedge = g.n("ShaderNodeTexVoronoi", {"Scale": 20.0}, feature="DISTANCE_TO_EDGE")
    g.link(tc.outputs["Object"], vedge.inputs["Vector"])

    amp = cfg["amp"]
    tone = g.m("ADD", 1.0, g.m("MULTIPLY", g.m("SUBTRACT", blotch, 0.5), 0.46 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", dab.outputs[0], 0.5), 0.50 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", grain, 0.5), 0.34 * amp))
    tone = g.m("ADD", tone, g.m("MULTIPLY", g.m("SUBTRACT", brush, 0.5), 0.30 * amp))
    if cfg["band"] > 0:
        steps = g.m("DIVIDE", g.m("ROUND", g.m("MULTIPLY", tone, 7.0)), 7.0)
        tone = g.m("ADD", g.m("MULTIPLY", tone, 1 - cfg["band"]), g.m("MULTIPLY", steps, cfg["band"]))

    hue1 = g.mix(base, warm, g.m("MULTIPLY", g.m("SUBTRACT", blotch, 0.35), 1.8 * amp, clamp=True))
    hue2 = g.mix(hue1, grey, g.m("MULTIPLY", g.m("SUBTRACT", dab.outputs[0], 0.55), 1.2 * amp, clamp=True))
    comb = g.n("ShaderNodeCombineColor")
    for i in range(3):
        g.link(tone, comb.inputs[i])
    painted = g.n("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY")
    painted.inputs["Factor"].default_value = 1.0
    g.link(hue2, painted.inputs["A"])
    g.link(comb.outputs[0], painted.inputs["B"])

    ao = g.n("ShaderNodeAmbientOcclusion", {"Distance": 0.45})
    ao.inside = False
    gz = g.n("ShaderNodeSeparateXYZ")
    g.link(geo.outputs["Normal"], gz.inputs[0])
    oz = g.n("ShaderNodeSeparateXYZ")
    g.link(tc.outputs["Object"], oz.inputs[0])
    crev = g.m("MULTIPLY", g.m("SUBTRACT", 1.0, ao.outputs["AO"]), 1.6)
    under = g.m("MULTIPLY", g.m("SUBTRACT", 0.15, gz.outputs["Z"]), 1.2, clamp=True)
    low = g.m("MULTIPLY", g.m("SUBTRACT", 0.45, oz.outputs["Z"]), 2.2, clamp=True)
    dirty = g.m("ADD", g.m("ADD", crev, g.m("MULTIPLY", under, 0.6)), g.m("MULTIPLY", low, 0.7))
    stain = g.m("MULTIPLY", g.m("SUBTRACT", noise(2.0, 5.0, 0.6), 0.42), 2.2, clamp=True)
    dirty = g.m("ADD", dirty, g.m("MULTIPLY", stain, 0.4))
    dirty = g.m("MULTIPLY", dirty, cfg["dirt"], clamp=True)
    grimed = g.mix(painted.outputs["Result"], UNDER, dirty)

    bev = g.n("ShaderNodeBevel", {"Radius": 0.05}, samples=8)
    dot = g.n("ShaderNodeVectorMath", operation="DOT_PRODUCT")
    g.link(bev.outputs["Normal"], dot.inputs[0])
    g.link(geo.outputs["Normal"], dot.inputs[1])
    edge = g.m("MULTIPLY", g.m("SUBTRACT", 1.0, dot.outputs["Value"]), 9.0, clamp=True)
    scratch = g.m("SUBTRACT", 1.0, g.m("MULTIPLY", vedge.outputs["Distance"], 11.0, clamp=True))
    wear = g.m("MULTIPLY", g.m("ADD", edge, g.m("MULTIPLY", scratch, 0.25)), cfg["wear"] * 0.55, clamp=True)
    g.link(g.mix(grimed, WEAR, wear), bsdf.inputs["Base Color"])
    return mat


def build(cfg):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {
        "dark": material("leaf_dark", DARK, cfg, 1.0),
        "mid": material("leaf_mid", MID, cfg, 4.0),
        "lit": material("leaf_lit", LIT, cfg, 8.0),
    }

    def blob(x, y, z, r, key):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x, y, z))
        ob = bpy.context.active_object
        ob.data.materials.append(mats[key])
        soften(ob, cfg["smooth"])

    # same blobs as transplant_icon.py
    blob(0.0, 0.0, 0.45, 0.62, "dark")
    blob(-0.75, 0.05, 0.38, 0.5, "dark")
    blob(0.75, -0.02, 0.4, 0.52, "dark")
    blob(-1.1, 0.1, 0.3, 0.36, "dark")
    blob(1.12, 0.08, 0.3, 0.37, "dark")
    blob(-0.4, -0.12, 0.62, 0.46, "mid")
    blob(0.42, -0.1, 0.65, 0.47, "mid")
    blob(-0.85, -0.08, 0.5, 0.34, "mid")
    blob(0.9, -0.1, 0.52, 0.32, "mid")
    blob(0.0, -0.22, 0.78, 0.42, "lit")
    blob(-0.45, -0.2, 0.72, 0.3, "lit")
    blob(0.5, -0.22, 0.7, 0.28, "lit")


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

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.location = (0.0, -6.0, 0.55)
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 3.1
    scene.camera = cam

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    scene.collection.objects.link(sun)
    sun.data.energy = 3.4
    sun.data.angle = math.radians(28)
    sun.data.color = (1.0, 0.97, 0.92)
    sun.rotation_euler = (math.radians(55), 0, math.radians(-20))

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
    bg.inputs["Strength"].default_value = 0.75
    scene.render.filepath = os.path.join(outdir, "raw_%s.png" % tag)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = args[0] if args else "medium"
    outdir = args[1] if len(args) > 1 else os.path.join(ROOT, "renders", "lhm-72")
    for v in (list(VARIANTS) if which == "all" else [which]):
        build(VARIANTS[v])
        stage(outdir, v)
        bpy.ops.render.render(write_still=True)
        bpy.context.scene.render.film_transparent = False
        print("rendered", v)


main()
