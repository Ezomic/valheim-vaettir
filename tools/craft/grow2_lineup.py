"""The four GROW candidates round one stowing post, with the two upgrades already chosen.

    blender --background --python tools/craft/grow2_lineup.py

Renders assets/previews/grow2_lineup.png. Nothing is modelled here. Every piece is the
exported .obj its own design script wrote, imported with the same forward Z / up Y the
export used, so this is a picture of the files that would ship rather than of a scene that
only exists in Blender. If a piece looks wrong here it is wrong in the game.

Layout, left to right as the camera sees it. The camera stands on +y, so +x is the LEFT
of the frame and the walk below runs towards -x:

    POST  |  barrow  gear  winch  cellar  |  bench jib  spirit perch  |  1m cube

Four decisions are in that line and each was made against an alternative:

  * The post stands at the LEFT END, not in the middle. The brief's other reading - post
    centred, two candidates either side - puts the two outer candidates 6m apart with the
    post between them, and a pick is made by comparing silhouettes against each other, not
    against the post. Ranged along one wall they are neighbours; the post being first means
    every one of them is read left-to-right *after* the thing it stands beside, which is the
    order the eye wants for "is this subordinate to it".

  * The two chosen upgrades sit at the FAR end behind their own wider gap. They are not
    candidates and must not be mistaken for a fifth and sixth; the gap is the whole of what
    says so, since nothing here is captioned. They are in the frame for one question only -
    whether the four candidates compete with a vertical piece that holds a light - and that
    question is answered by having them in the same picture at the same scale.

  * Every piece is centred on its own DEPTH bounds before placing, so they stand on one
    line rather than wherever each script happened to leave its origin. The cellar is 1.25m
    deep against the winch's 0.58 and without this it sat a half metre forward of the row,
    which reads as "bigger" when it is only "nearer".

  * Gaps are clear floor between bounding boxes, not centre spacing. The candidates run
    1.18m to 1.66m wide, so centre spacing would crowd the barrow and strand the winch.

Camera and lights are the stow scripts' numbers - eye height 1.70m, 42mm, no orbit, sun
1.4, fill 0.35, world 0.28, Standard view transform. The distance is computed from the
span rather than typed, because the span changes whenever a candidate's width does, and a
hand-tuned distance is the way a piece ends up clipped at the frame edge.
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

from vhbuild import clear_scene, tint, TINTS
from post_heartwood import GLOW

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Left to right as the frame sees it. None is a group break.
ROW = [
    ("POST",    POST),
    None,
    ("barrow",  os.path.join(VARIANTS, "grow2_barrow.obj")),
    ("gear",    os.path.join(VARIANTS, "grow2_gear.obj")),
    ("winch",   os.path.join(VARIANTS, "grow2_winch.obj")),
    ("cellar",  os.path.join(VARIANTS, "grow2_cellar.obj")),
    None,
    ("jib",     os.path.join(VARIANTS, "upgrade_bench_jib.obj")),
    ("perch",   os.path.join(VARIANTS, "upgrade_spirit_perch.obj")),
]

# Material groups this lineup must know about that the shared TINTS does not carry.
#
# vhbuild.tint() SKIPS any material whose name is not in TINTS, and a skipped material keeps
# Blender's default Principled BSDF, which is very nearly WHITE. So a piece that invents a
# group renders it white here and correctly in its own script, and nothing anywhere says so:
# the first render of this file had the winch's rope coil and hank glowing bone-white, which
# reads as a material or colour-space bug rather than as a missing dictionary key.
#
# It is EMPTY now, and that is the fix rather than the problem: "rope" was the one entry and
# it has moved into vhbuild's own TINTS, where a group two scripts share belongs. The table
# and the check below stay, because the next piece to invent a group will make exactly this
# mistake again and the guard is what turns it from a silent white part into a failed render.
EXTRA_TINTS = {}

PIECE_GAP = 0.45    # clear floor between two pieces of one group
GROUP_GAP = 1.25    # clear floor between groups: wider, so position reads as the label
CUBE_GAP = 1.25     # and the reference cube stands off on its own beyond the last piece

# A wide, short frame. The span fixes the distance, so the only way to buy pixels for a
# silhouette is to spend them on width and stop paying for sky: 3600x800 put the top of the
# frame 1.85m above the aim, which is a metre of empty air over a 1.74m post.
WIDTH, HEIGHT = 4200, 660
LENS = 42.0
SENSOR_HALF = 18.0  # 36mm sensor, horizontal fit
EYE = 1.70


def load(path):
    """Import one .obj and return its meshes with world-space bounds.

    forward_axis Z / up_axis Y is the exact inverse of the export, so Blender x/y/z here
    are the obj's x/z/y: depth is y and HEIGHT IS Z. Measuring height off y is the quiet
    way to prove a piece is subordinate to the post when it is not.
    """
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
    """Sun 1.4, fill 0.35, world 0.28 - the stow scripts' numbers, not vhbuild's brighter
    stage_scene. A 3.2 sun puts every timber on one value and no silhouette can be judged,
    which is the whole job of this picture."""
    bpy.ops.mesh.primitive_plane_add(size=200.0, location=(0, 0, 0))
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


def camera(at, aim, lens=LENS):
    bpy.ops.object.camera_add(location=at)
    cam = bpy.context.active_object
    cam.data.lens = lens
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 400
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = aim
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam
    return cam


def render(path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = False     # scene state; this render wants the sky
    try:
        scene.view_settings.view_transform = "Standard"   # never AgX - it rolls the glow to white
    except TypeError:
        pass
    scene.render.resolution_x = WIDTH
    scene.render.resolution_y = HEIGHT
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    clear_scene()

    loaded = []
    for slot in ROW:
        if slot is None:
            loaded.append(None)
            continue
        label, path = slot
        if not os.path.exists(path):
            raise SystemExit("LINEUP missing %s - run that piece's design script first" % path)
        objs, lo, hi = load(path)
        loaded.append((label, objs, lo, hi))

    # Walk towards -x, leaving clear floor after each piece. The first pass measures so the
    # whole run can be centred on x = 0 afterwards.
    placed = []
    cursor = 0.0
    gap = None
    for item in loaded:
        if item is None:
            gap = GROUP_GAP
            continue
        label, objs, lo, hi = item
        if placed:
            cursor -= (gap if gap is not None else PIECE_GAP)
        gap = None
        dx = cursor - hi.x                # the piece's +x edge lands on the cursor
        dy = -(lo.y + hi.y) / 2.0         # stand it on the row's line, not on its own origin
        placed.append((label, objs, dx, dy, lo, hi))
        cursor -= (hi.x - lo.x)

    left_edge = placed[0][2] + placed[0][5].x
    cube_x = cursor - CUBE_GAP - 0.5      # cube is placed by its centre, framed by its near face
    right_edge = cube_x - 0.5
    centre = (left_edge + right_edge) / 2.0

    tallest = 0.0
    for label, objs, dx, dy, lo, hi in placed:
        place(objs, dx - centre, dy)
        tallest = max(tallest, hi.z - lo.z)
        print("LINEUP %-8s x=%+6.2f  w=%.2f  d=%.2f  h=%.2f"
              % (label, dx - centre + (lo.x + hi.x) / 2, hi.x - lo.x, hi.y - lo.y, hi.z - lo.z))

    TINTS.update(EXTRA_TINTS)
    tint(GLOW)

    # Say out loud what tint() could not colour. An untinted material is white, and white in
    # a lineup is read as a deliberate choice about the piece rather than as a bug in this file.
    unknown = sorted({m.name.split(".")[0].lower() for m in bpy.data.materials
                      if m.name.split(".")[0].lower() not in TINTS
                      and m.name.split(".")[0].lower() not in ("ground", "ref")})
    if unknown:
        raise SystemExit("LINEUP untinted material groups %s - add them to EXTRA_TINTS or they "
                         "render white" % ", ".join(unknown))

    stage()
    reference_cube((cube_x - centre, 0.0, 0.5))

    # Distance from the span, never typed. Half the run plus a little air must fit the
    # 42mm half-angle; the vertical then follows from the aspect and is checked, because a
    # frame wide enough to hold the row can still cut the post's canopy off at the top.
    half = max(left_edge - centre, centre - right_edge) + 0.7
    fov = 2 * math.atan(SENSOR_HALF / LENS)
    distance = half / math.tan(fov / 2)
    aim_z = 0.80
    cam = camera((0.0, distance, EYE), (0.0, 0.0, aim_z))
    vertical_half = half * HEIGHT / WIDTH
    print("LINEUP camera distance %.2f  half %.2f  vertical half %.2f  tallest %.2f"
          % (distance, half, vertical_half, tallest))
    if aim_z + vertical_half < tallest + 0.15:
        raise SystemExit("LINEUP frame is too short for the post - raise HEIGHT or widen the run")

    os.makedirs(PREVIEWS, exist_ok=True)
    render(os.path.join(PREVIEWS, "grow2_lineup.png"))
    print("DESIGN_OK grow2_lineup")


main()
