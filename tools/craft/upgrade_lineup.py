"""The stowing post's three upgrades, all nine candidates, in one frame.

    blender --background --python tools/craft/upgrade_lineup.py

Renders assets/previews/upgrade_lineup.png. Nothing is built here: every piece is the
exported .obj the three design scripts wrote to assets/variants/, imported with the same
forward Z / up Y the export used, so this is a picture of the files that would ship and
not of a scene that only exists in Blender.

Layout, left to right as the camera sees it (the camera stands on +y, so +x is the LEFT
of the frame):

    GROW x3    |  bench, bench  POST  bench  |    SPIRIT x3

The post is the middle of the frame and the middle of the BENCH group. Three bench pieces
cannot sit symmetrically round one post, so they split two and one, and the whole bench
group stands one step nearer the camera than the others: the step is what tells the eye
they are one row, without changing their scale against the rest. A second row further
forward was tried on paper and rejected - from eye height every front-row piece covers the
foot of whatever stands behind it, and that is exactly the part of a silhouette a pick is
made on. The jib goes on the post's right because it was drawn with its arm reaching
away from a post on its left.

Groups are separated by a wider gap than pieces within a group, so the position is the
label. Camera is eye height 1.7m at 42mm, pulled back until everything fits - never an
orbit, never raised.
"""

import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math

from mathutils import Vector

from vhbuild import clear_scene, tint
from post_heartwood import GLOW

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# (label, path, depth offset). Listed left to right as they appear in the frame.
BENCH_STEP = 1.0
ROW = [
    ("grow_leanto",   os.path.join(VARIANTS, "upgrade_grow_leanto.obj"),   0.0),
    ("grow_hopper",   os.path.join(VARIANTS, "upgrade_grow_hopper.obj"),   0.0),
    ("grow_tiers",    os.path.join(VARIANTS, "upgrade_grow_tiers.obj"),    0.0),
    None,
    ("bench_block",   os.path.join(VARIANTS, "upgrade_bench_block.obj"),   BENCH_STEP),
    ("bench_trestle", os.path.join(VARIANTS, "upgrade_bench_trestle.obj"), BENCH_STEP),
    ("POST",          POST,                                                0.0),
    ("bench_jib",     os.path.join(VARIANTS, "upgrade_bench_jib.obj"),     BENCH_STEP),
    None,
    ("spirit_perch",  os.path.join(VARIANTS, "upgrade_spirit_perch.obj"),  0.0),
    ("spirit_hut",    os.path.join(VARIANTS, "upgrade_spirit_hut.obj"),    0.0),
    ("spirit_cairn",  os.path.join(VARIANTS, "upgrade_spirit_cairn.obj"),  0.0),
]

PIECE_GAP = 0.55    # clear floor between two pieces of one group
GROUP_GAP = 1.60    # clear floor between groups: wider, so position reads as the label


def load(path):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            for a in range(3):
                lo[a] = min(lo[a], w[a])
                hi[a] = max(hi[a], w[a])
    return objs, lo, hi


def place(objs, dx, dy):
    for obj in objs:
        obj.location.x += dx
        obj.location.y += dy


def stage():
    """Sun 1.4, fill 0.35, world 0.28 - the stow scripts' numbers."""
    bpy.ops.mesh.primitive_plane_add(size=120.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    plane.data.materials.append(gm)

    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    sun = bpy.context.active_object
    sun.data.energy = 1.4
    sun.rotation_euler = (math.radians(52), 0, math.radians(200))

    bpy.ops.object.light_add(type="SUN", location=(-3, 4, 3))
    fill = bpy.context.active_object
    fill.data.energy = 0.35
    fill.rotation_euler = (math.radians(68), 0, math.radians(140))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.28


def reference_cube(at):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    obj = bpy.context.active_object
    mat = bpy.data.materials.new("ref")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.52, 0.52, 0.56, 1)
    obj.data.materials.append(mat)


def camera(at, aim, lens=42):
    bpy.ops.object.camera_add(location=at)
    cam = bpy.context.active_object
    cam.data.lens = lens
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 300
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = aim
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam
    return cam


def render(path, width, height):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = False
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    clear_scene()

    loaded = []
    for slot in ROW:
        if slot is None:
            loaded.append(None)
            continue
        label, path, dy = slot
        objs, lo, hi = load(path)
        loaded.append((label, objs, lo, hi, dy))

    # Walk right to left in world x (left of frame is +x), leaving a gap after each piece.
    # First pass measures the whole run so the post can be centred on x = 0.
    positions = []
    cursor = 0.0
    gap = None
    for item in loaded:
        if item is None:
            gap = GROUP_GAP
            continue
        label, objs, lo, hi, dy = item
        width = hi.x - lo.x
        if positions:
            cursor -= (gap if gap is not None else PIECE_GAP)
        gap = None
        # The piece's +x edge sits at the cursor.
        dx = cursor - hi.x
        positions.append((label, objs, dx, dy, lo, hi))
        cursor -= width

    post = [p for p in positions if p[0] == "POST"][0]
    centre = post[2] + (post[4].x + post[5].x) / 2
    span_left = positions[0][2] + positions[0][5].x - centre
    span_right = centre - (positions[-1][2] + positions[-1][4].x)

    for label, objs, dx, dy, lo, hi in positions:
        place(objs, dx - centre, dy)
        print("LINEUP %-14s x=%+.2f  y=%+.2f  w=%.2f  h=%.2f"
              % (label, dx - centre + (lo.x + hi.x) / 2, dy, hi.x - lo.x, hi.z - lo.z))

    tint(GLOW)
    stage()

    # The cube stands past the far end of SPIRIT, a group gap away. Put in the gap between
    # GROW and the bench pair (the first try) it read as a fourth bench piece.
    spirit_end = positions[-1][2] + positions[-1][4].x - centre
    reference_cube((spirit_end - GROUP_GAP - 0.5, 0.0, 0.5))
    span_right += GROUP_GAP + 1.0

    # Pull back until the wider half fits in a 42mm frame with a little air.
    half = max(span_left, span_right) + 0.8
    fov = 2 * math.atan(18.0 / 42.0)
    distance = half / math.tan(fov / 2)
    camera((0.0, distance, 1.70), (0.0, 0.0, 0.80), lens=42)
    print("LINEUP camera distance %.2f  half width %.2f" % (distance, half))

    os.makedirs(PREVIEWS, exist_ok=True)
    render(os.path.join(PREVIEWS, "upgrade_lineup.png"), 3200, 620)
    print("DESIGN_OK upgrade_lineup")


main()
