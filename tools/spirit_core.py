"""
The forest spirit as nothing but its core: a floating light, no body.

    blender --background --python tools/spirit_core.py

Every previous pass hung the glow inside a wooden creature. The creature is gone.
What comes out of the pod is the light itself - which is a much better answer than
the bark figure was, and not only because it is simpler:

  * it is the only thing in the mod that is not made of wood, so it reads as the
    payoff for an hour of feeding a sapling rather than as another forest monster
  * a floating light needs no walk cycle, no ragdoll and no attack animation, none
    of which this mod can author
  * "commune with it and it fades" is what a light does. It was always a strange
    thing to ask of something with legs

Family resemblance to a Mistlands wisp is deliberate and wanted. It parts company on
warmth - gold rather than teal - and on scale and structure: a wisp is a lone
fist-sized mote, and this is knee-high with parts held in an arrangement, which is
what makes it read as something rather than as a spark.

Two readings of "just the core", built to be compared: with the motes it came with,
and stripped to the single mass.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (camera, clear_scene, collide, export, finish, orb,
                     reference_cube, render, ring, stage_scene, tint, write_col)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

# How high it floats. Chest height on a player, so communing with it is eye to eye
# rather than a thing you stoop over.
HOVER = 1.15


def heart(scale=1.0):
    """
    The mass itself. Slightly taller than wide.

    A sphere reads as an object - a ball, a fruit, something you could pick up. An
    upright soft mass reads as a presence, which is the whole difference between the
    spirit being a thing and the spirit being a someone.
    """
    orb(0.150 * scale, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.30)
    orb(0.104 * scale, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.55)


def spirit_core():
    """The fourth design's core at creature scale, motes and all."""
    heart()

    # Held in an arrangement, not scattered. A ring of motes is the one thing here
    # that says something is keeping them - a wisp does not come in formation.
    for i in range(7):
        rad = math.radians(360.0 / 7.0 * i + 12.0)
        orb(0.040, (math.cos(rad) * 0.34, math.sin(rad) * 0.06,
                    HOVER + math.sin(rad) * 0.30), "core", subdivisions=1)

    # A faint ring tying them together, so the motes read as orbiting rather than as
    # crumbs that happen to be nearby.
    ring(0.34, 0.010, (0.0, 0.0, HOVER), "core", minor=5)

    collide((0.0, 0.0, HOVER), (0.80, 0.80, 0.80))


def spirit_core_plain():
    """Stripped all the way down: one mass, nothing around it."""
    heart(1.25)
    collide((0.0, 0.0, HOVER), (0.60, 0.60, 0.70))


DESIGNS = (
    ("grove_spirit_core", spirit_core),
    ("grove_spirit_core_plain", spirit_core_plain),
)


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        build()
        obj = finish(name)

        export(obj, name, ASSETS)
        write_col(os.path.join(ASSETS, name + ".col"))

        tint()
        stage_scene()
        reference_cube((1.30, 0.10, 0.50))
        camera((-1.85, 2.60, 1.45), (0.0, 0.0, HOVER), lens=48)
        render(os.path.join(PREVIEWS, name + ".png"))
        print("DESIGN_OK %s tris=%d" % (name, len(obj.data.polygons)))

    # And at the distance you would first spot one across a clearing, which for a
    # light is the shot that matters most - it is either a landmark or it is nothing.
    clear_scene()
    spirit_core()
    finish("far")
    tint()
    stage_scene()
    reference_cube((3.40, 0.10, 0.50))
    camera((-5.20, 7.40, 2.20), (0.0, 0.0, HOVER), lens=46)
    render(os.path.join(PREVIEWS, "grove_spirit_core_far.png"), width=760, height=520)
    print("DESIGN_OK grove_spirit_core_far")


main()
