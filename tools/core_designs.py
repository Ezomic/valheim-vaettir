"""
Four glowing cores for the forest spirit, on the same body, for comparison.

    blender --background --python tools/core_designs.py

The problem being solved is not "make it glow" - it is "make it glow without reading
as Mistlands". Mistlands owns lit green in this game, and it owns it three ways at
once:

  * COLD colour   - wisps and Dvergr lanterns are teal and pale green-white
  * SOFT edge     - diffuse haze and particles, no boundary you could point at
  * NO MATERIAL   - light floating free, or hung in a lamp; nothing around it

So all four of these are warm amber rather than teal, all four are hard-edged
geometry rather than haze, and all four have wood *in front of* the light rather than
light in front of wood. That last one matters most: a wisp is a light with nothing
around it, and every one of these is a hole in something with light behind it.

Amber also earns its place in the fiction. The thing grew on greydwarf blood in a
black forest, and what glows warm in a tree is sap.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (box, camera, clear_scene, disc, finish, limb, material,
                     reference_cube, render, roots, stage_scene, taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

# The chest faces +y, which is where the camera stands.
CHEST = 1.15
FRONT = 0.17


# --------------------------------------------------------------------------- cores

def core_knot():
    """
    Growth rings. A cut branch end set into the chest, glowing between the rings.

    The strongest anti-Mistlands move available, because concentric rings are a thing
    wood does and a thing light does not. No wisp has structure inside it; this is
    almost entirely structure, with the glow only in the gaps.
    """
    disc(0.185, 0.05, (0.0, FRONT - 0.03, CHEST), "bark", sides=13)
    disc(0.150, 0.05, (0.0, FRONT + 0.005, CHEST), "core", sides=13)
    disc(0.112, 0.05, (0.0, FRONT + 0.015, CHEST), "bark", sides=11)
    disc(0.076, 0.05, (0.0, FRONT + 0.025, CHEST), "core", sides=11)
    disc(0.034, 0.05, (0.0, FRONT + 0.035, CHEST), "bark", sides=9)


def core_ring():
    """
    An annulus: glow around a dark hollow centre.

    Reads as a socket or a wreath rather than as an orb. The hollow is what does the
    work - a wisp is bright in the middle, and this is dark exactly there.
    """
    disc(0.195, 0.06, (0.0, FRONT - 0.03, CHEST), "bark", sides=13)
    disc(0.160, 0.05, (0.0, FRONT + 0.005, CHEST), "core", sides=13)
    disc(0.095, 0.07, (0.0, FRONT + 0.02, CHEST), "bark", sides=11)


def core_well():
    """
    A bored socket with the light at the bottom of it.

    The only one with real depth: a proud bark rim throws the inside into shadow, so
    the glow is only fully visible head-on and dims as you walk around it. Nothing
    floating can do that, because depth needs something solid to be deep *in*.
    """
    taper(0.20, 0.185, 0.16, (0.0, FRONT - 0.02, CHEST), "bark", sides=13, rot_x=90.0)
    disc(0.135, 0.04, (0.0, FRONT - 0.075, CHEST), "core", sides=13)

    # Two bars across the mouth. They break the circle into arcs, which is the
    # difference between a lit hole and a headlamp.
    for angle in (24, 108):
        box((0.34, 0.05, 0.045), (0.0, FRONT + 0.03, CHEST), "bark",
            rot_x=90.0, rot_y=angle, tilt=1.0)


def core_cracked():
    """
    A closed bark disc split by radial cracks, lit from behind.

    The most restrained: almost no glowing area at all, just lines of it. If the
    others read as too much light this is the answer, and it is the one that best
    says "there is something inside this creature" rather than "this creature has a
    lamp on it".
    """
    disc(0.155, 0.06, (0.0, FRONT - 0.005, CHEST), "core", sides=13)
    disc(0.190, 0.05, (0.0, FRONT - 0.035, CHEST), "bark", sides=13)

    # Spokes laid across the face of the disc. rot_y spins them within the plane of
    # the disc once rot_x has stood it up, so each one is a crack at its own angle.
    for angle in (8, 62, 128, 196, 254, 312):
        box((0.34, 0.05, 0.05), (0.0, FRONT + 0.012, CHEST), "bark",
            rot_x=90.0, rot_y=angle, tilt=1.0)


CORES = (
    ("grove_core_knot", core_knot),
    ("grove_core_ring", core_ring),
    ("grove_core_well", core_well),
    ("grove_core_cracked", core_cracked),
)


# --------------------------------------------------------------------------- body

def warden(core):
    """The chosen body, with the core left to the caller."""
    for x, bow in ((-0.20, -7.0), (0.21, 6.0)):
        roots((x, 0.0, 0.14), 0.15)
        limb((x, 0.0, 0.10), 0.58, 3, 0.125, 0.095, bow, 90.0 if x > 0 else -90.0,
             -bow * 0.5, "bark")

    taper(0.30, 0.26, 0.20, (0.0, 0.0, 0.74), "bark")

    # Ribs on the flanks. 0 degrees is +x and 90 is +y, so the previous 58/122 pair
    # were both on the *front* and barred the core behind three posts - a fence in
    # front of a window. These sit at the sides where they belong.
    for angle in (22.0, 158.0, 202.0, 338.0):
        rad = math.radians(angle)
        limb((math.cos(rad) * 0.21, math.sin(rad) * 0.21, CHEST - 0.28),
             0.56, 3, 0.055, 0.048, -5.0, angle, 5.0, "bark")

    taper(0.26, 0.34, 0.24, (0.0, 0.0, 1.42), "bark")
    taper(0.34, 0.27, 0.20, (0.0, 0.0, 1.58), "moss", sides=9)
    taper(0.37, 0.32, 0.09, (0.0, 0.0, 1.50), "moss", sides=9)

    for x, yaw, curve in ((-0.40, -90.0, -5.0), (0.41, 90.0, 4.0)):
        limb((x, 0.04, 1.46), 1.00, 4, 0.085, 0.052, 175.0, yaw, curve, "bark")

    taper(0.16, 0.13, 0.22, (0.0, -0.02, 1.72), "bark")

    for x, yaw, spread in ((-0.11, -74.0, 20.0), (0.12, 82.0, 26.0)):
        tip = limb((x, 0.0, 1.80), 0.46, 3, 0.055, 0.035, spread, yaw, 9.0, "bark")
        limb(tip, 0.30, 2, 0.036, 0.018, spread + 34.0, yaw + 16.0, 12.0, "bark", sides=4)
        limb(tip, 0.22, 2, 0.030, 0.015, spread - 26.0, yaw - 22.0, 10.0, "bark", sides=4)

    core()


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, core in CORES:
        clear_scene()
        warden(core)
        obj = finish(name)

        tint()
        stage_scene()
        # Close, and framed on the chest. A core is judged at the distance you would
        # actually look at one, not from across a clearing.
        camera((-1.05, 1.62, 1.34), (0.0, 0.0, 1.20), lens=58)
        render(os.path.join(PREVIEWS, name + ".png"), width=620, height=560)
        print("DESIGN_OK %s tris=%d" % (name, len(obj.data.polygons)))

    # And one at the distance it will actually be met, to check the glow still reads.
    clear_scene()
    warden(core_knot)
    finish("far")
    tint()
    stage_scene()
    reference_cube((1.85, 0.10, 0.50))
    camera((-2.70, 3.90, 1.75), (0.0, 0.0, 1.15))
    render(os.path.join(PREVIEWS, "grove_core_distance.png"))
    print("DESIGN_OK grove_core_distance")


main()
