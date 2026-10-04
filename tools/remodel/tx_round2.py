"""
Round 2 candidates, built to the measured rules and textured. blender --background --python tools/remodel/tx_round2.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import v2_variants as V             # noqa: E402
from tx_run import *             # noqa: E402,F401,F403

BASES = {"post": "stow_post", "rail": "stow_rail", "perch": "stow_perch", "jib": "hod_jib"}


def neighbour(piece):
    def pick(letter):
        if piece == "post":
            return None
        if letter == "current":
            return (os.path.join(ASSETS, "stow_post_canopy.obj"), True)
        return (os.path.join(VARIANTS, "stow_post_remodel2_%s.obj" % letter), False)
    return pick


SETS = [("post", "stow_post_canopy.obj", V.POST, True),
        ("rail", "stow_rail.obj", V.RAIL, False),
        ("perch", "stow_perch.obj", V.PERCH, False),
        ("jib", "hod_jib.obj", V.JIB, False)]

for piece, current, builders, chest in SETS:
    run_textured(piece, current, builders, neighbour(piece), "r2",
                 lambda letter, piece=piece: "%s_remodel2_%s" % (BASES[piece], letter), chest)
