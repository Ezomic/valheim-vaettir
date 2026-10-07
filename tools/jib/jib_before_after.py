"""
Before/after renders of the hod jib: blender --background --python tools/jib/jib_before_after.py -- <before.obj> <after.obj> <out dir> [boxes.json]

Flat Workbench renders (colour by material group), the same cameras for both:
  base    legs and mast base from front-left, slightly above (the angle of Robbin's hover screenshot)
  boom    the boom end from close, slightly above and to the side
  side    the whole piece from the side, a 1.8 m capsule beside it
  front   the whole piece from the front
With a boxes.json from fit_col.py the after model also gets the collision boxes as translucent
wireframes (files named col_*), with a vanilla-workbench-sized block and the capsule for scale.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

COLOURS = {"wood": (0.55, 0.36, 0.2), "bark": (0.38, 0.25, 0.15), "stone": (0.35, 0.35, 0.38),
           "iron": (0.28, 0.32, 0.42), "cord": (0.8, 0.66, 0.3), "wicker": (0.85, 0.7, 0.35),
           "core": (1.0, 0.7, 0.2), "frond": (0.2, 0.7, 0.25)}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_cavity = True
    sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.62, 0.72, 0.85)
    sc.render.film_transparent = False
    sc.display.shading.background_type = "WORLD"


def load(path):
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    for o in objs:
        for slot in o.material_slots:
            m = slot.material
            key = m.name.split(".")[0]
            m.diffuse_color = tuple(COLOURS.get(key, (0.7, 0.7, 0.7))) + (1.0,)
    return objs


def ground():
    bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0, 0))
    g = bpy.context.active_object
    m = bpy.data.materials.new("ground")
    m.diffuse_color = (0.30, 0.50, 0.25, 1.0)
    g.data.materials.append(m)
    # grid lines every metre so a floating or sunk foot reads
    for i in range(-5, 6):
        for axis in (0, 1):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(i if axis == 0 else 0, i if axis == 1 else 0, 0.002))
            c = bpy.context.active_object
            c.scale = (0.01, 14, 0.001) if axis == 0 else (14, 0.01, 0.001)
            mm = bpy.data.materials.new("line")
            mm.diffuse_color = (0.2, 0.35, 0.18, 1.0)
            c.data.materials.append(mm)


def capsule(at):
    mat = bpy.data.materials.new("person")
    mat.diffuse_color = (0.25, 0.32, 0.8, 1.0)
    objs = []
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.4, location=(at[0], at[1], 0.9))
    objs.append(bpy.context.active_object)
    for z in (0.2, 1.6):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.2, location=(at[0], at[1], z))
        objs.append(bpy.context.active_object)
    for o in objs:
        o.data.materials.append(mat)
    return objs


def cam(loc, target, lens=40):
    bpy.ops.object.camera_add(location=loc)
    c = bpy.context.active_object
    c.data.lens = lens
    c.data.clip_end = 100
    d = Vector(target) - Vector(loc)
    c.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = c
    return c


def shoot(path, w=900, h=900):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def views(tag, out, boxes=None):
    # Blender space: the obj import undoes the exporter's swap, so the boom end is at +x, up is +z.
    sets = {
        "base": ((-1.6, -1.9, 1.15), (0.0, 0.1, 0.45), 38, (900, 900)),
        "boom": ((2.0, -1.05, 3.1), (1.12, 0.0, 2.4), 60, (900, 900)),
        "side": ((0.3, -6.0, 1.7), (0.15, 0.0, 1.6), 42, (700, 900)),
        "front": ((7.0, 0.0, 1.7), (0.15, 0.0, 1.6), 42, (700, 900)),
    }
    for name, (loc, tgt, lens, size) in sets.items():
        cam(loc, tgt, lens)
        shoot(os.path.join(out, "%s_%s.png" % (name, tag)), *size)


def add_boxes(boxes):
    for b in boxes["boxes"]:
        bpy.ops.mesh.primitive_cube_add(size=1)
        o = bpy.context.active_object
        o.scale = tuple(b["s"])
        # fit_col.py works in the exported (Unity, mirrored) space; the obj import undoes the swap,
        # so a box has to go through the same swap: x -> -x on the way back, y/z exchanged.
        R = Matrix(b["R"])
        c = Vector(b["c"])
        swap = Matrix(((-1, 0, 0), (0, 0, 1), (0, 1, 0)))
        o.matrix_world = Matrix.Translation(swap @ c) @ (swap @ R).to_4x4()
        o.scale = tuple(b["s"])
        mod = o.modifiers.new("wire", "WIREFRAME")
        mod.thickness = 0.012
        mod.use_even_offset = True
        mat = bpy.data.materials.new("boxwire")
        mat.diffuse_color = (0.95, 0.1, 0.1, 1.0)
        o.data.materials.append(mat)


def walk_test(boxes):
    """Top view with 0.4 m radius player discs stepped along the boom and round the base: red where
    the disc would touch a box that is within 1.8 m of the ground, green where it passes."""
    import math as m
    polys = []
    for b in boxes["boxes"]:
        R = Matrix(b["R"]); c = Vector(b["c"]); h = Vector(b["s"]) / 2
        pts = [c + R @ Vector((sx * h.x, sy * h.y, sz * h.z)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        ys = [p.y for p in pts]
        if min(ys) > 1.8:
            continue
        # Unity (x, y, z) to Blender ground plane (-x, z)
        polys.append([(-p.x, p.z) for p in pts])

    def hull(pp):
        pp = sorted(set(pp))
        def cr(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
        lo = []
        for q in pp:
            while len(lo) >= 2 and cr(lo[-2], lo[-1], q) <= 0: lo.pop()
            lo.append(q)
        up = []
        for q in reversed(pp):
            while len(up) >= 2 and cr(up[-2], up[-1], q) <= 0: up.pop()
            up.append(q)
        return lo[:-1] + up[:-1]

    hulls = [hull(p) for p in polys]

    def hit(x, y, r=0.4):
        for hp in hulls:
            inside = all((hp[(k + 1) % len(hp)][0] - hp[k][0]) * (y - hp[k][1]) - (hp[(k + 1) % len(hp)][1] - hp[k][1]) * (x - hp[k][0]) >= 0 for k in range(len(hp)))
            if inside:
                return True
            for k in range(len(hp)):
                a, bq = hp[k], hp[(k + 1) % len(hp)]
                dx, dy = bq[0] - a[0], bq[1] - a[1]
                t = max(0, min(1, ((x - a[0]) * dx + (y - a[1]) * dy) / max(dx * dx + dy * dy, 1e-9)))
                if m.hypot(x - (a[0] + t * dx), y - (a[1] + t * dy)) < r:
                    return True
        return False

    path = [(x / 10.0, 0.0) for x in range(-5, 16)] + [(1.2, y / 10.0) for y in range(-8, 9)]
    path += [(x / 10.0, -1.1) for x in range(-8, 14)]
    hits = 0
    for (x, y) in path:
        red = hit(x, y)
        hits += red
        bpy.ops.mesh.primitive_cylinder_add(radius=0.4, depth=0.01, location=(x, y, 0.012))
        d = bpy.context.active_object
        mat = bpy.data.materials.new("wt")
        mat.diffuse_color = (0.9, 0.15, 0.1, 1.0) if red else (0.2, 0.8, 0.3, 1.0)
        d.data.materials.append(mat)
    return hits, len(path)


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    before, after, out = a[0], a[1], a[2]
    boxes = json.load(open(a[3])) if len(a) > 3 else None
    os.makedirs(out, exist_ok=True)
    for tag, path in (("before", before), ("after", after)):
        reset()
        load(path)
        ground()
        capsule((-2.2, -0.9))
        views(tag, out)
    if boxes:
        reset()
        load(after)
        ground()
        capsule((-2.2, -0.9))
        add_boxes(boxes)
        views("col", out)
        reset(); load(after); ground(); add_boxes(boxes)
        hits, n = walk_test(boxes)
        cam((0.2, 0.0, 9.0), (0.2, 0.0, 0.0), 35)
        shoot(os.path.join(out, "walk_top.png"), 1100, 900)
        print("JIBWALK red %d of %d discs" % (hits, n))
    print("JIBRENDER done", out)


main()
