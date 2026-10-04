"""
Textured materials and the two UV routes, for renders that have to show texture.

The flat-tinted renders of round 1 could not show the half of the gap that is surface, so
everything here exists to put the game's real textures on a Blender mesh the way the
runtime would, and to do it identically for vanilla and for ours:

  piece_material   Custom/Piece approximated: albedo from the rip's diffuse with
                   point filtering, the rip's own normal map (Unity packs x in alpha and y
                   in green, so it is unpacked first), roughness from _Glossiness, no
                   specular. The shader's value noise, rain and wear passes are not
                   approximated.
  fit_classic      What PostModel.Remap does today: the OBJ's own UVs clamped into the
                   donor's rect (largest triangle's bounds), then the material's own
                   _MainTex_ST applied.
  fit_metric       A port of PostModel.FitMetric: parts welded by position, one island per
                   part and facing, metre scale at 42 texels per metre, long side along the
                   grain, clamped inside the group's rect.

fit_metric is a port, not the same code: Blender's vertex numbering differs from the
runtime's, so the hash that scatters islands inside the rect gives different offsets.
Density, orientation and rects are the same.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

from rm_common import RIPS, clear_scene, load

TEXELS_PER_METRE = 42.0


def _img(path, noncolor=False):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    return img


def unpacked_normal(path):
    """Unity's packed tangent normal (x in alpha, y in green) as an ordinary RGB map."""
    key = "unpacked:" + path
    if key in bpy.data.images:
        return bpy.data.images[key]
    src = bpy.data.images.load(path, check_existing=True)
    src.colorspace_settings.name = "Non-Color"
    w, h = src.size
    a = np.array(src.pixels[:], dtype=np.float32).reshape(h, w, 4)
    x, y = a[..., 3] * 2 - 1, a[..., 1] * 2 - 1
    z = np.sqrt(np.clip(1 - x * x - y * y, 0, 1))
    out = np.ones((h, w, 4), dtype=np.float32)
    out[..., 0], out[..., 1], out[..., 2] = x * 0.5 + 0.5, y * 0.5 + 0.5, z * 0.5 + 0.5
    img = bpy.data.images.new(key, w, h, alpha=False)
    img.colorspace_settings.name = "Non-Color"
    img.pixels = out.ravel().tolist()
    return img


