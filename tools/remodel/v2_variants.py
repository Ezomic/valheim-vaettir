"""
LHM-69 round 2: the twelve candidates redone to the measured rules.

    blender --background --python tools/remodel/tx_round2.py

Round 1 survived the texture test on silhouette and proportion (see STYLE_GAP.txt) but
failed three of the numbers measured off the vanilla rips:

  1. Boards. Vanilla builds from 4 to 9 cm planks laid side by side (the workbench's median
     large part is 6.3 cm thick, 24 cm wide). Round 1 used single 12 to 17 cm slabs.
  2. Thin modelled detail. 24% of the workbench's triangles, 63% of the dvergr shelf's and
     most of the barrel's sit in parts under 4 cm thick: straps, collars, brackets, handles.
     Round 1 had about 3%. Here every joint that a vanilla piece would tie gets a collar.
  3. Grey. Round 1 used an iron group for locks and handles. Vanilla keeps its straps in the
     dark leather of the same sheet. Round 2 uses no iron at all.

Same twelve concepts, same footprints, same heartwood group at the same size: the point is
whether the language is right, and a new outline would hide that.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_detail import *          # noqa: E402,F401,F403
import math                      # noqa: E402
from rm_parts import band as _band  # noqa: E402


def band(centre, axis, radius, width, mat="rope", sides=9):
    """Lashings are straps: under 4 cm wide, like the bench's."""
    return _band(centre, axis, radius, min(width, 0.036), mat, sides)


def ringpts(n, radius, start=90.0):
    return [(math.cos(math.radians(start + i * 360.0 / n)) * radius,
             math.sin(math.radians(start + i * 360.0 / n)) * radius) for i in range(n)]


def hang(x, y, rim_z, pole_z, half=0.12):
    for s in (-1, 1):
        strut((x, y, pole_z), (x + s * half, y, rim_z), 0.022, 0.022, "rope", tilt=0.5)


def seat(x, y, rope_z, z):
    for s in (-1, 1):
        strut((x, y, rope_z), (x + s * 0.11, y, z + 0.02), 0.024, 0.024, "rope", tilt=0.5)
    planks_flat((x, y, z), 0.28, 0.22, 2, 0.04)
    heartwood((x, y, z + 0.12))


def leg_x(s, z, top=0.55, foot=0.64, zt=1.10):
    return s * (foot + (top - foot) * (z + 0.12) / (zt + 0.12))


# ----------------------------------------------------------------------------- post

def post_a():
    for s in (-1, 1):
        leg((s * 0.64, 0, -0.12), (s * 0.55, 0, 1.10), 0.58, 0.15, across=(0, 1, 0))
        for z in (0.22, 0.95):
            collar((leg_x(s, z), 0, z), (0.19, 0.62, 0.05))
    boards((0, -0.26, 0.60), 1.14, 4, 0.05, 0.90)
    planks_flat((0, 0, 0.30), 1.14, 0.56, 2, 0.07)
    planks_flat((0, -0.01, 0.72), 1.14, 0.52, 2, 0.06)
    for z in (0.33, 0.46):
        slab((1.14, 0.06, 0.12), (0, 0.27, z))
    slab((1.50, 0.12, 0.12), (0, 0, 0.14))
    for s in (-1, 1):
        collar((s * 0.69, 0, 0.14), (0.05, 0.16, 0.16))
    planks_flat((0, 0, 1.17), 1.54, 0.70, 3, 0.08)
    slab((0.34, 0.26, 0.06), (0, 0.20, 1.26))
    for s in (-1, 1):
        strut((s * 0.09, 0.20, 1.26), (s * 0.23, 0.20, 1.62), 0.11, 0.10)
        collar((s * 0.115, 0.20, 1.31), (0.14, 0.14, 0.04))
    heartwood((0, 0.20, 1.44))
    collide((0, 0, 0.62), (1.52, 0.68, 1.26))


