"""
LHM-69 round 2: the vanilla look, in numbers.

    blender --background --python tools/remodel/measure_rips.py

Reads the Devkit rips in own-profile/BepInEx/rips and prints, per reference mesh:
triangle count; loose parts and their oriented sizes (thinnest side is the plank
thickness); how much of the mesh is thin stuff (straps, nails, hinges) versus timber;
short-edge length (the bevel); texel density (texels per metre, from UV area against world
area); the UV rectangle and how much of the atlas the mesh covers; the palette under the
UVs the mesh actually samples; normal-map strength. Writes renders/lhm-69/vanilla_numbers.json
so STYLE_GAP.txt quotes measurements and not impressions.
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bmesh
import bpy
import numpy as np

from rm_common import RENDERS, RIPS, clear_scene, load


def read_image(path):
    """Raw 0..1 values, bottom row first (the same orientation UVs use)."""
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    arr = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]
    return arr, w, h


def read_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    return np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)


def raster(mask, tri):
    """Fill one UV triangle (pixel coords) into a boolean mask."""
    h, w = mask.shape
    x0, x1 = int(max(0, np.floor(min(p[0] for p in tri)))), int(min(w - 1, np.ceil(max(p[0] for p in tri))))
    y0, y1 = int(max(0, np.floor(min(p[1] for p in tri)))), int(min(h - 1, np.ceil(max(p[1] for p in tri))))
    if x1 < x0 or y1 < y0:
        return
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = tri
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-12:
        return
    l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
    l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
    inside = (l1 >= -0.5 / w) & (l2 >= -0.5 / w) & (1 - l1 - l2 >= -0.5 / w)
    mask[y0:y1 + 1, x0:x1 + 1] |= inside


SUBJECTS = [
    ("chest_wood", "piece_chest_wood", ["New/woodchest", "New/woodchesttop_closed"],
     "woodchest_d.png", "woodchest_n.png"),
    ("workbench", "piece_workbench", ["New/high"], "WorkBench_d.png", "WorkBench_n.png"),
    ("wood_beam", "wood_beam", ["new"],
     "Planks5c_low.png", "Planks5c_low_n.png"),
    ("wood_wall_log", "wood_wall_log", None, "Pine_tree_log_wall.png",
     "Pine_tree_log_wall_n.png"),
    ("wood_stack", "wood_stack", ["New/log (1)"], "woodpile_diffuse.png",
     "woodpile_generaten.png"),
    ("barrell", "barrell", None, "barrellDiffuse.png", "barrell_n.png"),
    ("artisan_station", "piece_artisanstation", ["New/high/ArtisanTable.004"],
     "Artisan_Table_d.png", "Artisan_Table_n.png"),
    ("dvergr_shelf", "dvergrprops_shelf", ["new/shelf_high"], "Planks5c_low.png",
     "Planks5_Normal.png"),
]


def components(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    seen, comps = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, group = [v], []
        seen.add(v.index)
        while stack:
            cur = stack.pop()
            group.append(cur.index)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        comps.append(group)
    tris_of = {}
    for f in bm.faces:
        tris_of.setdefault(f.verts[0].index, 0)
        tris_of[f.verts[0].index] += len(f.verts) - 2
    coords = np.array([tuple(v.co) for v in bm.verts])
    out = []
    for group in comps:
        pts = coords[group]
        c = pts - pts.mean(axis=0)
        if len(pts) < 4:
            continue
        _, _, vt = np.linalg.svd(c, full_matrices=False)
        proj = c @ vt.T
        dims = sorted(proj.max(axis=0) - proj.min(axis=0))
        out.append({"dims": [float(d) for d in dims],
                    "tris": sum(tris_of.get(i, 0) for i in group)})
    bm.free()
    return out


def texel_density(objs, tex_w, tex_h):
    dens, tri_n = [], 0
    for o in objs:
        me = o.data
        uv = me.uv_layers.active.data if me.uv_layers.active else None
        if uv is None:
            continue
        for poly in me.polygons:
            if len(poly.vertices) != 3:
                continue
            idx = poly.loop_indices
            w = [np.array(me.vertices[i].co) for i in poly.vertices]
            wa = np.linalg.norm(np.cross(w[1] - w[0], w[2] - w[0])) / 2.0
            u = [np.array(uv[i].uv) for i in idx]
            ua = abs((u[1][0] - u[0][0]) * (u[2][1] - u[0][1]) -
                     (u[2][0] - u[0][0]) * (u[1][1] - u[0][1])) / 2.0
            tri_n += 1
            if wa > 1e-5 and ua > 1e-9:
                dens.append(np.sqrt(ua * tex_w * tex_h / wa))
    return np.array(dens), tri_n


def uv_stats(objs, tex_w, tex_h):
    mask = np.zeros((tex_h, tex_w), bool)
    lo = np.array([9e9, 9e9])
    hi = np.array([-9e9, -9e9])
    for o in objs:
        me = o.data
        if not me.uv_layers.active:
            continue
        uv = me.uv_layers.active.data
        for poly in me.polygons:
            pts = [(uv[i].uv[0], uv[i].uv[1]) for i in poly.loop_indices]
            for p in pts:
                lo = np.minimum(lo, p)
                hi = np.maximum(hi, p)
            px = [(p[0] * tex_w, p[1] * tex_h) for p in pts]
            for k in range(1, len(px) - 1):
                raster(mask, (px[0], px[k], px[k + 1]))
    return mask, lo, hi


def palette(arr, mask):
    out = {}
    for name, sel in (("whole_texture", np.ones(mask.shape, bool)), ("under_mesh_uvs", mask)):
        px = arr[sel]
        px = px[px.sum(axis=1) > 0.06]
        if len(px) == 0:
            continue
        lum = px @ np.array([0.2126, 0.7152, 0.0722])
        mx, mn = px.max(axis=1), px.min(axis=1)
        sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
        out[name] = {"mean_rgb": [round(float(x), 3) for x in px.mean(axis=0)],
                     "lum_p10_p50_p90": [round(float(np.percentile(lum, q)), 3)
                                         for q in (10, 50, 90)],
                     "mean_saturation": round(float(sat.mean()), 3),
                     "black_fraction_of_texture": round(float((arr.sum(axis=2) <= 0.06).mean()), 3)}
    return out


def normal_strength(path):
    # Unity packs tangent normals as x in alpha, y in green (DXTnm), so red is not x.
    a = read_rgba(path)
    x, y = a[..., 3] * 2 - 1, a[..., 1] * 2 - 1
    dev = np.sqrt(x ** 2 + y ** 2)
    return {"mean_tilt": round(float(dev.mean()), 3),
            "p95_tilt": round(float(np.percentile(dev, 95)), 3),
            "pixels_over_0.2": round(float((dev > 0.2).mean()), 3)}


def measure(key, rip, only, diffuse, normal):
    clear_scene()
    objs, _, _ = load(os.path.join(RIPS, rip, rip + ".obj"), only=only)
    if only is None:
        objs = objs[:1]
    arr, W, H = read_image(os.path.join(RIPS, rip, "textures", diffuse))
    tris = 0
    parts = []
    short_edges = []
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        tris += len(me.loop_triangles)
        parts += components(me)
        for e in me.edges:
            a, b = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
            short_edges.append((a - b).length)
    dens, _ = texel_density(objs, W, H)
    mask, lo, hi = uv_stats(objs, W, H)
    covered = float(mask.mean())

    thick = np.array([p["dims"][0] for p in parts]) if parts else np.array([0.0])
    weights = np.array([p["tris"] for p in parts]) if parts else np.array([1])
    thin_tris = int(sum(p["tris"] for p in parts if p["dims"][0] < 0.04))
    se = np.array(short_edges)
    big = [p for p in parts if p["tris"] >= 20]
    res = {
        "tris": tris,
        "loose_parts": len(parts),
        "parts_over_20_tris": len(big),
        "biggest_parts_dims_m_tris": [[round(d, 2) for d in p["dims"]] + [p["tris"]]
                                     for p in sorted(parts, key=lambda p: -p["tris"])[:10]],
        "thickness_of_big_parts_m": sorted(round(p["dims"][0], 3) for p in big)[:24],
        "median_thickness_big_parts_m": round(float(np.median([p["dims"][0] for p in big])), 3)
        if big else None,
        "thin_part_tri_share_under_4cm": round(thin_tris / max(tris, 1), 3),
        "edge_len_p10_p25_p50_m": [round(float(np.percentile(se, q)), 4) for q in (10, 25, 50)],
        "edges_under_3cm_share": round(float((se < 0.03).mean()), 3),
        "texel_density_p10_p50_p90_per_m": [round(float(np.percentile(dens, q)), 1)
                                            for q in (10, 50, 90)] if len(dens) else None,
        "texture_px": [W, H],
        "uv_rect": [round(float(x), 3) for x in (lo[0], lo[1], hi[0], hi[1])],
        "atlas_fraction_covered": round(covered, 3),
        "palette": palette(arr, mask),
        "normal_map": normal_strength(os.path.join(RIPS, rip, "textures", normal)),
    }
    return res


def main():
    out = {}
    for key, rip, only, d, n in SUBJECTS:
        try:
            out[key] = measure(key, rip, only, d, n)
        except Exception as exc:       # a missing rip must not hide the others
            out[key] = {"error": repr(exc)}
        print("MEASURED", key, json.dumps(out[key]))
    os.makedirs(RENDERS, exist_ok=True)
    with open(os.path.join(RENDERS, "vanilla_numbers.json"), "w") as fh:
        json.dump(out, fh, indent=1)
    print("MEASURE_DONE")


main()