def piece_material(name, diffuse, normal, bump=1.0, gloss=0.0, st=None, cutout=False):
    """diffuse and normal are absolute paths. st is Unity's (scale_x, scale_y, off_x, off_y)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL":
            nt.nodes.remove(n)
    out = [n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"][0]
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 1.0 - gloss
    for key in ("Specular IOR Level", "Specular"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.0
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    uvn = nt.nodes.new("ShaderNodeUVMap")
    coord = uvn.outputs["UV"]
    if st:
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (st[0], st[1], 1.0)
        mp.inputs["Location"].default_value = (st[2], st[3], 0.0)
        nt.links.new(coord, mp.inputs["Vector"])
        coord = mp.outputs["Vector"]

    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = _img(diffuse)
    tex.interpolation = "Closest"
    tex.extension = "REPEAT"
    nt.links.new(coord, tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if cutout:
        # Custom/Vegetation is an alpha cutout (_Cutoff 0.54) drawn two-sided.
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        try:
            mat.surface_render_method = "DITHERED"
        except Exception:
            pass
        mat.use_backface_culling = False

    if not normal or not os.path.exists(normal):
        return mat
    nrm = nt.nodes.new("ShaderNodeTexImage")
    nrm.image = unpacked_normal(normal)
    nrm.interpolation = "Closest"
    nrm.extension = "REPEAT"
    nt.links.new(coord, nrm.inputs["Vector"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = bump
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def rip_path(rip, *parts):
    return os.path.join(RIPS, rip, *parts)


def glow_material():
    mat = bpy.data.materials.new("glow")
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (1.0, 0.74, 0.30, 1.0)
    b.inputs["Emission Color"].default_value = (1.0, 0.74, 0.30, 1.0)
    b.inputs["Emission Strength"].default_value = 1.6
    return mat


def assign(obj, by_name, default=None):
    """Replace material slots by slot name (our groups) or all with `default`."""
    for slot in obj.material_slots:
        key = slot.name.split(".")[0].lower()
        mat = by_name.get(key, default)
        if mat is not None:
            slot.material = mat


def donor_rect(rip, prefix="New/"):
    """PostModel.UvRegion: bounds of the largest single triangle's UVs (width and height
    in (0.005, 1])."""
    clear_scene_keep = bpy.context.scene.objects[:]
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=rip_path(rip, rip + ".obj"), forward_axis="Z", up_axis="Y")
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    chosen = [o for o in objs if o.name.startswith(prefix)] or objs
    best, best_area = (0.0, 0.0, 1.0, 1.0), 0.0
    for o in chosen:
        me = o.data
        uv = me.uv_layers.active.data
        me.calc_loop_triangles()
        for tri in me.loop_triangles:
            pts = [uv[i].uv for i in tri.loops]
            xs, ys = [p[0] for p in pts], [p[1] for p in pts]
            w, h = max(xs) - min(xs), max(ys) - min(ys)
            if w <= 0.005 or h <= 0.005 or w > 1 or h > 1:
                continue
            if w * h > best_area:
                best_area, best = w * h, (min(xs), min(ys), w, h)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return best


def fit_classic(obj, rects, st):
    """rects: group -> (x, y, w, h); st: group -> (sx, sy, ox, oy) or None. The material
    applies st itself, so only the rect clamp happens here, exactly as Remap does."""
    me = obj.data
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        group = obj.material_slots[poly.material_index].name.split(".")[0].lower()
        if group not in rects:
            continue
        x, y, w, h = rects[group]
        for li in poly.loop_indices:
            u, v = uv[li].uv
            uv[li].uv = (x + min(1.0, max(0.0, u)) * w, y + min(1.0, max(0.0, v)) * h)


def _hash01(a, b):
    h = ((a * 73856093) ^ (b * 19349663)) & 0xFFFFFFFF
    h ^= h >> 13
    h = (h * 0x5BD1E995) & 0xFFFFFFFF
    h ^= h >> 15
    return (h & 0xFFFF) / 65535.0


def fit_metric(obj, rects, tex_px=256, px=None, stretch=()):
    """Port of PostModel.FitMetric over Blender loops."""
    me = obj.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    nverts = len(me.vertices)
    parent = list(range(nverts))

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
        key = (group, find(poly.vertices[0]), ax)
        islands.setdefault(key, []).append(poly)

    def project(co, ax):
        # The runtime is Y-up; its axis numbering is x, y(up), z. Blender is x, y, z(up).
        # What matters is which two coordinates lie in the face, so map by facing only.
        if ax == 0:
            return co.y, co.z
        if ax == 1:
            return co.x, co.z
        return co.x, co.y

    for (group, root, ax), polys in islands.items():
        choices = rects[group] if isinstance(rects[group], list) else [rects[group]]
        x0, y0, rw, rh = choices[min(len(choices) - 1, int(_hash01(root, 7) * len(choices)))]
        pts = []
        for poly in polys:
            for li in poly.loop_indices:
                pts.append((li, project(me.vertices[me.loops[li].vertex_index].co, ax)))
        minA = min(p[1][0] for p in pts); maxA = max(p[1][0] for p in pts)
        minB = min(p[1][1] for p in pts); maxB = max(p[1][1] for p in pts)
        swap = (maxA - minA) > (maxB - minB)
        extS = (maxB - minB) if swap else (maxA - minA)
        extT = (maxA - minA) if swap else (maxB - minB)
        if group in stretch:
            # A cutout leaf: the sheet's silhouette must fill the part, so stretch, long side up.
            for li, (p, q) in pts:
                s2 = (q - minB) if swap else (p - minA)
                t2 = (p - minA) if swap else (q - minB)
                uv[li].uv = (x0 + s2 / max(extS, 1e-5) * rw, y0 + t2 / max(extT, 1e-5) * rh)
            continue
        scale = TEXELS_PER_METRE / px.get(group, tex_px)
        fit = min(1.0, rw / max(extS * scale, 1e-5), rh / max(extT * scale, 1e-5))
        k = scale * fit
        seed = (root * 4 + ax) & 0x7FFFFFFF
        ox = x0 + _hash01(seed, 1) * max(0.0, rw - extS * k)
        oy = y0 + _hash01(seed, 2) * max(0.0, rh - extT * k)
        for li, (p, q) in pts:
            s2 = (q - minB) if swap else (p - minA)
            t2 = (p - minA) if swap else (q - minB)
            uv[li].uv = (ox + s2 * k, oy + t2 * k)
