"""
The vocabulary every LHM-69 design is built from, plus the file-level measurements.

It is deliberately small, because the baseline named the problem as the opposite of small:
25 to 60 thin parts per model. Vanilla's timber props read as FEW LARGE pieces of wood that
lean and overlap, so the helpers here are timbers, slabs, logs, lashings and baskets, and
nothing that makes it easy to add a fourth hundred little sticks.
"""

import math
import os
import random

import bpy

from mathutils import Matrix, Vector

from rm_common import *          # noqa: F401,F403
from rm_common import _bounds    # noqa: F401
import vhbuild
from vhbuild import box, export, finish, orb, shell, wobble, write_col

from rm_common import VARIANTS, shoot


def _frame(direction, across=(1.0, 0.0, 0.0)):
    """Euler angles putting local Z along `direction` and local X as near to `across` as
    it can go. Explicit rather than track-to, so a leg leaning 5 degrees keeps its wide
    face the same way round as the same leg standing upright."""
    z = Vector(direction).normalized()
    a = Vector(across)
    x = a - z * a.dot(z)
    if x.length < 1e-6:
        x = Vector((0.0, 1.0, 0.0)) - z * z.y
    x.normalize()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_euler("XYZ")


def _skin(obj, mat, nobevel=False):
    obj.data.materials.append(vhbuild.material(mat))
    if nobevel:
        obj["nobevel"] = True
    return obj


def strut(p0, p1, wide, thick, mat="wood", across=(1.0, 0.0, 0.0), tilt=1.0):
    """
    A square-section timber from p0 to p1.

    Splayed legs and diagonal braces are most of what makes vanilla's workbench read as
    built rather than assembled, and they are all one call here. `wide` runs along
    `across`, `thick` along the other cross-section axis.
    """
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt, 0.004)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(p0 + p1) / 2.0 + Vector((dx, dy, dz)))
    obj = bpy.context.active_object
    obj.scale = (wide, thick, d.length * (1.0 + random.uniform(-0.012, 0.012)))
    e = _frame(d, across)
    obj.rotation_euler = (e.x + math.radians(jx), e.y + math.radians(jy),
                          e.z + math.radians(jz))
    return _skin(obj, mat)


def slab(size, loc, mat="wood", rot=(0.0, 0.0, 0.0), tilt=1.0):
    """An axis-aligned plank or board. Width varies a little: boards are not cut to one
    figure."""
    sx, sy, sz = size
    return box((sx * (1.0 + random.uniform(-0.015, 0.015)), sy, sz), loc, mat,
               rot_x=rot[0], rot_y=rot[1], rot_z=rot[2], tilt=tilt)


