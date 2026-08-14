"""
Four glowing cores for the forest spirit, on the same body, for comparison.

    blender --background --python tools/core_designs.py

The brief is a Mistlands-spirit *kind* of thing, not a rejection of it. An earlier
pass here got that backwards: it treated every resemblance as a fault and buried the
light behind carved wood, which produced four lit knotholes and no spirit at all.

So these keep what makes a wisp a wisp - soft, floating, luminous, obviously not
solid - and differ on three axes instead:

  * WARM    - gold rather than the teal and pale green-white Mistlands owns
  * BIG     - a wisp is a fist-sized mote; this is a hand-span ring inside a body
  * HOUSED  - a wisp belongs to nothing, and this belongs to something that grew

Bloom is doing as much work here as geometry. An emissive surface with hard edges
reads as painted plastic; light spilling past its own boundary is most of what makes
a thing look like a spirit rather than a lamp.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (box, camera, clear_scene, disc, finish, limb, material,
                     reference_cube, render, ring, roots, stage_scene, taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

# The chest faces +y, which is where the camera stands.
CHEST = 1.15
FRONT = 0.17


# --------------------------------------------------------------------------- cores

def core_halo():
    """
    A ring of light hanging free inside the chest cavity, touching nothing.

    The closest to a wisp, and deliberately so - it floats, it has no housing, it is
    obviously not solid. What makes it ours is that it is a *ring* the size of a hand
    rather than a mote the size of a fist, and that it hangs inside a body.
    """
    ring(0.150, 0.026, (0.0, 0.02, CHEST), "core")


def core_mote_ring():
    """
    One soft mote with smaller ones held in a ring around it.

    A wisp does not come in formation. Several, arranged, reads as something keeping
    them rather than as something that happens to be glowing - which is the whole
    difference between a light and a spirit that has one.
    """
    taper(0.075, 0.075, 0.15, (0.0, 0.02, CHEST), "core", sides=11, tilt=1.0)

    for i in range(7):
        rad = math.radians(360.0 / 7.0 * i + 12.0)
        taper(0.030, 0.030, 0.06,
              (math.cos(rad) * 0.165, 0.02 + math.sin(rad) * 0.03,
               CHEST + math.sin(rad) * 0.165), "core", sides=7, tilt=1.0)


def core_aureole():
    """
    A disc of light behind the whole ribcage, wider than the body opening.

    The light is not in the creature, the creature is standing in front of the light.
    Reads as the biggest and least contained of the four, and the ribs crossing it
    are what stop it being a floodlight.
    """
    disc(0.235, 0.025, (0.0, -0.10, CHEST), "core", sides=17)
    disc(0.300, 0.020, (0.0, -0.15, CHEST), "core", sides=17)


def core_ember():
    """
    A small bright heart with a faint ring drifting out around it.

    The most restrained: nearly all of the read comes from bloom rather than from
    surface area. If the others are too much light, this is the answer - and at
    distance it is the one that most looks like something alive breathing.
    """
    taper(0.055, 0.055, 0.11, (0.0, 0.03, CHEST), "core", sides=9, tilt=1.0)
    ring(0.185, 0.012, (0.0, -0.02, CHEST), "core", minor=5)


CORES = (
    ("grove_core_halo", core_halo),
    ("grove_core_mote_ring", core_mote_ring),
    ("grove_core_aureole", core_aureole),
    ("grove_core_ember", core_ember),
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
    warden(core_halo)
    finish("far")
    tint()
    stage_scene()
    reference_cube((1.85, 0.10, 0.50))
    camera((-2.70, 3.90, 1.75), (0.0, 0.0, 1.15))
    render(os.path.join(PREVIEWS, "grove_core_distance.png"))
    print("DESIGN_OK grove_core_distance")


main()
