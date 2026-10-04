"""
Before and after of the post's material, with the real workbench textures in frame.

    blender --background --python tools/remodel/post_skin_before_after.py

Left to right as the camera sees it: the shipping post as the runtime skins it today
(BEFORE), the same untouched mesh skinned the way PostSkin=workbench does (AFTER), the
vanilla workbench, a 1m cube. One light rig and one material model for all of them, so a
difference on screen is a difference in the donor and the UV fit and nothing else.

The mesh is assets/stow_post_canopy.obj, loaded and never written: this script only reads
it, and the commit that carries it shows no .obj change.

BEFORE approximates the donors the classic skin picks: wood from woodwall (carries its own
_MainTex_ST of -0.56, 0.12), stone from stone_wall_2x1, iron from the stonecutter bench's
sheet. Iron is the loosest of the three: the runtime tries piece_cauldron first, which is
not among the rips on disk.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403
from rm_run import _bounds       # noqa: E402,F401
import skins                     # noqa: E402

POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Measured from the WorkBench_d rip, inset past each island's worn rim (see PostModel.cs).
PLANKS = (0.06, 0.15, 0.84, 0.31)
LASHING = (0.09, 0.60, 0.17, 0.30)


def bench_material():
    return skins.piece_material(
        "bench", skins.rip_path("piece_workbench", "textures", "WorkBench_d.png"),
        skins.rip_path("piece_workbench", "textures", "WorkBench_n.png"), bump=1.0, gloss=0.0)


def build_scene(out, width=2400, height=760, close=False):
    clear_scene()

    # Donor rects first: donor_rect imports and removes its own objects.
    wood_rect = skins.donor_rect("woodwall")
    stone_rect = skins.donor_rect("stone_wall_2x1")
    iron_rect = skins.donor_rect("piece_stonecutter")
    print("DONOR rects wood %s stone %s iron %s" % (wood_rect, stone_rect, iron_rect))

    before = import_obj(POST, ground_it=True)
    after = import_obj(POST, ground_it=True)
    bench = import_obj(os.path.join(RIPS, "piece_workbench", "piece_workbench.obj"),
                       only=["New/high"], ground_it=True)

    glow = skins.glow_material()
    classic = {
        "wood": skins.piece_material(
            "c_wood", skins.rip_path("woodwall", "textures", "Planks5c_low.png"),
            skins.rip_path("woodwall", "textures", "Planks5c_low_n.png"), 1.0, 0.25,
            st=(-0.56, 0.12, 0.0, 0.01)),
        "stone": skins.piece_material(
            "c_stone", skins.rip_path("stone_wall_2x1", "textures", "stone.png"),
            skins.rip_path("stone_wall_2x1", "textures", "stone_n.png"), 1.0, 0.15),
        "iron": skins.piece_material(
            "c_iron", skins.rip_path("piece_stonecutter", "textures", "StoneCutterBench_d.png"),
            skins.rip_path("piece_stonecutter", "textures", "StoneCutterBench_n.png"), 1.0, 0.107),
        "core": glow,
    }
    bm = bench_material()
    modern = {"wood": bm, "stone": bm, "iron": bm, "rope": bm, "bark": bm, "core": glow}

    # Fit BEFORE assigning: both fits find a part's group by its material slot name, and
    # assigning replaces the slot's material, so fitting afterwards matches nothing and
    # silently leaves the OBJ's own UVs spread over the whole atlas.
    for o in before:
        skins.fit_classic(o, {"wood": wood_rect, "stone": stone_rect, "iron": iron_rect}, None)
        skins.assign(o, classic)
    for o in after:
        skins.fit_metric(o, {"wood": PLANKS, "stone": PLANKS, "iron": LASHING, "rope": LASHING,
                             "bark": PLANKS})
        skins.assign(o, modern)
    for o in bench:
        skins.assign(o, {}, default=bm)

    end = row_left_to_right([before, after, bench], gap=0.55)
    lo, hi = _bounds([o for g in (before, after, bench) for o in g])
    cube_x = lo.x - 1.0
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    ref_cube((cube_x, 0.0, 0.5))
    if close:
        b_lo, b_hi = _bounds(before)
        a_lo, a_hi = _bounds(after)
        centre = (b_hi.x + a_lo.x) / 2.0
        eye_camera(centre, 3.6, min_dist=3.0, aim_z=0.9)
    else:
        centre = (hi.x + cube_x - 0.5) / 2.0
        eye_camera(centre, hi.x - cube_x + 1.0, min_dist=3.0, aim_z=0.8)
    shoot(out, width, height)
    return len(before[0].data.polygons)


if __name__ == "__main__":
    build_scene(os.path.join(RENDERS, "post_skin_before_after.png"))
    build_scene(os.path.join(RENDERS, os.environ.get("CLOSE_NAME", "post_skin_before_after_close.png")), width=1800, height=900,
                close=True)
    print("SKIN_DONE")
