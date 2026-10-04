"""
LHM-69: three silhouettes for the creel rail, the upgrade that grows the post to chest size.

    blender --background --python tools/remodel/rail_variants.py

Run post_variants.py first: each tile stands the rail beside the post of the same letter,
which is how a rail is seen.

The shipped rail is 6,856 triangles, 84% of them coils of wicker, on a 10cm frame: seven
times a vanilla chest for a basket you glance at. The baskets are what carry the idea (the
post holds more because creels hang on it), so every candidate keeps them, as FEW LARGE
baskets with a real wall, and spends the rest of the budget on timber heavy enough to hold
them up. Basket walls are solidified inward, so nothing can be seen through them from
inside (the bug the old rail shipped).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403


def hang(x, y, rim_z, pole_z, half=0.12):
    """Two rope legs from a pole down to a basket rim: an inverted V from the front."""
    for s in (-1, 1):
        strut((x, y, pole_z), (x + s * half, y, rim_z), 0.022, 0.022, "rope", tilt=0.5)


def rail_a():
    """
    A: DRYING POLE. Two heavy splayed posts, one thick round pole resting across them,
    three creels hung from it on rope.

    The tallest and most open of the three, and the closest to what the shipped rail was:
    the difference is weight. Posts are 14 x 30cm slabs leaning outward like the post's
    trestle sides, the pole is a real log, and there are three baskets instead of coils.
    """
    for s in (-1, 1):
        strut((s * 0.80, 0.0, -0.12), (s * 0.70, 0.0, 1.00), 0.32, 0.15, across=(0, 1, 0))
        band((s * 0.70, 0.0, 0.97), (1, 0, 0), 0.115, 0.05)
    log((-0.94, 0.0, 0.99), (0.94, 0.0, 0.99), 0.068, 0.062)
    slab((1.42, 0.10, 0.10), (0.0, 0.0, 0.20))

    for x in (-0.45, 0.0, 0.45):
        creel((x, 0.0, 0.60), 0.115, 0.165, 0.27)
        hang(x, 0.0, 0.73, 0.97, half=0.14)

    collide((-0.75, 0.0, 0.5), (0.34, 0.34, 1.1))
    collide((0.75, 0.0, 0.5), (0.34, 0.34, 1.1))


def rail_b():
    """
    B: CREEL BENCH. A low trestle bench with a back board, two big creels standing on it.

    Lowest and widest, 1.0m tall, so beside the post it reads as a table set against it
    and not a second tower. The baskets sit rather than hang: bigger, fewer, and heavy
    enough to look full. It is the workbench's vocabulary in miniature: one thick top, a
    slab leg at each end, a back rail.
    """
    for s in (-1, 1):
        strut((s * 0.70, 0.0, -0.12), (s * 0.62, 0.0, 0.54), 0.42, 0.14, across=(0, 1, 0))
    slab((1.62, 0.50, 0.11), (0.0, 0.0, 0.58))
    slab((1.50, 0.08, 0.34), (0.0, -0.22, 0.80))
    slab((1.20, 0.10, 0.10), (0.0, 0.10, 0.20))

    for x in (-0.38, 0.38):
        creel((x, 0.04, 0.82), 0.205, 0.265, 0.50)

    collide((0.0, 0.0, 0.40), (1.60, 0.50, 0.80))


def rail_c():
    """
    C: LEANING POLE. One tall pole leaning back, two short props lashed to it, three small
    creels on pegs at different heights.

    The only asymmetric one and the narrowest: 0.9m wide against 1.7 for the others, so it
    is the choice if the rail has to fit where a post stands against a wall. The pegs
    alternate sides and the baskets hang under them, which is what a peg is for.
    """
    def pole_at(z):
        return (0.0, -0.26 * (z + 0.12) / 1.64, z)

    log(pole_at(-0.12), pole_at(1.52), 0.10, 0.072)
    knot = pole_at(0.78)
    for s in (-1, 1):
        log((s * 0.46, 0.36, -0.08), (knot[0], knot[1] + 0.03, knot[2]), 0.058, 0.05)
    band(knot, (0, 1, 0.2), 0.115, 0.07)

    for i, z in enumerate((1.02, 1.26, 1.48)):
        s = -1 if i % 2 == 0 else 1
        px, py, pz = pole_at(z)
        tip = (s * 0.32, py, pz + 0.07)
        log((px, py, pz), tip, 0.04, 0.032)
        creel((tip[0], py, tip[2] - 0.17), 0.085, 0.125, 0.21)
        hang(tip[0], py, tip[2] - 0.065, tip[2] - 0.01, half=0.085)

    collide((0.0, 0.0, 0.7), (0.30, 0.50, 1.50))


BUILDERS = [
    ("a", rail_a, "Drying pole: two heavy splayed posts, a thick pole, three hung creels."),
    ("b", rail_b, "Creel bench: a low trestle bench with a back board and two big creels "
                  "standing on it."),
    ("c", rail_c, "Leaning pole: one tall pole braced by two props, three small creels on "
                  "pegs, the narrowest of the three."),
]

if __name__ == "__main__":
    run_piece("rail", "stow_rail.obj", BUILDERS, post_neighbour)
