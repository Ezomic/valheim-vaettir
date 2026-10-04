"""
Shared staging for the LHM-69 remodel: import, tint, stage, shoot, measure.

Everything here follows the rig Kynda settled on after three rounds of picking from
renders that lied (kynda/tools/upgrade_remodel.py): key 1.4, fill 0.35, sky 0.28 so a 0.30
albedo stays timber instead of turning to sand, pieces rendered at the scale the runtime
applies with the thing they stand beside in frame, a 1m cube for scale, eye height 1.7m at
42mm, and film_transparent off for every staged shot.

Builders live in the design scripts. This module only knows how to look at a model, so a
rejected design cannot change how the survivors are judged.
"""

import math
import os
import sys

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = os.path.dirname(TOOLS)
sys.path.insert(0, TOOLS)

import bpy
import bmesh

import random

from mathutils import Euler, Matrix, Quaternion, Vector

from vhbuild import (TINTS, bevel_all, box, clear_scene, collide, export, finish, orb,
                     shell, taper, wobble, write_col)
import vhbuild

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
RENDERS = os.path.join(ROOT, "renders", "lhm-69")
RIPS = os.path.join(os.path.dirname(ROOT), "own-profile", "BepInEx", "rips")

KEY, FILL, SKY = 1.4, 0.35, 0.28

# Group tints the preview needs beyond vhbuild's own table. Previews only: at runtime each
# group wears a material borrowed off a vanilla prefab, so these say something about form
# and value and nothing about texture.
TINTS.setdefault("plank", (0.36, 0.23, 0.12, 1.0))
TINTS.setdefault("frame", (0.40, 0.27, 0.15, 1.0))
TINTS["bronze"] = (0.45, 0.30, 0.14, 1.0)
TINTS.setdefault("hide", (0.52, 0.40, 0.28, 1.0))
TINTS.setdefault("stone", (0.30, 0.30, 0.31, 1.0))

# --------------------------------------------------------------------------- import

def _bounds(objs):
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            for a in range(3):
                lo[a] = min(lo[a], w[a])
                hi[a] = max(hi[a], w[a])
    return lo, hi


def load(path, only=None):
    """
    Import an .obj with the axes the export used. Returns (objects, lo, hi).

    `only` keeps objects whose name contains one of the strings: a vanilla rip carries
    open and closed lids, LOD copies and damage states on top of the visible piece, and
    staging all of them draws three chests on one spot.
    """
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    if only:
        import re
        keep = []
        for o in objs:
            if re.sub(r"\.\d+$", "", o.name) in only:
                keep.append(o)
            else:
                bpy.data.objects.remove(o, do_unlink=True)
        objs = keep
    bpy.context.view_layer.update()
    lo, hi = _bounds(objs)
    return objs, lo, hi


def ground(objs):
    """Sit the lowest point on z = 0 and centre on x/y, so staging is by offset only."""
    lo, hi = _bounds(objs)
    for o in objs:
        o.location.x -= (lo.x + hi.x) / 2.0
        o.location.y -= (lo.y + hi.y) / 2.0
        o.location.z -= lo.z
    bpy.context.view_layer.update()
    return _bounds(objs)


def move(objs, dx=0.0, dy=0.0, dz=0.0, turn=0.0):
    pivot = Vector((0.0, 0.0, 0.0))
    for o in objs:
        if turn:
            o.rotation_euler.z += math.radians(turn)
            c = o.location.copy()
            c.rotate(__import__("mathutils").Euler((0, 0, math.radians(turn))))
            o.location = c
        o.location.x += dx
        o.location.y += dy
        o.location.z += dz
    bpy.context.view_layer.update()


def tint_all(objs=None):
    """Tint by material name. Vanilla rips and our groups both."""
    for mat in bpy.data.materials:
        key = mat.name.split(".")[0].lower()
        colour = TINTS.get(key)
        if colour is None:
            continue
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if not bsdf:
            continue
        bsdf.inputs["Base Color"].default_value = colour
        bsdf.inputs["Roughness"].default_value = 0.88
        if key in vhbuild.EMISSIVE:
            bsdf.inputs["Emission Color"].default_value = colour
            bsdf.inputs["Emission Strength"].default_value = 1.15


