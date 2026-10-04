"""
LHM-69 phase 1a: the shipped stowing post and its three upgrades, at eye height, beside
vanilla.

    blender --background --python tools/remodel/baseline.py

Not a design. This is the picture of the problem: the files that ship, imported exactly as
the game reads them (forward Z, up Y), standing next to what the game itself builds in the
same role, so the gap can be named in measurements instead of "looks off".

Vanilla comes from Devkit rips already on disk (own-profile/BepInEx/rips). The post is
cloned from piece_chest_wood, so that chest is the honest reference: same donor material,
same hammer menu, same person placing both.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rm_common import *          # noqa: E402,F401,F403
from rm_common import _bounds    # noqa: E402

CURRENT = {
    "post": "stow_post_canopy.obj",
    "rail": "stow_rail.obj",
    "perch": "stow_perch.obj",
    "jib": "hod_jib.obj",
}


def vanilla(name, only=None):
    path = os.path.join(RIPS, name, name + ".obj")
    objs, lo, hi = load(path, only=only)
    ground(objs)
    return objs


def mine(file):
    objs, lo, hi = load(os.path.join(ASSETS, file))
    ground(objs)
    return objs


def lay_row(rows, gap=0.45):
    """rows: list of object lists, LEFT to RIGHT as the camera sees it (+x is frame-left)."""
    cursor = 0.0
    out = []
    for objs in rows:
        lo, hi = _bounds(objs)
        w = hi.x - lo.x
        cx = cursor - w / 2.0
        move(objs, dx=cx - (lo.x + hi.x) / 2.0)
        out.append((objs, cx))
        cursor -= w + gap
    return out


def shot(name, rows, cube=True, min_dist=3.0, width=1100, height=480):
    clear_scene()
    objs = [r() for r in rows]
    placed = lay_row(objs)
    lo, hi = _bounds([o for g in objs for o in g])
    cube_x = lo.x - 0.45 - 0.5
    out = os.path.join(RENDERS, name + ".png")
    tint_all()
    stage()
    if cube:
        ref_cube((lo.x - 0.45 - 0.5, 0.0, 0.5))
    xs = [lo.x, hi.x] + ([cube_x - 0.5, cube_x + 0.5] if cube else [])
    dist = eye_camera((min(xs) + max(xs)) / 2.0, max(xs) - min(xs), min_dist=min_dist)
    shoot(out, width, height)
    print("SHOT %-26s camera %.2fm back, 1.7m up, 42mm" % (name, dist))


def main():
    os.makedirs(RENDERS, exist_ok=True)

    chest = lambda: vanilla("piece_chest_wood", only=["New/woodchest", "New/woodchesttop_closed"])
    bench = lambda: vanilla("piece_workbench", only=["New/high"])
    shelf = lambda: vanilla("dvergrprops_shelf", only=["dvergr_shelf01"])

    # Measurements first, because the renders can only suggest and these decide.
    print("---- STATS")
    for label, getter in (("vanilla chest_wood", chest), ("vanilla workbench", bench),
                          ("vanilla dverg shelf", shelf)):
        clear_scene()
        print(stat_line(label, stats(getter())))
    for key, file in CURRENT.items():
        clear_scene()
        print(stat_line("CURRENT " + key, stats(mine(file))))

    print("---- SHOTS")
    for key, file in CURRENT.items():
        piece = lambda f=file: mine(f)
        post = lambda: mine(CURRENT["post"])
        if key == "post":
            shot("baseline_post_wide", [piece, chest], min_dist=3.0)
            shot("baseline_post_workbench", [piece, bench], min_dist=3.0)
        else:
            shot("baseline_%s_wide" % key, [piece, post, chest], min_dist=3.0)
    print("BASELINE_DONE")


main()
