"""
Overview: every piece beside the vanilla chest and workbench, shipping row above, round 2
picks below. blender --background --python tools/remodel/tx_overview.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from tx_run import *             # noqa: E402,F401,F403
from tx_run import _cache, _bounds  # noqa: E402

PICKS = [("post", "stow_post_remodel2_b.obj"), ("rail", "stow_rail_remodel2_a.obj"),
         ("perch", "stow_perch_remodel2_c.obj"), ("jib", "hod_jib_remodel2_c.obj")]
SHIPPING = [("post", "stow_post_canopy.obj"), ("rail", "stow_rail.obj"),
            ("perch", "stow_perch.obj"), ("jib", "hod_jib.obj")]


def shoot_row(items, out, classic):
    clear_scene()
    _cache.clear()
    groups = []
    for piece, file in items:
        objs = import_obj(os.path.join(ASSETS if classic else VARIANTS, file))
        (dress_classic if classic else dress_modern)(objs)
        groups.append(objs)
    groups.append(vanilla_chest())
    groups.append(vanilla_bench())
    row_left_to_right(groups, gap=0.5)
    lo, hi = _bounds([o for g in groups for o in g])
    cube_x = lo.x - 1.0
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    ref_cube((cube_x, 0.0, 0.5))
    centre = (hi.x + cube_x - 0.5) / 2.0
    eye_camera(centre, 14.6, min_dist=3.0, aim_z=0.85)
    shoot(out, 2600, 640)


shoot_row(SHIPPING, os.path.join(RENDERS, "overview_shipping.png"), True)
shoot_row(PICKS, os.path.join(RENDERS, "overview_picks.png"), False)
print("OVERVIEW_DONE")