def block(bottom, top, height, loc, mat="wood", tilt=0.8):
    """A tapering rectangular solid: bottom and top are (width, depth). The chest's own
    body is one of these, wider at the lid than at the feet."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    obj = bpy.context.active_object
    for v in obj.data.vertices:
        w, d = (top if v.co.z > 0 else bottom)
        v.co.x = math.copysign(w / 2.0, v.co.x)
        v.co.y = math.copysign(d / 2.0, v.co.y)
        v.co.z = math.copysign(height / 2.0, v.co.z)
    (jx, jy, jz), _ = wobble(tilt, 0.0)
    obj.rotation_euler = (math.radians(jx), math.radians(jy), math.radians(jz))
    return _skin(obj, mat)


def log(p0, p1, r0, r1=None, mat="wood", sides=7, tilt=1.5):
    """A round timber, odd-sided so it always shows an edge. r0 is at p0."""
    p0, p1 = Vector(p0), Vector(p1)
    r1 = r0 if r1 is None else r1
    d = p1 - p0
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt, 0.003)
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1, depth=d.length,
                                    location=(p0 + p1) / 2.0 + Vector((dx, dy, dz)))
    obj = bpy.context.active_object
    spin = random.uniform(0.0, 6.28)
    e = _frame(d, (math.cos(spin), math.sin(spin), 0.0))
    obj.rotation_euler = (e.x + math.radians(jx), e.y + math.radians(jy),
                          e.z + math.radians(jz))
    return _skin(obj, mat, nobevel=True)


def band(centre, axis, radius, width, mat="rope", sides=9):
    """
    A lashing: one thin cylinder around a timber, standing proud of it.

    A hoop is one cylinder, not a ring of blocks. Vanilla paints its straps into the
    texture, so a modelled band earns its place only where it ties two timbers together
    and the join would otherwise read as two sticks touching.
    """
    ax = Vector(axis).normalized()
    c = Vector(centre)
    return log(c - ax * width / 2.0, c + ax * width / 2.0, radius, radius, mat=mat,
               sides=sides, tilt=0.6)


def creel(centre, r0, r1, height, mat="bark", wall=0.024, sides=9):
    """
    A basket, open at the top, with a real wall.

    The old creels shipped as zero-thickness shells and were see-through from inside.
    shell() solidifies inward, so the silhouette a design is picked on is the outer skin.
    """
    cx, cy, cz = centre
    body = _skin_nobevel(shell(r0, r1, height, centre, mat, sides=sides, thickness=wall))
    # A wicker rim: a thin tube hugging the top outside, so a basket does not read as a
    # wooden pail. A tube rather than a disc, because a disc across the mouth is a lid.
    _skin_nobevel(shell(r1 + 0.012, r1 + 0.012, 0.05, (cx, cy, cz + height / 2.0 - 0.02),
                        "rope", sides=sides, thickness=0.02))
    return body


def _skin_nobevel(obj):
    obj["nobevel"] = True
    return obj


def heartwood(at, radius=0.09, stretch=1.2):
    return _skin_nobevel(orb(radius, at, "core", subdivisions=2, stretch=stretch, tilt=3.0))


def bevel_each(width=0.022):
    """
    One-segment chamfer per object, scale applied first.

    vhbuild.bevel_all does not apply scale before the modifier, and a bevel measured in a
    unit cube's local space is stretched by the object's non-uniform scale: a 14mm bevel on
    a 1.1 x 0.14 x 0.52 box is three different widths. Applying scale first makes the
    chamfer equal on every face. Width is capped at a quarter of the thinnest side so a
    thin board does not bevel itself away. Vanilla timber is chunkier than the 14mm the old
    models used, and one segment is plenty at eye distance (Kynda cut barrels from 27k to
    6.4k triangles on exactly that).
    """
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or obj.get("nobevel"):
            continue
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        thin = min(obj.dimensions)
        mod = obj.modifiers.new("chamfer", "BEVEL")
        mod.width = min(width, thin * 0.24)
        mod.segments = 1
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(30.0)
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except RuntimeError:
            obj.modifiers.remove(mod)


def build_model(name):
    """Bevel, join, bake. Returns (object, part count before the join)."""
    parts = len([o for o in bpy.context.scene.objects if o.type == "MESH"])
    bevel_each()
    obj = finish(name, bevel=False)
    return obj, parts


# --------------------------------------------------------------------------- the file

def obj_measure(path):
    """
    Triangles, open edges and loose parts, read from the .obj that would ship.

    Open edges use the same test as own-profile/check-models.py: an edge used by exactly
    one face. Parts are connected components over vertex indices, which for these models
    is the number of separate timbers, since overlapping parts are joined but not welded.
    """
    from collections import defaultdict
    edges = defaultdict(int)
    parent = {}

    def find(x):
        while parent.setdefault(x, x) != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    tris = 0
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            if not line.startswith("f "):
                continue
            idx = [p.split("/")[0] for p in line.split()[1:]]
            tris += max(0, len(idx) - 2)
            for i in range(len(idx)):
                a, b = idx[i], idx[(i + 1) % len(idx)]
                edges[(a, b) if a < b else (b, a)] += 1
                parent[find(a)] = find(b)
    islands = len({find(x) for x in list(parent)})
    return tris, sum(1 for c in edges.values() if c == 1), islands


def export_variant(obj, name):
    os.makedirs(VARIANTS, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    export(obj, name, VARIANTS)
    write_col(os.path.join(VARIANTS, name + ".col"))
    return os.path.join(VARIANTS, name + ".obj")


def icon(objs, path, size=128):
    """
    Front-on orthographic icon on a transparent film, with its own exposure.

    film_transparent is switched back off by shoot(): it is scene state, and the next
    staged shot in the same run came out with a white void for a sky the one time it was
    left on (kynda 2026-08).
    """
    lo, hi = _bounds(objs)
    centre = (lo + hi) / 2.0
    ext = max(hi.x - lo.x, hi.z - lo.z) * 1.12
    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    sun = bpy.context.active_object
    sun.data.energy = 2.2
    sun.rotation_euler = (math.radians(52.0), 0.0, math.radians(200.0))
    world = bpy.data.worlds.new("iw")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.8, 0.8, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    bpy.ops.object.camera_add(location=(centre.x, 8.0, centre.z),
                              rotation=(math.radians(90), 0, math.radians(180)))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ext
    bpy.context.scene.camera = cam
    shoot(path, size, size, transparent=True)
