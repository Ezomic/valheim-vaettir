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


def collar(centre, size, mat="iron"):
    """A thin band wrapped round a joint: iron by default (a forged band), cord on the rail."""
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


# ----------------------------------------------------------------------------- Vaettir's own

def _links(obj):
    obj["nobevel"] = True
    return obj


def plate(centre, size, mat="iron"):
    """A sheet-metal plate standing 1.5 to 2 cm proud of what it is nailed to."""
    return slab(size, centre, mat, tilt=0.4)


def tag(at):
    """
    The post's label: an iron plate hanging on two links from the front, the sign of the chest
    its contents are bound for. Distinct from anything on a vanilla chest or bench.
    """
    x, y, z = at
    for s in (-1, 1):
        r = vhbuild.ring(0.016, 0.005, (x + s * 0.035, y, z + 0.06), "iron", major=7, minor=4)
        _links(r)
    slab((0.13, 0.014, 0.15), (x, y, z - 0.03), "iron", tilt=0.5)
    slab((0.055, 0.016, 0.055), (x, y + 0.004, z - 0.03), "iron", tilt=0.2)


def chain(top, bottom, radius=0.024):
    """A real chain of alternating linked rings (the jib's recipe asks for two chain)."""
    top, bottom = Vector(top), Vector(bottom)
    n = max(2, int((top - bottom).length / (radius * 1.45)))
    for i in range(n):
        p = top + (bottom - top) * ((i + 0.5) / n)
        r = vhbuild.ring(radius, 0.0065, tuple(p), "iron", major=7, minor=4)
        if i % 2:
            r.rotation_euler.z += math.radians(90.0)
        _links(r)


def pulley(at):
    """An iron sheave in two wooden cheeks: the jib lifts, and this is how."""
    x, y, z = at
    for s in (-1, 1):
        slab((0.045, 0.02, 0.15), (x, y + s * 0.045, z), "wood", tilt=0.6)
    wheel = taper(0.07, 0.07, 0.04, (x, y, z - 0.01), "iron", sides=11, rot_x=90.0, tilt=0.5)
    wheel["nobevel"] = True
    pin = taper(0.012, 0.012, 0.14, (x, y, z - 0.01), "iron", sides=5, rot_x=90.0, tilt=0.3)
    pin["nobevel"] = True


def sprig(at, scale=1.0, turn=0.0):
    """
    The family trait: a fan of three fern fronds growing from the piece, each a 1.2 cm closed
    slab whose outline comes from the fiddlehead sheet's alpha. Three, at 120 degrees, leaning
    out 22 degrees.
    """
    x, y, z = at
    for k in range(3):
        a = math.radians(turn + 90.0 + k * 120.0)
        tilt = math.radians(22.0 + 4.0 * k)
        d = Vector((math.sin(tilt) * math.cos(a), math.sin(tilt) * math.sin(a), math.cos(tilt)))
        tip = Vector((x, y, z)) + d * 0.36 * scale
        strut((x, y, z), tip, 0.16 * scale, 0.012, "frond", across=(-math.sin(a), math.cos(a), 0.0),
              tilt=0.3)


def jar(at, height=0.20):
    """A fired-clay water jar: belly and neck, two closed frustums."""
    x, y, z = at
    h1, h2 = height * 0.58, height * 0.42
    taper(0.07, 0.105, h1, (x, y, z + h1 / 2.0), "clay", sides=9, tilt=0.8)
    taper(0.105, 0.06, h2, (x, y, z + h1 + h2 / 2.0 - 0.01), "clay", sides=9, tilt=0.8)
    lip = taper(0.07, 0.07, 0.025, (x, y, z + height - 0.005), "clay", sides=9, tilt=0.5)
    lip["nobevel"] = True


def bowl(at, radius=0.12, height=0.055):
    """A fired-clay seed bowl: a wide shallow frustum with a lip."""
    x, y, z = at
    taper(radius * 0.62, radius, height, (x, y, z + height / 2.0), "clay", sides=11, tilt=0.6)
    lip = taper(radius * 1.04, radius * 1.04, 0.015, (x, y, z + height), "clay", sides=11, tilt=0.4)
    lip["nobevel"] = True


def chain_seat(x, y, hang_z, z):
    """The heartwood seat on a chain from a pulley: replaces the rope V."""
    pulley((x, y, hang_z))
    chain((x, y, hang_z - 0.10), (x, y, z + 0.07))
    hook = vhbuild.ring(0.03, 0.008, (x, y, z + 0.075), "iron", major=8, minor=4)
    _links(hook)
    planks_flat((x, y, z), 0.28, 0.22, 2, 0.04)
    heartwood((x, y, z + 0.12))
