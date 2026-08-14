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

from mathutils import Euler, Vector

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


ORBIT = 0.34


def spirit_ring():
    """
    The ring and the motes threaded on it. Exported on its own so it can turn.

    Every mote sits exactly on the ring: same radius, and y=0 so it is in the ring's
    own plane. The first version put them on a 0.34 by 0.30 ellipse and pushed them
    6cm out of plane as well, so the line passed near each bead instead of through
    it - which reads as beads floating beside a hoop rather than as beads on one.
    """
    for i in range(7):
        rad = math.radians(360.0 / 7.0 * i + 12.0)
        orb(0.040, (math.cos(rad) * ORBIT, 0.0, HOVER + math.sin(rad) * ORBIT),
            "core", subdivisions=1, tilt=0.0)

    ring(ORBIT, 0.010, (0.0, 0.0, HOVER), "core", minor=5, tilt=0.0)


def spirit_core():
    """Both halves together, for the preview and for anyone wanting one static mesh."""
    heart()
    spirit_ring()
    collide((0.0, 0.0, HOVER), (0.80, 0.80, 0.80))


def spirit_core_plain():
    """Stripped all the way down: one mass, nothing around it."""
    heart(1.25)
    collide((0.0, 0.0, HOVER), (0.60, 0.60, 0.70))


def spirit_heart_only():
    """The mass on its own, so it can stay upright while the ring turns around it."""
    heart()
    collide((0.0, 0.0, HOVER), (0.80, 0.80, 0.80))


def spirit_ring_only():
    spirit_ring()


# Two meshes, not one. The mass is an upright spheroid, so tumbling the whole model
# would roll it end over end like an egg - and a rolling light has no up, which
# removes the one thing making it read as standing there rather than falling. The
# ring is what turns; the mass stays.
DESIGNS = (
    ("grove_spirit_core", spirit_core),
    ("grove_spirit_heart", spirit_heart_only),
    ("grove_spirit_ring", spirit_ring_only),
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

    # Four points through the tumble. A ring turning on all three axes passes through
    # every orientation including edge-on, where it is a bright line and the motes
    # bunch up - so it has to be checked at more than the one angle that flatters it.
    clear_scene()
    poses = ((0.0, 0.0, 0.0), (52.0, 18.0, 24.0), (86.0, 40.0, 12.0), (28.0, 74.0, 58.0))

    for index, (rx, ry, rz) in enumerate(poses):
        offset = (1.5 - index) * 0.95

        before = set(bpy.data.objects)
        heart()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

        before = set(bpy.data.objects)
        spirit_ring()
        for obj in set(bpy.data.objects) - before:
            pivot = Vector((0.0, 0.0, HOVER))
            turn = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), "XYZ")

            local = obj.location - pivot
            local.rotate(turn)
            obj.location = pivot + local + Vector((offset, 0.0, 0.0))

            spin = obj.rotation_euler.to_matrix()
            obj.rotation_euler = (turn.to_matrix() @ spin).to_euler()

    finish("spin")
    tint()
    stage_scene()
    camera((-0.10, 6.30, 1.45), (0.0, 0.0, HOVER), lens=52)
    render(os.path.join(PREVIEWS, "grove_spirit_spin.png"), width=940, height=420)
    print("DESIGN_OK grove_spirit_spin")


main()