def post_b():
    body = block((0.88, 0.52), (1.08, 0.66), 1.04, (0, 0, 0.62))
    body["nobevel"] = True
    for sx in (-1, 1):
        for sy in (-1, 1):
            leg((sx * 0.50, sy * 0.28, -0.10), (sx * 0.39, sy * 0.21, 0.28), 0.17, 0.17)
    planks_flat((0, 0, 1.20), 1.18, 0.74, 3, 0.09)
    planks_flat((0, 0, 1.29), 0.98, 0.56, 2, 0.07)
    boards((0, 0.31, 0.62), 0.92, 4, 0.04, 0.94, rot=(-4, 0, 0))
    for z in (0.34, 0.90):
        collar((0, 0, z), (1.0 + 0.1 * (z - 0.6), 0.62, 0.05))
    for sx in (-1, 1):
        slab((0.05, 0.05, 0.34), (sx * 0.18, 0.345, 0.90), "wood", rot=(-4, 0, 0))
        collar((sx * 0.575, 0, 0.92), (0.04, 0.16, 0.05))
    slab((0.42, 0.05, 0.05), (0, 0.35, 1.06), "wood", rot=(-4, 0, 0))
    slab((0.42, 0.05, 0.05), (0, 0.34, 0.73), "wood", rot=(-4, 0, 0))
    heartwood((0, 0.34, 0.90))
    collar((0, 0.33, 0.46), (0.10, 0.03, 0.13))
    collide((0, 0, 0.62), (1.14, 0.72, 1.30))


def post_c():
    r, pitch = 0.105, 0.19
    for c in range(6):
        z = 0.07 + c * pitch
        for sy in (-1, 1):
            if c == 2 and sy == 1:
                log((-0.66, 0.27, z), (-0.19, 0.27, z), r, r * 0.95)
                log((0.19, 0.27, z), (0.66, 0.27, z), r * 0.95, r)
            else:
                log((-0.66, sy * 0.27, z), (0.66, sy * 0.27, z), r, r * 0.96)
        for sx in (-1, 1):
            log((sx * 0.53, -0.37, z + pitch / 2), (sx * 0.53, 0.37, z + pitch / 2), r, r * 0.96)
    top = 0.07 + 5 * pitch + pitch / 2 + r
    planks_flat((0, 0, top + 0.05), 1.42, 0.82, 3, 0.08)
    for sx in (-1, 1):
        log((sx * 0.46, -0.34, top + 0.15), (sx * 0.46, 0.34, top + 0.15), 0.07, 0.07)
        for sy in (-1, 1):
            strut((sx * 0.60, sy * 0.30, -0.10), (sx * 0.60, sy * 0.30, top + 0.02), 0.09, 0.09)
            collar((sx * 0.60, sy * 0.30, 0.30), (0.12, 0.12, 0.05))
            collar((sx * 0.60, sy * 0.30, 0.80), (0.12, 0.12, 0.05))
    heartwood((0, 0.10, 0.07 + 2 * pitch))
    collide((0, 0, 0.64), (1.38, 0.78, 1.26))


# ----------------------------------------------------------------------------- rail

def rail_a():
    for s in (-1, 1):
        leg((s * 0.80, 0, -0.12), (s * 0.70, 0, 1.00), 0.32, 0.15, across=(0, 1, 0))
        collar((s * 0.70, 0, 0.94), (0.19, 0.36, 0.06))
        collar((s * 0.755, 0, 0.45), (0.19, 0.36, 0.05))
    log((-0.94, 0, 0.99), (0.94, 0, 0.99), 0.068, 0.062)
    planks_flat((0, 0, 0.20), 1.42, 0.12, 1, 0.10)
    for x in (-0.45, 0, 0.45):
        creel((x, 0, 0.60), 0.115, 0.165, 0.27)
        hang(x, 0, 0.73, 0.97, half=0.14)
        collar((x, 0, 0.965), (0.05, 0.16, 0.05))
    collide((-0.75, 0, 0.5), (0.34, 0.34, 1.1))
    collide((0.75, 0, 0.5), (0.34, 0.34, 1.1))


def rail_b():
    for s in (-1, 1):
        leg((s * 0.70, 0, -0.12), (s * 0.62, 0, 0.54), 0.42, 0.14, across=(0, 1, 0))
        collar((s * 0.655, 0, 0.45), (0.19, 0.46, 0.05))
    planks_flat((0, 0, 0.58), 1.62, 0.50, 3, 0.09)
    boards((0, -0.22, 0.80), 1.50, 5, 0.05, 0.34)
    planks_flat((0, 0.10, 0.20), 1.20, 0.10, 1, 0.09)
    for x in (-0.38, 0.38):
        creel((x, 0.04, 0.82), 0.205, 0.265, 0.50)
    for s in (-1, 1):
        collar((s * 0.755, -0.22, 0.70), (0.05, 0.12, 0.30))
    collide((0, 0, 0.40), (1.60, 0.50, 0.80))


