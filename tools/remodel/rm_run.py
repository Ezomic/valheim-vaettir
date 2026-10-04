"""
Builds a piece's variants and shoots every one, and the shipped model, through one rig.

A tile is: the model at the scale the runtime applies (1.0 for all of these), the thing it
is seen beside, a vanilla wooden chest from a Devkit rip, and a 1m cube. Eye height 1.7m,
42mm, and the camera distance is FIXED per piece so every tile on a sheet is the same
scale: a tile that backs off further to fit a wider model quietly shrinks it and a design
looks smaller than its neighbour for no reason but its neighbour.

Upgrades are shown beside the post of the SAME letter (the shipped post for CURRENT),
because an upgrade is only ever seen touching its post.
"""

import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy

from rm_parts import *           # noqa: E402,F401,F403
from rm_parts import _bounds     # noqa: E402

CAM_SPAN = {"post": 5.8, "rail": 5.9, "perch": 5.9, "jib": 5.9}
SHIPPED_POST = "stow_post_canopy.obj"

CHEST = ("piece_chest_wood", ["New/woodchest", "New/woodchesttop_closed"])


def centre_xy(objs):
    lo, hi = _bounds(objs)
    for o in objs:
        o.location.x -= (lo.x + hi.x) / 2.0
        o.location.y -= (lo.y + hi.y) / 2.0
    bpy.context.view_layer.update()


def import_obj(path, only=None, ground_it=False):
    objs, lo, hi = load(path, only=only)
    if ground_it:
        ground(objs)
    else:
        centre_xy(objs)
    return objs


def row_left_to_right(groups, gap=0.5):
    """Frame-left is +x (the camera stands on +y), so walk the cursor toward -x."""
    cursor = 0.0
    for objs in groups:
        lo, hi = _bounds(objs)
        w = hi.x - lo.x
        move(objs, dx=(cursor - w / 2.0) - (lo.x + hi.x) / 2.0)
        cursor -= w + gap
    return cursor + gap


def tile(piece, model_objs, neighbour_path, out):
    groups = [model_objs]
    if neighbour_path:
        groups.append(import_obj(neighbour_path))
    if piece == "post":
        name, only = CHEST
        groups.append(import_obj(os.path.join(RIPS, name, name + ".obj"), only=only,
                                 ground_it=True))
    end = row_left_to_right(groups)
    lo_all, hi_all = _bounds([o for g in groups for o in g])
    cube_x = lo_all.x - 0.5 - 0.5
    tint_all()
    stage()
    ref_cube((cube_x, 0.0, 0.5))
    centre = (hi_all.x + cube_x - 0.5) / 2.0
    dist = eye_camera(centre, CAM_SPAN[piece], min_dist=3.0)
    shoot(out, 1000, 440)
    return dist, (hi_all.x - (cube_x - 0.5))


CLOSE_SPAN = {"post": 3.5, "rail": 3.8, "perch": 3.0, "jib": 4.0}


def close_tile(piece, letter, model_objs):
    """The piece alone with the 1m cube, as near as the widest candidate allows. Fixed per
    piece for the same reason as the wide tile."""
    lo, hi = _bounds(model_objs)
    cube_x = lo.x - 0.7
    tint_all()
    stage()
    ref_cube((cube_x, 0.0, 0.5))
    centre = (hi.x + cube_x - 0.5) / 2.0
    dist = eye_camera(centre, CLOSE_SPAN[piece], min_dist=3.0, aim_z=0.85)
    shoot(os.path.join(RENDERS, "%s_close_%s.png" % (piece, letter)), 640, 540)
    return dist


def rebuild(letter, current_file, fn, name):
    clear_scene()
    if letter == "current":
        return import_obj(os.path.join(ASSETS, current_file))
    fn()
    obj, _ = build_model(name)
    return [obj]


def run_piece(piece, current_file, builders, neighbour_for):
    """
    builders: [(letter, builder, one-line description)]
    neighbour_for(letter) -> path of the neighbour .obj, or None.
    """
    os.makedirs(RENDERS, exist_ok=True)
    results = {}
    keys = [("current", None, "The model shipping now.")] + list(builders)

    for letter, fn, what in keys:
        name = None if letter == "current" else "%s_remodel_%s" % (BASENAME[piece], letter)
        clear_scene()
        if letter == "current":
            model = import_obj(os.path.join(ASSETS, current_file))
            path = os.path.join(ASSETS, current_file)
            parts = None
        else:
            fn()
            obj, parts = build_model(name)
            path = export_variant(obj, name)
            model = [obj]

        tris, open_edges, islands = obj_measure(path)
        lo, hi = _bounds(model)
        dims = (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)

        nb = neighbour_for(letter)
        dist, span = tile(piece, model, nb,
                          os.path.join(RENDERS, "%s_tile_%s.png" % (piece, letter)))
        results[letter] = {"what": what, "tris": tris, "open": open_edges,
                           "parts": islands, "dim": dims, "span": span, "dist": dist,
                           "file": os.path.basename(path)}
        print("REMODEL %-6s %-8s tris=%-5d open_edges=%-3d parts=%-3d  %.2f x %.2f x %.2fm"
              "  tile span %.2fm (cap %.1f) camera %.2fm"
              % (piece, letter, tris, open_edges, islands, dims[0], dims[1], dims[2],
                 span, CAM_SPAN[piece], dist))
        if span > CAM_SPAN[piece] + 0.01:
            print("REMODEL WARNING tile wider than the fixed camera span")

        model = rebuild(letter, current_file, fn, name)
        results[letter]["close_dist"] = close_tile(piece, letter, model)

        if letter != "current":
            model = rebuild(letter, current_file, fn, name)
            tint_all()
            icon(model, os.path.join(VARIANTS, name + "_icon.png"))

    with open(os.path.join(RENDERS, "results_%s.json" % piece), "w") as fh:
        json.dump(results, fh, indent=1)
    print("REMODEL_DONE", piece)


BASENAME = {"post": "stow_post", "rail": "stow_rail", "perch": "stow_perch", "jib": "hod_jib"}


def variant_path(piece, letter):
    return os.path.join(VARIANTS, "%s_remodel_%s.obj" % (BASENAME[piece], letter))


def post_neighbour(letter):
    if letter == "current":
        return os.path.join(ASSETS, SHIPPED_POST)
    return variant_path("post", letter)
