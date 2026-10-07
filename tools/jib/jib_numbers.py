"""
LHM-77: the wood of the jib against the wood of the workbench, in numbers.

    blender --background --python tools/jib/jib_numbers.py

For the workbench's planks (New/high triangles whose UVs sit in the plank islands), the jib's
wood and bark groups as the running game had them (BEFORE: woodwall's Planks5c_low through
its own _MainTex_ST) and as PostModel now skins them (AFTER: Workbench_mat, region per part,
metre-scale unwrap), it prints:

  texel density   sqrt(sum UV area x px^2 / sum surface area), texels per metre
  lum sd          standard deviation of the albedo's luminance, area weighted
  mean sat        mean HSV saturation of the albedo, area weighted
  mean lum        mean luminance
  dark share      share of the surface sampling albedo darker than 0.06 luminance

The albedo is sampled with nearest filtering at each triangle's UV centroid, so these are
properties of the texture the surface reads, independent of lighting.
"""

import colorsys
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import jib_check                                   # noqa: E402
import bpy                                         # noqa: E402
import numpy as np                                 # noqa: E402
from jib_check import skins, import_obj, RIPS      # noqa: E402

_tex = {}


def texture(path):
    if path not in _tex:
        img = bpy.data.images.load(path, check_existing=True)
        w, h = img.size
        a = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(a)
        _tex[path] = (a.reshape(h, w, 4), w, h)
    return _tex[path]


def srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def stats(tris, path, px):
    """tris: [(area3d, (u, v) centroid, uv_area_in_sheet_units)] already through any ST."""
    a, w, h = texture(path)
    ws, lum, sat = [], [], []
    uvarea = 0.0
    area = 0.0
    for a3, (u, v), ua in tris:
        x = int(math.floor((u % 1.0) * w)) % w
        y = int(math.floor((v % 1.0) * h)) % h
        r, g, b = [float(srgb(np.float32(c))) for c in a[y, x, :3]]
        ws.append(a3)
        lum.append(0.2126 * r + 0.7152 * g + 0.0722 * b)
        sat.append(colorsys.rgb_to_hsv(r, g, b)[1])
        uvarea += ua
        area += a3
    ws, lum, sat = np.array(ws), np.array(lum), np.array(sat)
    m = np.average(lum, weights=ws)
    sd = math.sqrt(np.average((lum - m) ** 2, weights=ws))
    return {"texels_per_m": math.sqrt(uvarea * px * px / area), "lum_sd": sd,
            "mean_sat": float(np.average(sat, weights=ws)), "mean_lum": float(m),
            "dark_share": float(ws[lum < 0.06].sum() / ws.sum())}


def collect(objs, groups, st=None, rect_filter=None):
    out = []
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        uv = me.uv_layers.active.data
        for tri in me.loop_triangles:
            if groups is not None:
                name = o.material_slots[tri.material_index].name.split(".")[0].lower()
                if name not in groups:
                    continue
            pts = [uv[i].uv for i in tri.loops]
            if st:
                pts = [(p[0] * st[0] + st[2], p[1] * st[1] + st[3]) for p in pts]
            cu = sum(p[0] for p in pts) / 3.0
            cv = sum(p[1] for p in pts) / 3.0
            if rect_filter and not any(x <= cu <= x + w and y <= cv <= y + h
                                       for x, y, w, h in rect_filter):
                continue
            ua = abs((pts[1][0] - pts[0][0]) * (pts[2][1] - pts[0][1])
                     - (pts[2][0] - pts[0][0]) * (pts[1][1] - pts[0][1])) / 2.0
            out.append((tri.area, (cu, cv), ua))
    return out


def show(label, s):
    print("NUM %-34s %5.1f texels/m  lum sd %.3f  mean sat %.3f  mean lum %.3f  dark<0.06 %4.1f%%"
          % (label, s["texels_per_m"], s["lum_sd"], s["mean_sat"], s["mean_lum"], 100 * s["dark_share"]))


def main():
    bench_png = skins.rip_path("piece_workbench", "textures", "WorkBench_d.png")
    wall_png = skins.rip_path("woodwall", "textures", "Planks5c_low.png")

    jib_check.clear_scene()
    bench = import_obj(os.path.join(RIPS, "piece_workbench", "piece_workbench.obj"),
                       only=["New/high"], ground_it=True)
    planks = jib_check.tx_run.WOOD
    show("workbench planks (vanilla)", stats(collect(bench, None, rect_filter=planks), bench_png, 256))

    objs = jib_check.load_model(False)
    show("jib wood BEFORE (woodwall, all fallback groups)",
         stats(collect(objs, {"c_wood"}, st=(-0.56, 0.12, 0.0, 0.01)), wall_png, 128))

    objs = jib_check.load_model(True)
    show("jib wood+bark AFTER (Workbench_mat)",
         stats(collect(objs, {"bench"}), bench_png, 256))
    others = [("iron (stonecutter metal)", {"d_iron"}, "piece_stonecutter", "StoneCutterBench_d.png", 256),
              ("stone (stone_wall_2x1)", {"d_clay"}, "stone_wall_2x1", "stone.png", 128),
              ("wicker+cord (village weave)", {"d_weave"}, "fi_vil_container_basket02_closed",
               "fi_village_containers_hd.png", 256),
              ("moss/frond (fern sheet)", {"d_moss", "d_frond"}, "Pickable_Fiddlehead",
               "Ashlandsvegetation_d.png", 64)]
    for label, names, rip, png, px in others:
        tris = collect(objs, names)
        if tris:
            show("jib AFTER " + label, stats(tris, skins.rip_path(rip, "textures", png), px))
    print("NUM_DONE")


main()