def rail_c():
    def pole_at(z):
        return (0.0, -0.26 * (z + 0.12) / 1.64, z)

    log(pole_at(-0.12), pole_at(1.52), 0.10, 0.072)
    knot = pole_at(0.78)
    for s in (-1, 1):
        log((s * 0.46, 0.36, -0.08), (knot[0], knot[1] + 0.03, knot[2]), 0.058, 0.05)
    band(knot, (0, 1, 0.2), 0.115, 0.08)
    band(pole_at(0.40), (0, 1, 0.2), 0.115, 0.05)
    for i, z in enumerate((1.02, 1.26, 1.48)):
        s = -1 if i % 2 == 0 else 1
        px, py, pz = pole_at(z)
        tip = (s * 0.32, py, pz + 0.07)
        log((px, py, pz), tip, 0.04, 0.032)
        band((px + s * 0.03, py, pz + 0.01), (1, 0, 0.2), 0.055, 0.04)
        creel((tip[0], py, tip[2] - 0.17), 0.085, 0.125, 0.21)
        hang(tip[0], py, tip[2] - 0.065, tip[2] - 0.01, half=0.085)
    collide((0, 0, 0.7), (0.30, 0.50, 1.50))


# ----------------------------------------------------------------------------- perch

def perch_a():
    log((0, 0, -0.10), (0, 0, 0.80), 0.30, 0.22, sides=9)
    for x, y in ringpts(5, 1.0, 30.0):
        leg((x * 0.44, y * 0.44, -0.08), (x * 0.20, y * 0.20, 0.42), 0.15, 0.11,
            across=(-y, x, 0))
    band((0, 0, 0.70), (0, 0, 1), 0.245, 0.06)
    band((0, 0, 0.30), (0, 0, 1), 0.285, 0.05)
    for x, y in ringpts(3, 1.0, -90.0):
        strut((x * 0.15, y * 0.15, 0.80), (x * 0.31, y * 0.31, 1.26), 0.075, 0.07,
              across=(-y, x, 0))
        band((x * 0.18, y * 0.18, 0.88), (x, y, 1.6), 0.07, 0.04)
    heartwood((0, 0, 1.03))
    collide((0, 0, 0.40), (0.62, 0.62, 0.82))


def perch_b():
    top = 1.36
    for x, y in ringpts(3, 1.0, -90.0):
        log((x * 0.54, y * 0.54, -0.08), (x * 0.05, y * 0.05, top), 0.065, 0.045)
    band((0, 0, top - 0.10), (0, 0, 1), 0.10, 0.12)
    planks_flat((0, 0, 0.78), 0.44, 0.36, 3, 0.05)
    for x, y in ringpts(3, 1.0, -90.0):
        band((x * 0.21, y * 0.21, 0.78), (-y, x, 0), 0.062, 0.06)
        band((x * 0.38, y * 0.38, 0.40), (-y, x, 0), 0.07, 0.04)
    heartwood((0, 0, 0.93))
    collide((0, 0, 0.70), (0.50, 0.50, 1.40))


def perch_c():
    log((0, 0, -0.10), (0, 0, 0.86), 0.145, 0.10, sides=7)
    for s in (-1, 1):
        log((s * 0.02, 0, 0.78), (s * 0.30, 0, 1.38), 0.085, 0.052, sides=7)
        band((s * 0.17, 0, 1.10), (s * 0.30, 0, 0.6), 0.075, 0.05)
    for x, y in ringpts(3, 1.0, 210.0):
        leg((x * 0.34, y * 0.34, -0.08), (x * 0.11, y * 0.11, 0.32), 0.13, 0.10,
            across=(-y, x, 0))
    band((0, 0, 0.84), (0, 0, 1), 0.125, 0.07)
    band((0, 0, 0.35), (0, 0, 1), 0.14, 0.05)
    heartwood((0, 0, 1.06))
    collide((0, 0, 0.50), (0.44, 0.44, 1.0))


# ----------------------------------------------------------------------------- jib

