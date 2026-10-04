"""
Which part of the vanilla workbench wears which island of its one atlas.

    blender --background --python tools/remodel/bench_parts.py

The bench has ONE material (Workbench_mat, WorkBench_d 256px). Its hide, stones, straps and
posts are not different materials: they are different painted islands of that sheet, and each
part of the mesh is UV-mapped into the island that suits it. This prints, per island, how many
loose parts wear it, how many triangles, and the sizes of the biggest, so the post can be
dressed in the same kinds of things. Writes renders/lhm-69/bench_islands.json.
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh
import numpy as np
from rm_common import RENDERS, RIPS, clear_scene, load

ISLANDS = {
    "plank_field":   (0.008, 0.012, 0.953, 0.551),
    "grained_plank": (0.301, 0.559, 0.754, 0.980),
    "dark_plank":    (0.043, 0.562, 0.289, 0.961),
    "strap_leather": (0.801, 0.555, 0.961, 0.777),
    "hide":          (0.625, 0.570, 0.789, 0.750),
    "wood_small":    (0.812, 0.805, 0.941, 0.984),
    "stone":         (0.324, 0.809, 0.422, 0.902),
}

def which(u, v):
    best, bd = None, 9
    # Smallest island first: the grained plank's box encloses the stone and the hide.
    for k, (x0, y0, x1, y1) in sorted(ISLANDS.items(), key=lambda kv: (kv[1][2] - kv[1][0]) * (kv[1][3] - kv[1][1])):
        du = max(x0 - u, 0, u - x1); dv = max(y0 - v, 0, v - y1)
        d = du + dv
        if d < bd - 1e-9:
            best, bd = k, d
    return best

clear_scene()
objs, _, _ = load(os.path.join(RIPS, "piece_workbench", "piece_workbench.obj"), only=["New/high"])
o = objs[0]
bm = bmesh.new(); bm.from_mesh(o.data)
uvl = bm.loops.layers.uv.active
bm.verts.ensure_lookup_table()
seen = set(); comps = []
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; seen.add(v.index); grp = []
    while st:
        c = st.pop(); grp.append(c)
        for e in c.link_edges:
            w = e.other_vert(c)
            if w.index not in seen: seen.add(w.index); st.append(w)
    comps.append(grp)
out = {}
for grp in comps:
    ids = {v.index for v in grp}
    faces = {f for v in grp for f in v.link_faces}
    if not faces: continue
    us = [l[uvl].uv for f in faces for l in f.loops]
    cu = sum(p[0] for p in us) / len(us); cv = sum(p[1] for p in us) / len(us)
    isl = which(cu, cv)
    pts = np.array([tuple(v.co) for v in grp])
    c = pts - pts.mean(axis=0)
    dims = sorted(float(d) for d in (c @ np.linalg.svd(c, full_matrices=False)[2].T).ptp(axis=0)) if len(pts) > 3 else [0, 0, 0]
    tris = sum(len(f.verts) - 2 for f in faces)
    if False:
        print("PART", isl, tris, "uv", [round(min(p[0] for p in us),3), round(min(p[1] for p in us),3), round(max(p[0] for p in us),3), round(max(p[1] for p in us),3)], "dims", [round(d,2) for d in dims], "centre", [round(float(x),2) for x in pts.mean(axis=0)])
    e = out.setdefault(isl, {"parts": 0, "tris": 0, "biggest_dims_m": []})
    e["parts"] += 1; e["tris"] += tris; e["biggest_dims_m"].append([round(d, 2) for d in dims] + [tris])
total = sum(e["tris"] for e in out.values())
for k, e in out.items():
    e["biggest_dims_m"] = sorted(e["biggest_dims_m"], key=lambda t: -t[3])[:4]
    e["tri_share"] = round(e["tris"] / total, 3)
    print("BENCH", k, json.dumps(e))
json.dump(out, open(os.path.join(RENDERS, "bench_islands.json"), "w"), indent=1)
