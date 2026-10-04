"""
LHM-69: three silhouettes for the spirit perch, the upgrade that seats a second spirit.

    blender --background --python tools/remodel/perch_variants.py

Run post_variants.py first: each tile stands the perch beside the post of the same letter.

The shipped perch is a 16cm pole on a pale slab under a birdhouse. It reads as a garden
bird table, which is a fair description of the idea and the wrong object for a world where
the nearest vanilla relative is a stump, a fork or a lashed tripod. Every candidate here is
a thing a Viking would cut from a tree and lash together, holds the heartwood in the open
(the spirit has to be able to come and go from it), and carries no stone and no iron.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403


def ring_points(n, radius, start=90.0):
    return [(math.cos(math.radians(start + i * 360.0 / n)) * radius,
             math.sin(math.radians(start + i * 360.0 / n)) * radius) for i in range(n)]


def perch_a():
    """
    A: STUMP CROWN. A squat cut stump on five root buttresses, three tines curving up
    from its rim to hold the heartwood like a talon holding an egg.

    The lowest and heaviest of the three: 1.2m, wide at the foot, so beside a post it
    reads as something growing at its side and not as a second piece of furniture.
    """
    log((0.0, 0.0, -0.10), (0.0, 0.0, 0.80), 0.30, 0.22, sides=9)
    for x, y in ring_points(5, 1.0, start=30.0):
        strut((x * 0.44, y * 0.44, -0.08), (x * 0.20, y * 0.20, 0.42), 0.15, 0.11,
              across=(-y, x, 0.0))
    band((0.0, 0.0, 0.70), (0, 0, 1), 0.245, 0.05)
    for x, y in ring_points(3, 1.0, start=-90.0):
        strut((x * 0.15, y * 0.15, 0.80), (x * 0.31, y * 0.31, 1.26), 0.075, 0.07,
              across=(-y, x, 0.0))
    heartwood((0.0, 0.0, 1.03))
    collide((0.0, 0.0, 0.40), (0.62, 0.62, 0.82))


def perch_b():
    """
    B: TRIPOD CRADLE. Three round poles splayed from the ground, lashed together at the
    apex, a triangular plank seat lashed across them with the heartwood sitting on it.

    The tallest and the airiest, 1.4m, and the only one you can see all the way through
    from every side, which is what a spirit's perch should be: nothing in front of the
    glow. Silhouette is a pyramid, nothing like the post's rectangle.
    """
    top = 1.36
    for x, y in ring_points(3, 1.0, start=-90.0):
        log((x * 0.54, y * 0.54, -0.08), (x * 0.05, y * 0.05, top), 0.065, 0.045)
    band((0.0, 0.0, top - 0.10), (0, 0, 1), 0.10, 0.10)

    taper(0.24, 0.24, 0.07, (0.0, 0.0, 0.78), "wood", sides=3, tilt=1.0)
    for x, y in ring_points(3, 1.0, start=-90.0):
        band((x * 0.20, y * 0.20, 0.78), (-y, x, 0.0), 0.058, 0.05)
    heartwood((0.0, 0.0, 0.93))
    collide((0.0, 0.0, 0.70), (0.50, 0.50, 1.40))


def perch_c():
    """
    C: Y-FORK. One thick trunk that splits into two limbs, the heartwood nested in the
    fork, three short roots at the foot.

    The narrowest silhouette and the most tree-like: a living thing's shape, and the
    closest of the three to what a spirit would perch in. 1.45m tall, 0.6 wide.
    """
    log((0.0, 0.0, -0.10), (0.0, 0.0, 0.86), 0.145, 0.10, sides=7)
    for s in (-1, 1):
        log((s * 0.02, 0.0, 0.78), (s * 0.30, 0.0, 1.38), 0.085, 0.052, sides=7)
    for x, y in ring_points(3, 1.0, start=210.0):
        strut((x * 0.34, y * 0.34, -0.08), (x * 0.11, y * 0.11, 0.32), 0.13, 0.10,
              across=(-y, x, 0.0))
    band((0.0, 0.0, 0.84), (0, 0, 1), 0.125, 0.06)
    heartwood((0.0, 0.0, 1.06))
    collide((0.0, 0.0, 0.50), (0.44, 0.44, 1.0))


BUILDERS = [
    ("a", perch_a, "Stump crown: a squat stump on root buttresses, three tines holding the "
                   "heartwood."),
    ("b", perch_b, "Tripod cradle: three lashed poles with a plank seat, open on every "
                   "side."),
    ("c", perch_c, "Y-fork: a trunk splitting into two limbs with the heartwood nested in "
                   "the fork, the narrowest."),
]

if __name__ == "__main__":
    run_piece("perch", "stow_perch.obj", BUILDERS, post_neighbour)
