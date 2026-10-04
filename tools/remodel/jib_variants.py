"""
LHM-69: three silhouettes for the hod jib, the upgrade that lets a post craft from its chests.

    blender --background --python tools/remodel/jib_variants.py

Run post_variants.py first: each tile stands the jib beside the post of the same letter.

The shipped jib is a post, an arm, a brace and a drop to a triangle with a light under it,
which is the outline of a gibbet and was read that way. The job is the opposite of grim:
it is the thing that fetches materials out of the chests for you. So every candidate
changes the picture the outline makes: a balanced sweep, a gate with a hoist, and a work
table with a short crane over it. The hanging seat for the heartwood is a plank on a rope
V in all three, which cannot be mistaken for a noose.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403


def seat(x, y, rope_z, z):
    """A plank seat on a rope V, the heartwood sitting on it."""
    for s in (-1, 1):
        strut((x, y, rope_z), (x + s * 0.11, y, z + 0.02), 0.024, 0.024, "rope", tilt=0.5)
    slab((0.28, 0.22, 0.05), (x, y, z))
    heartwood((x, y, z + 0.12))


def jib_a():
    """
    A: WELL SWEEP. A thick post with a long beam balanced across its top, a counterweight
    crate hanging from the short end and the heartwood seat from the long end.

    The tallest, 1.8m, and the only one that moves like it should: a sweep is a lever,
    which is what a jib is. Reaches 1.1m out from the post, so it hangs over a chest the
    post is standing next to.
    """
    strut((0.0, 0.0, -0.12), (0.0, 0.0, 1.30), 0.24, 0.24)
    for sy in (-1, 1):
        strut((0.0, sy * 0.30, -0.08), (0.0, sy * 0.06, 0.66), 0.13, 0.13, across=(1, 0, 0))
    strut((0.62, 0.0, 1.50), (-1.10, 0.0, 1.30), 0.13, 0.15)
    band((0.0, 0.0, 1.30), (0, 1, 0), 0.19, 0.06)

    for s in (-1, 1):
        strut((0.56, 0.0, 1.44), (0.56 + s * 0.09, 0.0, 1.20), 0.03, 0.03, "rope", tilt=0.4)
    slab((0.30, 0.30, 0.26), (0.56, 0.0, 1.08))
    seat(-1.0, 0.0, 1.31, 0.92)
    collide((0.0, 0.0, 0.75), (0.30, 0.50, 1.55))


def jib_b():
    """
    B: GATE HOIST. Two square posts with a lintel and knee braces, a hoist rope from the
    middle of the lintel to the heartwood seat.

    Symmetric and open, 1.55m, so it reads as a doorway for the glow to hang in. The
    only one of the three whose outline is a closed frame, and the only one with an iron
    ring, at the rope's top, because that is where a hoist wears metal.
    """
    for s in (-1, 1):
        strut((s * 0.58, 0.0, -0.12), (s * 0.55, 0.0, 1.46), 0.20, 0.20)
        strut((s * 0.55, 0.0, 0.98), (s * 0.28, 0.0, 1.40), 0.11, 0.11)
    slab((1.46, 0.20, 0.18), (0.0, 0.0, 1.50))

    iron_ring = vhbuild.ring(0.05, 0.014, (0.0, 0.0, 1.36), "iron", major=11, minor=5)
    iron_ring["nobevel"] = True
    seat(0.0, 0.0, 1.36, 0.86)
    collide((-0.57, 0.0, 0.75), (0.24, 0.24, 1.55))
    collide((0.57, 0.0, 0.75), (0.24, 0.24, 1.55))


def jib_c():
    """
    C: BENCH CRANE. A trestle work table with a short upright at one end and an arm
    reaching over the middle of the table, the heartwood seat hanging 30cm above it.

    The lowest, 1.65m including the upright, and the one that says "crafting" before the
    player reads a word: it is a workbench with a lantern over it. Footprint 1.4 x 0.5m,
    so it needs the most room beside a post.
    """
    slab((1.40, 0.54, 0.11), (0.0, 0.0, 0.84))
    for s in (-1, 1):
        strut((s * 0.60, 0.0, -0.12), (s * 0.52, 0.0, 0.79), 0.46, 0.14, across=(0, 1, 0))
    slab((1.16, 0.10, 0.10), (0.0, 0.0, 0.30))

    strut((-0.52, -0.12, 0.78), (-0.52, -0.12, 1.66), 0.16, 0.16)
    strut((-0.52, -0.12, 1.54), (0.14, -0.12, 1.54), 0.12, 0.12)
    strut((-0.52, -0.12, 1.14), (-0.16, -0.12, 1.50), 0.10, 0.10)
    band((-0.52, -0.12, 1.54), (0, 1, 0), 0.115, 0.05)
    seat(0.10, -0.12, 1.52, 1.18)
    collide((0.0, 0.0, 0.50), (1.40, 0.54, 1.02))
    collide((-0.52, -0.12, 1.0), (0.20, 0.20, 1.5))


BUILDERS = [
    ("a", jib_a, "Well sweep: a thick post with a balanced beam, counterweight crate on one "
                 "end, the glow seat on the other."),
    ("b", jib_b, "Gate hoist: two posts, a lintel and knee braces, the glow seat on a "
                 "rope from the middle."),
    ("c", jib_c, "Bench crane: a trestle work table with a short upright and arm hanging "
                 "the glow seat over it."),
]

if __name__ == "__main__":
    run_piece("jib", "hod_jib.obj", BUILDERS, post_neighbour)
