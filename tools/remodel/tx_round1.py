"""
Round 1 candidates, textured. blender --background --python tools/remodel/tx_round1.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import post_variants, rail_variants, perch_variants, jib_variants     # noqa: E402
from tx_run import *             # noqa: E402,F401,F403

BASES = {"post": "stow_post", "rail": "stow_rail", "perch": "stow_perch", "jib": "hod_jib"}


def neighbour(piece):
    def pick(letter):
        if piece == "post":
            return None
        if letter == "current":
            return (os.path.join(ASSETS, "stow_post_canopy.obj"), True)
        return (os.path.join(VARIANTS, "stow_post_remodel_%s.obj" % letter), False)
    return pick


SETS = [("post", "stow_post_canopy.obj", post_variants.BUILDERS, True),
        ("rail", "stow_rail.obj", rail_variants.BUILDERS, False),
        ("perch", "stow_perch.obj", perch_variants.BUILDERS, False),
        ("jib", "hod_jib.obj", jib_variants.BUILDERS, False)]

for piece, current, builders, chest in SETS:
    run_textured(piece, current, builders, neighbour(piece), "r1",
                 lambda letter, piece=piece: "%s_remodel_%s" % (BASES[piece], letter), chest)