# --------------------------------------------------------------------------- measure

def stats(objs):
    """Triangles, open edges, dimensions, triangle share per material group."""
    tris = 0
    open_edges = 0
    share = {}
    for o in objs:
        me = o.data
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        tris += len(bm.faces)
        bm.free()

        bm = bmesh.new()
        bm.from_mesh(me)
        open_edges += sum(1 for e in bm.edges if len(e.link_faces) == 1)
        bm.free()

        for poly in me.polygons:
            slot = o.material_slots[poly.material_index].name if o.material_slots else "?"
            n = len(poly.vertices) - 2
            share[slot.split(".")[0]] = share.get(slot.split(".")[0], 0) + n

    lo, hi = _bounds(objs)
    return {"tris": tris, "open": open_edges, "dim": tuple(hi - lo), "groups": share}


def stat_line(label, st):
    tot = float(sum(st["groups"].values()) or 1)
    g = ", ".join("%s %d%%" % (k, round(100 * v / tot)) for k, v in sorted(
        st["groups"].items(), key=lambda kv: -kv[1]))
    return ("%-22s tris=%-6d open_edges=%-5d %.2f x %.2f x %.2fm  [%s]"
            % (label, st["tris"], st["open"], st["dim"][0], st["dim"][1], st["dim"][2], g))


# --------------------------------------------------------------------------- stage

def stage(ground_size=80.0, key_energy=None, fill_energy=None, sky=None):
    bpy.ops.mesh.primitive_plane_add(size=ground_size, location=(0, 0, 0))
    plane = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    plane.data.materials.append(gm)

    # The camera stands on +y, which is where every model here faces: Blender +y exports
    # to Unity +z, the front. Frame-left is therefore +x.
    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    key = bpy.context.active_object
    key.data.energy = KEY if key_energy is None else key_energy
    key.data.angle = math.radians(3.0)
    key.rotation_euler = (math.radians(52.0), 0.0, math.radians(200.0))

    bpy.ops.object.light_add(type="SUN", location=(-3, 4, 3))
    fill = bpy.context.active_object
    fill.data.energy = FILL if fill_energy is None else fill_energy
    fill.rotation_euler = (math.radians(68.0), 0.0, math.radians(140.0))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.48, 0.44, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = SKY if sky is None else sky


def ref_cube(at):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    cube = bpy.context.active_object
    cm = bpy.data.materials.new("ref")
    cm.use_nodes = True
    cm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.55, 0.58, 1)
    cube.data.materials.append(cm)
    return cube


def eye_camera(centre_x, span, lens=42.0, height=1.7, aim_z=0.8, min_dist=3.0):
    """
    Eye height, as far back as the span needs and never closer than min_dist.

    Not an orbit and not raised: tilted down from 1.7m, which is where a player stands.
    """
    fov = 2.0 * math.atan(18.0 / lens)
    dist = max(min_dist, (span / 2.0 + 0.35) / math.tan(fov / 2.0))
    bpy.ops.object.camera_add(location=(centre_x, dist, height))
    cam = bpy.context.active_object
    cam.data.lens = lens
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 300
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = (centre_x, 0.0, aim_z)
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam
    return dist


def shoot(path, width, height, transparent=False):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = transparent
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    # Scene state, not render state: the next shot in the same run would inherit it.
    scene.render.film_transparent = False


def staged(entries, out, cube_at=None, width=900, height=560, label=""):
    """
    entries: list of (objs, centre_x). The caller has already grounded and positioned.
    Cube goes at cube_at (x) beside the row, in the same plane.
    """
    tint_all()
    stage()
    xs = []
    for objs, cx in entries:
        lo, hi = _bounds(objs)
        xs += [lo.x, hi.x]
    if cube_at is not None:
        ref_cube((cube_at, 0.0, 0.5))
        xs += [cube_at - 0.5, cube_at + 0.5]
    centre = (min(xs) + max(xs)) / 2.0
    dist = eye_camera(centre, max(xs) - min(xs))
    shoot(out, width, height)
    return dist
