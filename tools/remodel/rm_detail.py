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