def jib_a():
    leg((0, 0, -0.12), (0, 0, 1.30), 0.24, 0.24, across=(0, 1, 0))
    collar((0, 0, 0.10), (0.28, 0.28, 0.06))
    for sy in (-1, 1):
        strut((0, sy * 0.30, -0.08), (0, sy * 0.06, 0.66), 0.13, 0.12, across=(1, 0, 0))
    planks_flat((0.0, 0, 1.40), 1.72, 0.15, 1, 0.12)
    strut((0.62, 0, 1.50), (-1.10, 0, 1.30), 0.13, 0.15)
    collar((0, 0, 1.31), (0.30, 0.30, 0.05))
    for s in (-1, 1):
        strut((0.56, 0, 1.44), (0.56 + s * 0.09, 0, 1.20), 0.03, 0.03, "rope", tilt=0.4)
    boards((0.56, 0, 1.08), 0.30, 2, 0.30, 0.26, axis="x")
    collar((0.56, 0, 1.08), (0.33, 0.33, 0.04))
    seat(-1.0, 0, 1.31, 0.92)
    collide((0, 0, 0.75), (0.30, 0.50, 1.55))


def jib_b():
    for s in (-1, 1):
        leg((s * 0.58, 0, -0.12), (s * 0.55, 0, 1.46), 0.20, 0.20, across=(0, 1, 0))
        strut((s * 0.55, 0, 0.98), (s * 0.28, 0, 1.40), 0.11, 0.11)
        collar((s * 0.555, 0, 1.02), (0.24, 0.24, 0.05))
        collar((s * 0.56, 0, 0.12), (0.24, 0.24, 0.05))
    planks_flat((0, 0, 1.50), 1.46, 0.20, 2, 0.09)
    band((0, 0, 1.36), (0, 1, 0), 0.05, 0.06)
    seat(0, 0, 1.36, 0.86)
    collide((-0.57, 0, 0.75), (0.24, 0.24, 1.55))
    collide((0.57, 0, 0.75), (0.24, 0.24, 1.55))


def jib_c():
    planks_flat((0, 0, 0.84), 1.40, 0.54, 3, 0.08)
    for s in (-1, 1):
        leg((s * 0.60, 0, -0.12), (s * 0.52, 0, 0.79), 0.46, 0.14, across=(0, 1, 0))
        collar((s * 0.56, 0, 0.70), (0.18, 0.50, 0.05))
    slab((1.16, 0.10, 0.10), (0, 0, 0.30))
    strut((-0.52, -0.12, 0.78), (-0.52, -0.12, 1.66), 0.16, 0.16)
    strut((-0.52, -0.12, 1.54), (0.14, -0.12, 1.54), 0.12, 0.12)
    strut((-0.52, -0.12, 1.14), (-0.16, -0.12, 1.50), 0.10, 0.10)
    collar((-0.52, -0.12, 1.54), (0.20, 0.20, 0.05))
    collar((-0.52, -0.12, 0.90), (0.20, 0.20, 0.05))
    seat(0.10, -0.12, 1.52, 1.18)
    collide((0, 0, 0.50), (1.40, 0.54, 1.02))
    collide((-0.52, -0.12, 1.0), (0.20, 0.20, 1.5))


POST = [("a", post_a, "Trestle cupboard, plank built: layered-plank sides, boards for back, shelf and top, strap collars."),
        ("b", post_b, "Coffer, plank built: unbevelled chest body with boards laid on the front, strap collars, a lit window."),
        ("c", post_c, "Log crib with lashed corner stakes and a three-plank lid.")]
RAIL = [("a", rail_a, "Drying pole on layered-plank posts, strap collars at every joint."),
        ("b", rail_b, "Creel bench with a three-plank top and a boarded back."),
        ("c", rail_c, "Leaning pole with lashed pegs and props.")]
PERCH = [("a", perch_a, "Stump crown with banded stump and lashed tines."),
         ("b", perch_b, "Tripod cradle with a three-plank seat and lashings."),
         ("c", perch_c, "Y-fork with lashed limbs and banded foot.")]
JIB = [("a", jib_a, "Well sweep with a boarded counterweight crate and collars."),
       ("b", jib_b, "Gate hoist on layered-plank posts with two-plank lintel."),
       ("c", jib_c, "Bench crane with a three-plank table.")]
