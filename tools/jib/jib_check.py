"""
LHM-77: the roost-cage jib wearing the game's real textures, before and after the port of
the workbench skins into PostModel.

    blender --background --python tools/jib/jib_check.py -- [outdir]

BEFORE is what the test game showed: only wood, iron and stone had donors, so bark, wicker,
cord, moss and frond fell back to the plain wooden-wall material. AFTER is the group to donor
table PostModel now applies (see its DonorSkin fields), fitted by the Blender twin of
PostModel.FitMetric. Each is staged beside the vanilla chest and workbench, rendered from
their own Devkit rips, at eye height, with a 1 m cube.

The staging, the materials and the fit are the LHM-69 remodel pipeline, read from the
vaettir checkout (set REMODEL_DIR to point somewhere else); only the model comes from this
tree. Blender stands in for Custom/Piece and for the fern's Custom/Vegetation shader, so the
look is an approximation of the game's, and the fit is a port: the hash that scatters islands
inside a rect differs from the runtime's, density and rects do not.
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
WT = os.path.dirname(os.path.dirname(HERE))
REMODEL = os.environ.get("REMODEL_DIR", r"E:\Repositories\valheim\vaettir\tools\remodel")
sys.path.insert(0, REMODEL)

import tx_run                        # noqa: E402
from tx_run import *                 # noqa: E402,F401,F403
from tx_run import _cache, _bounds, _rects, _classic_mats, _classic_rects   # noqa: E402

MODEL = os.path.join(WT, "assets", "hod_jib.obj")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = args[0] if args else os.path.join(WT, "renders", "lhm-77c")
WIDE = (1500, 1000)
CLOSE = (900, 900)

# Moss: a patch of the fiddlehead sheet, as PostModel.MossSkin.
tx_run.MODERN_RECTS["moss"] = [(0.12, 0.07, 0.16, 0.08)]
tx_run.MODERN_PX["moss"] = 64
_orig_donors = tx_run._donor_mats


def _donors_with_moss():
    d = _orig_donors()
    if "moss" not in d:
        r = skins.rip_path
        d["moss"] = skins.piece_material(
            "d_moss", r("Pickable_Fiddlehead", "textures", "Ashlandsvegetation_d.png"),
            r("Pickable_Fiddlehead", "textures", "Ashlandsvegetation_n.png"), 1.0, 0.03)
    return d


tx_run._donor_mats = _donors_with_moss

def _pick_rect(choices, h, need_s, need_t):
    """PostModel.PickRect: a rect the part fits at full density, else the best fit."""
    fits = [c for c in choices if c[2] >= need_s and c[3] >= need_t]
    if fits:
        return fits[min(len(fits) - 1, int(h * len(fits)))]
    return max(choices, key=lambda c: min(c[2] / max(need_s, 1e-5), c[3] / max(need_t, 1e-5)))


def fit_metric_picked(obj, rects, tex_px=256, px=None, stretch=()):
    """skins.fit_metric with PostModel.PickRect's choice of island. Same welding, same
    projection, same clamp; only the rect is no longer chosen blind."""
    import skins as S
    me = obj.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    parent = list(range(len(me.vertices)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    by_point = {}
    for v in me.vertices:
        key = tuple(int(round(c * 1000)) for c in v.co)
        if key in by_point:
            parent[find(v.index)] = find(by_point[key])
        else:
            by_point[key] = v.index
    for poly in me.polygons:
        vs = poly.vertices
        for i in range(1, len(vs)):
            parent[find(vs[i])] = find(vs[0])

    px = px or {}
    islands = {}
    for poly in me.polygons:
        group = obj.material_slots[poly.material_index].name.split(".")[0].lower()
        if group not in rects:
            continue
        n = poly.normal
        ax = 0 if abs(n.x) >= abs(n.y) and abs(n.x) >= abs(n.z) else (1 if abs(n.y) >= abs(n.z) else 2)
        islands.setdefault((group, find(poly.vertices[0]), ax), []).append(poly)

    def project(co, ax):
        return (co.y, co.z) if ax == 0 else ((co.x, co.z) if ax == 1 else (co.x, co.y))

    for (group, root, ax), polys in islands.items():
        choices = rects[group] if isinstance(rects[group], list) else [rects[group]]
        pts = [(li, project(me.vertices[me.loops[li].vertex_index].co, ax))
               for poly in polys for li in poly.loop_indices]
        minA = min(p[1][0] for p in pts); maxA = max(p[1][0] for p in pts)
        minB = min(p[1][1] for p in pts); maxB = max(p[1][1] for p in pts)
        swap = (maxA - minA) > (maxB - minB)
        extS = (maxB - minB) if swap else (maxA - minA)
        extT = (maxA - minA) if swap else (maxB - minB)
        scale = S.TEXELS_PER_METRE / px.get(group, tex_px)
        x0, y0, rw, rh = _pick_rect(choices, S._hash01(root, 7), extS * scale, extT * scale)
        if group in stretch:
            for li, (p, q) in pts:
                s2 = (q - minB) if swap else (p - minA)
                t2 = (p - minA) if swap else (q - minB)
                uv[li].uv = (x0 + s2 / max(extS, 1e-5) * rw, y0 + t2 / max(extT, 1e-5) * rh)
            continue
        fit = min(1.0, rw / max(extS * scale, 1e-5), rh / max(extT * scale, 1e-5))
        k = scale * fit
        seed = (root * 4 + ax) & 0x7FFFFFFF
        ox = x0 + S._hash01(seed, 1) * max(0.0, rw - extS * k)
        oy = y0 + S._hash01(seed, 2) * max(0.0, rh - extT * k)
        for li, (p, q) in pts:
            s2 = (q - minB) if swap else (p - minA)
            t2 = (p - minA) if swap else (q - minB)
            uv[li].uv = (ox + s2 * k, oy + t2 * k)


skins.fit_metric = fit_metric_picked


FALLBACK_TO_WOOD = ("wicker", "cord", "moss", "frond")


def dress_before(objs):
    """The game before the port: the classic donors, and every group without one is wood."""
    mats = dict(_classic_mats())
    rects = _classic_rects()
    for g in FALLBACK_TO_WOOD:
        mats[g] = mats["wood"]
        rects[g] = rects["wood"]
    for o in objs:
        skins.fit_classic(o, rects, None)
        skins.assign(o, mats)


def load_model(after):
    clear_scene()
    _cache.clear()
    _rects.clear()
    objs = import_obj(MODEL)
    (dress_modern if after else dress_before)(objs)
    return objs


def look(loc, target, lens):
    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    cam.data.lens = lens
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 300
    aim = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(aim)
    aim.location = target
    tr = cam.constraints.new(type="TRACK_TO")
    tr.target = aim
    tr.track_axis = "TRACK_NEGATIVE_Z"
    tr.up_axis = "UP_Y"
    bpy.context.scene.camera = cam


def glow_up():
    for m in bpy.data.materials:
        if m.name.startswith("glow"):
            m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.6


def wide(after, name):
    model = load_model(after)
    groups = [model, vanilla_chest(), vanilla_bench()]
    row_left_to_right(groups, gap=0.55)
    lo, hi = _bounds([o for g in groups for o in g])
    cube_x = lo.x - 1.0
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    ref_cube((cube_x, 0.0, 0.5))
    span = hi.x - (cube_x - 0.5)
    eye_camera((hi.x + cube_x - 0.5) / 2.0, span, min_dist=3.0, aim_z=1.55)
    shoot(os.path.join(OUT, name + ".png"), *WIDE)


def close(after, name, kind):
    model = load_model(after)
    lo, hi = _bounds(model)
    cx = (lo.x + hi.x) / 2.0
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    if kind == "below":
        look((cx + 0.3, 3.6, 1.7), (cx, 0.0, 2.8), 45.0)
    elif kind == "arm":
        look((cx - 0.4, 3.4, 1.5), (cx - 0.6, 0.0, 1.3), 42.0)
    shoot(os.path.join(OUT, name + ".png"), *CLOSE)


def game_view(after, name):
    """The in-game screenshot's framing: the workbench left, the jib right, about 3 m off the
    bench's front-left and a little above."""
    model = load_model(after)
    groups = [vanilla_bench(), model]
    row_left_to_right(groups, gap=1.4)
    lo, hi = _bounds([o for g in groups for o in g])
    stage(key_energy=1.6, fill_energy=0.8, sky=1.0)
    cx = (lo.x + hi.x) / 2.0
    look((cx + 0.6, 5.6, 2.6), (cx, 0.0, 1.5), 38.0)
    shoot(os.path.join(OUT, name + ".png"), 1160, 868)


def main():
    os.makedirs(OUT, exist_ok=True)
    for after, tag in ((False, "before"), (True, "after")):
        wide(after, "wide_" + tag)
        close(after, "nest_" + tag, "below")
        close(after, "arm_" + tag, "arm")
        game_view(after, "game_" + tag)
    print("JIB_CHECK_DONE", OUT)


if __name__ == "__main__":
    main()
