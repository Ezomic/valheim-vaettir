"""
LHM-69: three silhouettes for the stowing post itself.

    blender --background --python tools/remodel/post_variants.py

Written to assets/variants/stow_post_remodel_{a,b,c}.obj, never to assets/, so re-running
cannot put a rejected design in the build menu.

WHAT THE BASELINE SAID (see renders/lhm-69/NOTES.txt): the shipped post is a 7cm-stick
shelving unit on a pale slab with four steel-grey straps and a toy roof. Vanilla's timber
furniture is the opposite on every count: few large pieces (the wood chest is one
tapering body and a lid, the workbench two splayed trestles and a plank top), 10 to 15cm
timber, legs and sides that lean, and iron kept to a handle or a lock. Every candidate
here changes the silhouette AND works to that vocabulary, because a new outline built
from the same thin sticks would be the same problem again.

Rules held to, from CLAUDE.md section 8: parts overlap, few large parts, a bevel per
object before the join (one segment, scale applied first), seeded jitter, odd-sided
cylinders, no open edges, and nothing buried is hollow. Two material groups carry the
bulk (wood, core) and iron stays under 6%: "two groups is vanilla", and vanilla paints
its straps into the texture instead of modelling them.

The heartwood stays a `core` group at the same size, because PostModel finds its glow by
group name and the spirit leaves from it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403


def post_a():
    """
    A: TRESTLE CUPBOARD. Wide and waist-high, two splayed slab sides, a heavy plank top
    that overhangs, and the heartwood cradled between two horns on the top front.

    Workbench vocabulary: the sides are the legs, they lean outward at the foot, a
    through-tenon stretcher pokes out past them, and the top is one thick board.
    Silhouette is a wide low rectangle with a notch of light on top, which the shipped
    post (a tall narrow tower) is not.
    """
    for s in (-1, 1):
        strut((s * 0.64, 0.0, -0.12), (s * 0.55, 0.0, 1.10), 0.60, 0.15, across=(0, 1, 0))

    slab((1.14, 0.07, 0.90), (0.0, -0.26, 0.60))        # back board
    slab((1.14, 0.56, 0.08), (0.0, 0.0, 0.30))          # floor
    slab((1.14, 0.52, 0.07), (0.0, -0.01, 0.72))        # shelf
    slab((1.14, 0.07, 0.26), (0.0, 0.265, 0.38))        # front apron
    slab((1.50, 0.12, 0.12), (0.0, 0.0, 0.14))          # tenon stretcher, past the sides
    slab((1.54, 0.70, 0.13), (0.0, 0.0, 1.17))          # the top

    slab((0.34, 0.26, 0.07), (0.0, 0.20, 1.26))         # seat for the heartwood
    for s in (-1, 1):
        strut((s * 0.09, 0.20, 1.26), (s * 0.23, 0.20, 1.62), 0.12, 0.11)
    heartwood((0.0, 0.20, 1.44))

    collide((0.0, 0.0, 0.62), (1.52, 0.68, 1.26))


def post_b():
    """
    B: COFFER. Tall, tapering, wider at the lid than at the feet, standing on four
    splayed stubs.

    The wood chest's own shape, taken up to waist height, so it reads as a member of the
    same family as the container beside it and not as furniture from another game. The
    heartwood sits in a plank frame set into the upper front, between two battens: a
    lit window, which is how a lantern reads on a chest-like body. Iron is a lock plate
    and two side handles and nothing else.
    """
    block((0.88, 0.52), (1.08, 0.66), 1.04, (0.0, 0.0, 0.62))
    for sx in (-1, 1):
        for sy in (-1, 1):
            strut((sx * 0.50, sy * 0.28, -0.10), (sx * 0.39, sy * 0.21, 0.28), 0.17, 0.17)

    slab((1.18, 0.74, 0.11), (0.0, 0.0, 1.20))          # lid
    slab((0.98, 0.56, 0.08), (0.0, 0.0, 1.29))          # lid step

    for sx in (-1, 1):
        slab((0.11, 0.05, 0.92), (sx * 0.34, 0.31, 0.62), rot=(-4.0, 0.0, 0.0))
        slab((0.06, 0.05, 0.34), (sx * 0.18, 0.325, 0.90), rot=(-4.0, 0.0, 0.0))
    slab((0.42, 0.05, 0.06), (0.0, 0.33, 1.06), rot=(-4.0, 0.0, 0.0))
    slab((0.42, 0.05, 0.06), (0.0, 0.318, 0.73), rot=(-4.0, 0.0, 0.0))
    heartwood((0.0, 0.31, 0.90))

    slab((0.09, 0.03, 0.12), (0.0, 0.31, 0.46), "iron", rot=(-4.0, 0.0, 0.0))
    for sx in (-1, 1):
        slab((0.05, 0.13, 0.04), (sx * 0.57, 0.0, 0.92), "iron", rot=(0.0, 0.0, 0.0))

    collide((0.0, 0.0, 0.62), (1.14, 0.72, 1.30))


def post_c():
    """
    C: LOG CRIB. A log-cabin stack, courses crossing at the corners with the ends left
    proud, a plank lid on runners, and the heartwood glowing through a gap left where one
    front log is shortened.

    The only candidate with no sawn timber below the lid, so it is the roughest and the
    closest to vanilla's wood_stack and log walls. Its silhouette is a stepped square
    with notched corners. The glow is *inside* the piece and shows through a slot, so the
    stow trips read as coming out of the pile.
    """
    r = 0.105
    pitch = 0.19
    for c in range(6):
        z = 0.07 + c * pitch
        for sy in (-1, 1):
            if c == 2 and sy == 1:
                log((-0.66, 0.27, z), (-0.19, 0.27, z), r, r * 0.95)
                log((0.19, 0.27, z), (0.66, 0.27, z), r * 0.95, r)
            else:
                log((-0.66, sy * 0.27, z), (0.66, sy * 0.27, z), r, r * 0.96)
        for sx in (-1, 1):
            log((sx * 0.53, -0.37, z + pitch / 2.0), (sx * 0.53, 0.37, z + pitch / 2.0),
                r, r * 0.96)

    top = 0.07 + 5 * pitch + pitch / 2.0 + r
    slab((1.42, 0.82, 0.10), (0.0, 0.0, top + 0.04))
    for sx in (-1, 1):
        log((sx * 0.46, -0.34, top + 0.14), (sx * 0.46, 0.34, top + 0.14), 0.07, 0.07)

    heartwood((0.0, 0.10, 0.07 + 2 * pitch))
    collide((0.0, 0.0, 0.64), (1.38, 0.78, 1.26))


BUILDERS = [
    ("a", post_a, "Trestle cupboard: wide and waist-high, splayed slab sides, one thick "
                  "top, the heartwood between two horns."),
    ("b", post_b, "Coffer: the wooden chest's tapering body taken up to waist height, "
                  "heartwood in a lit window on the front."),
    ("c", post_c, "Log crib: a notched log-cabin stack with a plank lid, the heartwood "
                  "glowing through a gap in the front course."),
]

if __name__ == "__main__":
    run_piece("post", "stow_post_canopy.obj", BUILDERS, lambda letter: None)
