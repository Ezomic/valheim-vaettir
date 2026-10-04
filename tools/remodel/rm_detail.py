"""
Round 2 helpers, written from what the vanilla rips measured (see STYLE_GAP.txt):

  * vanilla furniture is built from boards 4 to 9 cm thick and 20 to 26 cm wide laid side by
    side, not from one thick slab: the workbench's 28 large parts have a median thickness of
    6.3 cm, its legs are 6 x 24 cm planks. `boards` lays a panel that way, so every seam is a
    real chamfered edge and the grain changes from board to board.
  * 24% of the workbench's triangles, 63% of the shelf's and most of the barrel's are in
    parts under 4 cm thick: straps, brackets, lashings, handles, all MODELLED, all in the dark
    leather/strap colour of the sheet. `collar` is that: a thin wrap around a joint, standing
    1.5 cm proud, in the `rope` group (the dark plank rect), never grey iron.
  * the chest's large body parts are 14 to 22 triangles each, a plain box with no chamfer ring,
    so a big flat body is left unbevelled and the chamfer goes on the boards laid over it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_parts import *           # noqa: F401,F403
from rm_parts import _skin       # noqa: F401


def boards(centre, span, n, thick, height, axis="x", mat="wood", rot=(0.0, 0.0, 0.0), skew=0.01):
    """
    n boards side by side filling `span` along `axis`, each `thick` deep, `height` tall.
    centre is the middle of the panel. Lengths and heights differ by up to 1 cm, because
    hand-laid boards do.
    """
    cx, cy, cz = centre
    width = span / n
    out = []
    for i in range(n):
        off = -span / 2.0 + width * (i + 0.5)
        dz = random.uniform(-skew, skew)
        h = height + random.uniform(-skew, skew) * 2
        if axis == "x":
            out.append(slab((width - 0.004, thick, h), (cx + off, cy, cz + dz), mat, rot=rot))
        else:
            out.append(slab((thick, width - 0.004, h), (cx, cy + off, cz + dz), mat, rot=rot))
    return out


def planks_flat(centre, length, span, n, thick, axis="x", mat="wood"):
    """n planks lying flat, side by side across `span`, running `length` along `axis`."""
    cx, cy, cz = centre
    width = span / n
    out = []
    for i in range(n):
        off = -span / 2.0 + width * (i + 0.5)
        dz = random.uniform(-0.006, 0.006)
        ln = length + random.uniform(-0.02, 0.02)
        if axis == "x":
            out.append(slab((ln, width - 0.004, thick), (cx, cy + off, cz + dz), mat))
        else:
            out.append(slab((width - 0.004, ln, thick), (cx + off, cy, cz + dz), mat))
    return out


def collar(centre, size, mat="rope"):
    """A thin strap wrapped round a joint: a flat block 1.5 cm proud of what it ties."""
    dims = list(size)
    k = dims.index(min(dims))
    dims[k] = min(dims[k], 0.034)        # a strap, not a block: vanilla's are under 4 cm
    return slab(tuple(dims), centre, mat, tilt=0.5)


def leg(p0, p1, wide, thick, across=(1.0, 0.0, 0.0)):
    """A leg made of two layered planks, the way the workbench's are: two thin struts,
    offset and not quite the same length."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    a = Vector(across)
    side = d.cross(a)
    side = side.normalized() if side.length > 1e-6 else Vector((0, 1, 0))
    for s, extra in ((-1, 0.0), (1, 0.05)):
        o = side * (thick * 0.25 * s)
        strut(p0 + o, p1 + o + d.normalized() * extra, wide, thick * 0.52, across=across)


# ----------------------------------------------------------------------------- non-wood

def stone(at, size=0.18, flat=0.7, spin=0.0):
    """
    A weight stone: a faceted lump, 80 triangles. The bench carries 16 of these (13 to 28 cm,
    40 triangles each, 31% of its triangles); they are the 'stone' group, which the runtime
    paints with the bench's own grey island.
    """
    obj = orb(size / 2.0, at, "stone", subdivisions=1, stretch=flat, tilt=6.0)
    obj.scale = (1.0 + random.uniform(-0.15, 0.15), 1.0 + random.uniform(-0.15, 0.15), obj.scale.z)
    obj.rotation_euler.z = math.radians(spin or random.uniform(0, 360))
    obj["nobevel"] = True
    return obj


def hide(edge, width, top=0.30, drop=0.34, thick=0.014, turn=0.0):
    """
    A hide draped over an edge: lies flat for `top` metres, rolls over the edge at `edge`
    (the top outer corner) and hangs `drop` metres, with a ragged lower rim. Width runs along
    x, the drape along y; `turn` rotates the whole thing about z for an edge that runs along
    y. A real sheet of two skins (solidified inward), so it has no open edge.

    The bench has four of these (23 to 27 cm by 44 to 69 cm, 12% of its triangles); the
    runtime paints them with the bench's hide island.
    """
    n_w, prof = 6, [(-top, 0.0), (-top * 0.5, 0.004), (-0.04, 0.012), (0.0, 0.0), (0.035, -0.045),
                    (0.06, -0.12), (0.075, -0.22), (0.085, -1.0)]
    verts, faces = [], []
    for i in range(n_w):
        x = -width / 2.0 + width * i / (n_w - 1)
        ragged = 0.84 + 0.30 * random.random()
        for j, (y, z) in enumerate(prof):
            if z <= -1.0:
                z = -drop * ragged
            elif z < -0.2:
                z = z * (drop / 0.34)
            verts.append((x, y, z))
    for i in range(n_w - 1):
        for j in range(len(prof) - 1):
            a = i * len(prof) + j
            faces.append((a, a + len(prof), a + len(prof) + 1, a + 1))
    mesh = bpy.data.meshes.new("hide")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("hide", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wall = obj.modifiers.new("skin", "SOLIDIFY")
    wall.thickness = thick
    wall.offset = -1.0
    wall.use_rim = True
    bpy.ops.object.modifier_apply(modifier=wall.name)
    obj.location = edge
    obj.rotation_euler.z = math.radians(turn)
    obj.data.materials.append(vhbuild.material("hide"))
    obj["nobevel"] = True
    return obj
