"""
Five icon concepts for the Transplant entry, each a small hand-made model.

    blender --background --python tools/transplant_concepts.py -- [A|B|C|D|E|all] [outdir]

The restyled bush (transplant_icon_paint.py) was rejected as a whole: a green faceted
mound is not an object. Its faults, measured and seen at 64 px:

  - no recognisable subject: three greens and no part that says "bush", "plant" or
    "being moved", so the eye has nothing to land on
  - a blob silhouette: a flat wedge with a zigzag underside, wider than tall, with no
    protrusion, hole or neck that makes it that item rather than any other
  - no focal point and no value steps: every facet is within a few percent of the next
  - nothing hand made: vanilla icons are one clear object with 2 or 3 value steps and
    a second material (a band, a hinge, a glow) that gives the object a purpose

Each concept below is one dominant object with a different silhouette, built from wood,
cloth, earth, cord and leaf, and each carries a plant that is being moved, not made.
The paint, light and reduction are those of the LHM-70/72 work: the materials come from
transplant_icon_paint.material, the light is a sun plus a sky over a warm ground, the
render is 1024 px and is reduced by transplant_concepts_post.py.

The camera is the one the vanilla piece icons use: orthographic, about 24 degrees above
the horizon and turned a quarter-turn towards three quarter view, fitted to fill about
86 percent of the frame whatever the model's size.
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import transplant_icon_paint as paint  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

CFG = dict(smooth=0.5, amp=1.5, dirt=0.75, wear=0.6, band=0.0)

EARTH = (((0.075, 0.050, 0.032), (0.120, 0.080, 0.045), (0.085, 0.070, 0.060)), (0.020, 0.014, 0.010), (0.22, 0.15, 0.09))
WOOD = (((0.17, 0.10, 0.05), (0.26, 0.15, 0.07), (0.15, 0.11, 0.08)), (0.030, 0.018, 0.010), (0.34, 0.22, 0.12))
CLOTH = (((0.36, 0.29, 0.17), (0.46, 0.36, 0.19), (0.32, 0.28, 0.21)), (0.070, 0.055, 0.035), (0.62, 0.54, 0.36))
CORD = (((0.26, 0.19, 0.10), (0.34, 0.24, 0.11), (0.24, 0.20, 0.14)), (0.040, 0.030, 0.018), (0.46, 0.36, 0.20))
METAL = (((0.10, 0.11, 0.12), (0.14, 0.13, 0.12), (0.12, 0.13, 0.15)), (0.020, 0.020, 0.025), (0.35, 0.36, 0.36))
ROOTS = (((0.20, 0.13, 0.07), (0.28, 0.19, 0.09), (0.18, 0.14, 0.10)), (0.030, 0.020, 0.012), (0.42, 0.30, 0.16))
GRASS = (((0.10, 0.19, 0.05), (0.22, 0.26, 0.05), (0.12, 0.17, 0.08)), paint.UNDER, paint.WEAR)
STONE = (((0.15, 0.15, 0.14), (0.20, 0.18, 0.15), (0.14, 0.15, 0.16)), (0.030, 0.030, 0.030), (0.40, 0.39, 0.36))
BERRY = (((0.30, 0.040, 0.035), (0.42, 0.075, 0.040), (0.22, 0.070, 0.060)), (0.050, 0.008, 0.008), (0.60, 0.20, 0.12))
LEAF_D = (paint.DARK, paint.UNDER, paint.WEAR)
LEAF_M = (paint.MID, paint.UNDER, paint.WEAR)
LEAF_L = (paint.LIT, paint.UNDER, paint.WEAR)


class Kit:
    def __init__(self, cfg):
        self.cfg = cfg
        self.mats = {}
        self.objs = []
        self.seed = 0.0

    def mat(self, name, spec):
        if name not in self.mats:
            self.seed += 2.7
            cols, under, wear = spec
            self.mats[name] = paint.material(name, cols, self.cfg, self.seed, under, wear, sc=1.5)
        return self.mats[name]

    def prim(self, kind, mat, loc=(0, 0, 0), scale=(1, 1, 1), rot=(0, 0, 0), smooth=None, **kw):
        if kind == "cube":
            bpy.ops.mesh.primitive_cube_add(size=1.0)
        elif kind == "cyl":
            bpy.ops.mesh.primitive_cylinder_add(vertices=kw.get("v", 7), radius=0.5, depth=1.0)
        elif kind == "cone":
            bpy.ops.mesh.primitive_cone_add(vertices=kw.get("v", 7), radius1=0.5, radius2=kw.get("r2", 0.0), depth=1.0)
        elif kind == "ico":
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=kw.get("sub", 1), radius=0.5)
        elif kind == "torus":
            bpy.ops.mesh.primitive_torus_add(major_segments=kw.get("v", 11), minor_segments=5,
                                             major_radius=0.5, minor_radius=kw.get("minor", 0.06))
        ob = bpy.context.active_object
        ob.data.materials.append(mat)
        ob.scale = scale
        bpy.ops.object.transform_apply(scale=True)
        ob.rotation_euler = tuple(math.radians(a) for a in rot)
        bpy.ops.object.transform_apply(rotation=True)
        ob.location = loc
        bpy.ops.object.transform_apply(location=True)
        ob["smooth"] = self.cfg["smooth"] if smooth is None else smooth
        self.objs.append(ob)
        return ob

    def leaf(self, mat, base, length, width, tilt, yaw, curl=0.0):
        ob = self.prim("ico", mat, loc=(0, 0, length / 2), scale=(width, width * 0.22, length), sub=1)
        m = Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Rotation(math.radians(tilt), 4, "X")
        ob.data.transform(Matrix.Translation(Vector(base)) @ m)
        return ob

    def crown(self, base, count, length, width, tilt, spread=0.0, start=0.0):
        mats = [self.mat("leaf_m", LEAF_M), self.mat("leaf_l", LEAF_L), self.mat("leaf_d", LEAF_D)]
        for i in range(count):
            yaw = start + 360.0 * i / count
            self.leaf(mats[i % 3], base, length * (0.82 + 0.18 * ((i * 5) % 3) / 2), width,
                      tilt + spread * ((i * 7) % 3 - 1), yaw)

    def transform(self, objs, matrix):
        for ob in objs:
            ob.data.transform(matrix)

    def finish(self):
        for ob in self.objs:
            paint.soften(ob, ob["smooth"])


def concept_a(k):
    """A plant lifted with its root ball wrapped in cloth and tied."""
    cloth, cord, earth = k.mat("cloth", CLOTH), k.mat("cord", CORD), k.mat("earth", EARTH)
    k.prim("ico", cloth, (0, 0, 0.5), (1.2, 1.1, 0.95), sub=2, smooth=0.7)
    k.prim("cone", cloth, (0, 0, 1.06), (0.66, 0.6, 0.5), v=9, r2=0.15, smooth=0.5)
    k.prim("cyl", cord, (0, 0, 1.2), (0.4, 0.4, 0.11), v=9, smooth=0.3)
    k.prim("cone", cloth, (0.2, 0, 1.4), (0.26, 0.12, 0.46), (0, 32, 0), v=5, r2=0.12, smooth=0.5)
    k.prim("cone", cloth, (-0.19, 0, 1.38), (0.24, 0.12, 0.42), (0, -36, 0), v=5, r2=0.12, smooth=0.5)
    k.prim("ico", earth, (0, 0, 1.34), (0.16, 0.16, 0.14), sub=1)
    k.crown((0, 0, 1.34), 6, 0.95, 0.3, 38, 8)


def concept_b(k):
    """A spade lifting a clod of earth with a sprout in it."""
    wood, metal, earth = k.mat("wood", WOOD), k.mat("metal", METAL), k.mat("earth", EARTH)
    tool = [
        k.prim("ico", metal, (0, 0, 0), (1.15, 0.8, 0.09), sub=2, smooth=0.3),
        k.prim("cyl", metal, (0.6, 0, 0.03), (0.2, 0.2, 0.26), (0, 90, 0), v=7, smooth=0.3),
        k.prim("cyl", wood, (1.0, 0, 0.04), (0.13, 0.13, 0.8), (0, 90, 0), v=7, smooth=0.3),
        k.prim("cyl", wood, (1.4, 0, 0.04), (0.12, 0.12, 0.4), (90, 0, 0), v=7, smooth=0.3),
        k.prim("ico", earth, (-0.03, 0, 0.2), (0.78, 0.64, 0.44), sub=2, smooth=0.7),
    ]
    rot = Matrix.Rotation(math.radians(-34), 4, "Y") @ Matrix.Rotation(math.radians(38), 4, "X")
    k.transform(tool, rot)
    top = rot @ Vector((-0.03, 0, 0.38))
    k.crown(tuple(top), 5, 0.75, 0.27, 34, 8)


def concept_c(k):
    """A sprout in a carved wooden bucket."""
    wood, cord, earth = k.mat("wood", WOOD), k.mat("metal", METAL), k.mat("earth", EARTH)
    k.prim("cone", wood, (0, 0, 0.4), (1.0, 1.0, 0.8), v=9, r2=0.62, smooth=0.12)
    k.prim("torus", cord, (0, 0, 0.17), (1.07, 1.07, 1.0), minor=0.045, v=13)
    k.prim("torus", cord, (0, 0, 0.58), (1.2, 1.2, 1.0), minor=0.045, v=13)
    k.prim("ico", earth, (0, 0, 0.78), (1.2, 1.2, 0.30), sub=2, smooth=0.7)
    k.prim("torus", wood, (0, 0, 0.8), (1.26, 1.26, 1.0), minor=0.05, v=13)
    k.crown((0, 0, 0.88), 6, 0.78, 0.25, 36, 8)


def concept_d(k):
    """A bundle of roots tied with cord under a leaf crown."""
    root, cord, earth = k.mat("root", ROOTS), k.mat("cord", CORD), k.mat("earth", EARTH)
    top = 1.15
    k.prim("cone", root, (0, 0, top / 2), (0.4, 0.4, top), (180, 0, 0), v=5, smooth=0.5)
    fan = [(-20, 20, 0.95), (16, 60, 0.9), (-12, 105, 0.85), (22, 150, 0.95), (-24, 200, 0.8), (14, 250, 0.9), (-18, 300, 0.85)]
    for tilt, yaw, length in fan:
        m = Matrix.Rotation(math.radians(yaw), 4, "Z")
        ob = k.prim("cone", root, (0, 0, -length / 2), (0.2, 0.2, length), (180, 0, 0), v=5, smooth=0.5)
        ob.data.transform(Matrix.Translation((0, 0, top)) @ m @ Matrix.Rotation(math.radians(tilt), 4, "X"))
    k.prim("ico", earth, (0, 0, top + 0.02), (0.6, 0.56, 0.34), sub=2, smooth=0.7)
    k.prim("cyl", cord, (0, 0, top - 0.3), (0.5, 0.5, 0.15), v=9, smooth=0.3)
    k.prim("cone", cord, (0.24, -0.12, top - 0.52), (0.09, 0.09, 0.4), (170, 25, 0), v=5, smooth=0.5)
    k.prim("cone", cord, (-0.22, -0.14, top - 0.5), (0.09, 0.09, 0.34), (170, -20, 0), v=5, smooth=0.5)
    k.crown((0, 0, top + 0.12), 6, 0.9, 0.3, 30, 8)


def concept_e(k):
    """A turf block with a berry bush standing on it."""
    earth, grass, stone = k.mat("earth", EARTH), k.mat("grass", GRASS), k.mat("stone", STONE)
    berry = k.mat("berry", BERRY)
    d, m, lt = k.mat("leaf_d", LEAF_D), k.mat("leaf_m", LEAF_M), k.mat("leaf_l", LEAF_L)
    k.prim("cube", earth, (0, 0, 0.17), (1.25, 0.95, 0.34), smooth=0.2)
    k.prim("cube", grass, (0, 0, 0.375), (1.32, 1.02, 0.11), smooth=0.25)
    for x, z, s in ((-0.35, 0.12, 0.17), (0.3, 0.2, 0.14), (0.05, 0.08, 0.12)):
        k.prim("ico", stone, (x, -0.5, z), (s * 1.2, s * 0.6, s), sub=1)
    for x, y in ((-0.5, -0.38), (0.45, -0.4), (0.0, -0.46)):
        k.prim("cone", grass, (x, y, 0.47), (0.12, 0.12, 0.13), v=4, smooth=0.4)
    blobs = ((0, 0, 0.72, 0.42, d), (-0.3, -0.05, 0.62, 0.32, d), (0.32, 0, 0.64, 0.34, m),
             (-0.12, -0.14, 0.9, 0.34, m), (0.15, -0.12, 0.98, 0.3, lt), (-0.28, -0.14, 0.76, 0.26, lt))
    for x, y, z, r, mt in blobs:
        k.prim("ico", mt, (x, y, z), (r * 2,) * 3, sub=1, smooth=0.7)
    for x, y, z in ((-0.1, -0.36, 0.66), (0.22, -0.32, 0.86), (-0.33, -0.3, 0.88), (0.05, -0.34, 1.08), (0.4, -0.22, 0.66)):
        k.prim("ico", berry, (x, y, z), (0.15, 0.15, 0.15), sub=1, smooth=0.8)


CONCEPTS = {"A": concept_a, "B": concept_b, "C": concept_c, "D": concept_d, "E": concept_e}


def fit_camera(scene, objs, elevation=24.0, yaw=28.0, fill=0.86):
    el, ya = math.radians(elevation), math.radians(yaw)
    d = Vector((math.sin(ya) * math.cos(el), -math.cos(ya) * math.cos(el), math.sin(el)))
    quat = (-d).to_track_quat("-Z", "Y")
    inv = quat.to_matrix().to_4x4().inverted()
    pts = [inv @ v.co for ob in objs for v in ob.data.vertices]
    xs, ys = [p.x for p in pts], [p.y for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys))
    right, up = quat @ Vector((1, 0, 0)), quat @ Vector((0, 1, 0))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.location = d * 12 + right * cx + up * cy
    cam.rotation_euler = quat.to_euler()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = span / fill
    cam.data.clip_end = 60
    scene.camera = cam


def lights(scene):
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    scene.collection.objects.link(sun)
    sun.data.energy = 3.4
    sun.data.angle = math.radians(28)
    sun.data.color = (1.0, 0.97, 0.92)
    sun.rotation_euler = (math.radians(55), 0, math.radians(-20))
    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    g = paint.Graph(world.node_tree)
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


def render(tag, outdir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    kit = Kit(CFG)
    CONCEPTS[tag](kit)
    zmin = min(v.co.z for ob in kit.objs for v in ob.data.vertices)
    kit.transform(kit.objs, Matrix.Translation((0, 0, -zmin)))
    kit.finish()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    fit_camera(scene, kit.objs)
    lights(scene)
    scene.render.filepath = os.path.join(outdir, "raw_%s.png" % tag)
    bpy.ops.render.render(write_still=True)
    scene.render.film_transparent = False
    print("rendered", tag)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = args[0] if args else "all"
    outdir = args[1] if len(args) > 1 else os.path.join(ROOT, "renders", "lhm-72b")
    os.makedirs(outdir, exist_ok=True)
    for tag in (list(CONCEPTS) if which == "all" else [which]):
        render(tag, outdir)


main()
