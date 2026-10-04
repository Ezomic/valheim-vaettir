"""
Textured tiles: every candidate and the shipping model wearing the game's own textures, in
one frame with the vanilla chest and workbench, one light rig, one material model.

A candidate wears what the runtime would put on it under PostSkin=workbench: the
workbench's Workbench_mat, parts mapped by position at 42 texels per metre inside the plank
rect, straps and rope inside the darker plank rect, the heartwood glowing. The shipping
model wears what it wears today (the classic donors). Vanilla wears its own material built
from its own rip, through the same piece_material function.

Fits happen before materials are assigned: both look a part's group up by its material slot
name, and assigning replaces the name (the bug that made the first after render look wrong).
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from rm_run import *             # noqa: E402,F401,F403
from rm_run import _bounds       # noqa: E402,F401
import skins                     # noqa: E402

PLANKS = (0.06, 0.15, 0.84, 0.31)
DARK = (0.09, 0.60, 0.17, 0.30)
MODERN_RECTS = {"wood": PLANKS, "bark": PLANKS, "stone": PLANKS, "iron": DARK, "rope": DARK}

TX_SPAN = {"post": 9.2, "rail": 9.6, "perch": 8.6, "jib": 9.6}
TILE = (2400, 700)

_cache = {}
_rects = {}


def _bench_mat():
    if "bench" not in _cache:
        _cache["bench"] = skins.piece_material(
            "bench", skins.rip_path("piece_workbench", "textures", "WorkBench_d.png"),
            skins.rip_path("piece_workbench", "textures", "WorkBench_n.png"), 1.0, 0.0)
    return _cache["bench"]


def _classic_mats():
    if "classic" not in _cache:
        wood = skins.piece_material(
            "c_wood", skins.rip_path("woodwall", "textures", "Planks5c_low.png"),
            skins.rip_path("woodwall", "textures", "Planks5c_low_n.png"), 1.0, 0.25,
            st=(-0.56, 0.12, 0.0, 0.01))
        _cache["classic"] = {
            "wood": wood, "bark": wood, "rope": wood,
            "stone": skins.piece_material(
                "c_stone", skins.rip_path("stone_wall_2x1", "textures", "stone.png"),
                skins.rip_path("stone_wall_2x1", "textures", "stone_n.png"), 1.0, 0.15),
            "iron": skins.piece_material(
                "c_iron", skins.rip_path("piece_stonecutter", "textures", "StoneCutterBench_d.png"),
                skins.rip_path("piece_stonecutter", "textures", "StoneCutterBench_n.png"), 1.0, 0.107),
            "core": skins.glow_material()}
    return _cache["classic"]


def _classic_rects():
    if "crects" not in _rects:
        _rects["crects"] = {"wood": skins.donor_rect("woodwall"),
                            "stone": skins.donor_rect("stone_wall_2x1"),
                            "iron": skins.donor_rect("piece_stonecutter")}
        _rects["crects"]["bark"] = _rects["crects"]["wood"]
        _rects["crects"]["rope"] = _rects["crects"]["wood"]
    return _rects["crects"]


def thin_share(objs):
    """Share of triangles in loose parts whose thinnest side is under 4 cm: the straps,
    collars and handles the vanilla rips carry 24 to 63% of their triangles in."""
    import bmesh
    import numpy as np
    thin = total = 0
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.verts.ensure_lookup_table()
        seen = set()
        for v in bm.verts:
            if v.index in seen:
                continue
            stack, group = [v], []
            seen.add(v.index)
            while stack:
                cur = stack.pop()
                group.append(cur)
                for e in cur.link_edges:
                    w = e.other_vert(cur)
                    if w.index not in seen:
                        seen.add(w.index)
                        stack.append(w)
            faces = {f for g in group for f in g.link_faces}
            tris = sum(len(f.verts) - 2 for f in faces)
            pts = np.array([tuple(g.co) for g in group])
            if len(pts) < 4:
                continue
            c = pts - pts.mean(axis=0)
            _, _, vt = np.linalg.svd(c, full_matrices=False)
            proj = c @ vt.T
            dims = sorted(proj.max(axis=0) - proj.min(axis=0))
            total += tris
            if dims[0] < 0.04:
                thin += tris
        bm.free()
    return thin / float(max(total, 1))


def dress_modern(objs):
    glow = skins.glow_material()
    bm = _bench_mat()
    for o in objs:
        skins.fit_metric(o, MODERN_RECTS)
        skins.assign(o, {"wood": bm, "bark": bm, "stone": bm, "iron": bm, "rope": bm,
                         "core": glow})


def dress_classic(objs):
    mats = _classic_mats()
    rects = _classic_rects()
    for o in objs:
        skins.fit_classic(o, rects, None)
        skins.assign(o, mats)


def vanilla_chest():
    objs = import_obj(os.path.join(RIPS, "piece_chest_wood", "piece_chest_wood.obj"),
                      only=CHEST[1], ground_it=True)
    m = skins.piece_material(
        "chest", skins.rip_path("piece_chest_wood", "textures", "woodchest_d.png"),
        skins.rip_path("piece_chest_wood", "textures", "woodchest_n.png"), 3.41, 0.38)
    for o in objs:
        skins.assign(o, {}, default=m)
    return objs


def vanilla_bench():
    objs = import_obj(os.path.join(RIPS, "piece_workbench", "piece_workbench.obj"),
                      only=["New/high"], ground_it=True)
    for o in objs:
        skins.assign(o, {}, default=_bench_mat())
    return objs


def tx_tile(piece, model_objs, neighbour, out, classic_model, chest=True):
    """neighbour: (path, classic?) or None. model_objs are already fitted and dressed."""
    groups = [model_objs]
    if neighbour:
        nobjs = import_obj(neighbour[0])
        (dress_classic if neighbour[1] else dress_modern)(nobjs)
        groups.append(nobjs)
    if chest:
        groups.append(vanilla_chest())
    groups.append(vanilla_bench())
    row_left_to_right(groups, gap=0.5)
    lo_all, hi_all = _bounds([o for g in groups for o in g])
    cube_x = lo_all.x - 1.0
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    ref_cube((cube_x, 0.0, 0.5))
    centre = (hi_all.x + cube_x - 0.5) / 2.0
    dist = eye_camera(centre, TX_SPAN[piece], min_dist=3.0, aim_z=0.85)
    shoot(out, TILE[0], TILE[1])
    return dist, hi_all.x - (cube_x - 0.5)


def run_textured(piece, current_file, builders, neighbour_for, tag, name_for, chest):
    """builders: [(letter, fn, description)]. neighbour_for(letter) -> (path, classic) or None."""
    os.makedirs(RENDERS, exist_ok=True)
    results = {}
    keys = [("current", None, "The model shipping now, as the runtime skins it today.")] \
        + list(builders)
    for letter, fn, what in keys:
        clear_scene()
        _cache.clear()
        if letter == "current":
            model = import_obj(os.path.join(ASSETS, current_file))
            path = os.path.join(ASSETS, current_file)
            dress_classic(model)
        else:
            name = name_for(letter)
            fn()
            obj, _ = build_model(name)
            path = export_variant(obj, name)
            model = [obj]
            lo, hi = _bounds(model)
            dress_modern(model)
        thin = thin_share(model)
        tris, open_edges, islands = obj_measure(path)
        lo, hi = _bounds(model)
        dims = (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
        out = os.path.join(RENDERS, "%s_%s_tile_%s.png" % (tag, piece, letter))
        dist, span = tx_tile(piece, model, neighbour_for(letter), out, letter == "current",
                             chest=chest)
        results[letter] = {"what": what, "tris": tris, "open": open_edges, "parts": islands,
                           "dim": dims, "span": span, "dist": dist, "thin": thin,
                           "file": os.path.basename(path)}
        print("TX %-6s %-8s tris=%-5d open_edges=%-3d parts=%-3d thin<4cm=%2d%% %.2f x %.2f x %.2fm "
              "camera %.2fm" % (piece, letter, tris, open_edges, islands, round(thin * 100),
                                dims[0], dims[1], dims[2], dist))
        if span > TX_SPAN[piece] + 0.01:
            print("TX WARNING tile wider than the fixed camera span")
    with open(os.path.join(RENDERS, "%s_results_%s.json" % (tag, piece)), "w") as fh:
        json.dump(results, fh, indent=1)
    print("TX_DONE", piece)
