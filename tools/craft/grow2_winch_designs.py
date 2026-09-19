"""
WINCH - the stowing post's GROW upgrade, second round.

    blender --background --python tools/craft/grow2_winch_designs.py

The first round offered three pieces of storage joinery - a lean-to of bays, a hopper,
a tiered rack - and all three were turned down for the same reason: they were furniture
standing next to the post, which is the dull reading of "it holds more". A shelf beside
a shelf says nothing a bigger shelf would not.

So this round says the other half of what GROW actually does. The upgrade does two
things: the post holds more (6x2 becomes 8x3) **and the spirit carries twice as much a
trip** (10 to 20). The second half is about hauling, and hauling has its own machine.

A windlass: a rope drum slung between two uprights, a crank out to one side, the rope
wound on it and falling to an iron hook. Every shape on it is one this mod has nowhere
else - a horizontal drum, a coil of rope, a crank, a hank of spare rope on a peg - so it
cannot be mistaken for a second post, and it is the only piece in the family whose main
axis lies across the view instead of up it. The two upgrades it stands beside are both
vertical and both hold a light (the bench's jib with its heartwood, the spirit's lantern
house on a pole); this one is low, wide and dark on purpose.

It houses no spirit, so it carries **no heartwood and nothing that glows**. That absence
is how a player tells at a glance which kind of upgrade a piece is, and it is why the
`core` group - present on the post itself - appears nowhere below.

Materials are the post's own: sawn timber, iron, a stone footing under each upright.
The one group the post does not have is `rope`, and it is here because the wound coil is
half the silhouette; skinned as timber it would merge into the drum it is wound on and
the piece would lose the shape that makes it a winch. If four groups turns out to be one
too many at runtime, `ROPE` below is one edit away from being `"wood"`.

Staging is the shipped stowing post (assets/stow_post_canopy.obj) with the winch built
1.37m to one side of it, a 1m cube on the ground for scale, eye height 1.70m, 3.5m back,
42mm, no orbit. Lighting is CLAUDE.md's: sun 1.4, fill 0.35, world 0.28, Standard view
transform - the numbers that keep timber looking like timber.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
# WOOD_TILT is imported rather than restated, so this piece is jittered by the same
# carpenter as the post it stands beside.
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bmesh
import bpy
import math
import random

from mathutils import Euler, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     material, taper, tint, write_col, COLLIDERS)

from post_heartwood import WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped: the neighbour in the render, because this piece is
# never seen anywhere else.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

NAME = "grow2_winch"

# Where the winch stands relative to the post. The post is 1.21 wide over its canopy, so
# its side is at x -0.61; the winch spans -0.73..+0.44 about its own origin, so -1.40
# leaves a third of a metre of floor between them - room to walk through, close enough to
# belong to it.
#
# The winch was 1.33m wide before this and is 1.17m now. It was trimmed for two reasons
# and the second is the real one: an accessory as wide as the station it serves is not
# subordinate to it, whatever its height says.
BESIDE = -1.40

LIMIT = 10000

# Hemp. The colour itself now lives in vhbuild's TINTS, and this local override is gone
# on purpose: registering it here worked only for whoever imported THIS module, so every
# other script that staged grow2_winch.obj - the lineup first - got a bone-white coil and
# no warning at all, because tint() silently skips a name it does not know. A shared
# group is a shared fact. `ROPE` stays as the one edit that would fold it back into
# "wood" if four groups ever turns out to be one too many at runtime.
ROPE = "rope"

# Height of the drum's axis. Everything on the machine is measured off it.
AXLE_Z = 0.78

# The swept tubes, kept so the bevel pass can skip them - see bevel_frame().
TUBES = []


# --------------------------------------------------------------------------- parts

def beam(a, b, width, mat, depth=None, over=0.0, tilt=WOOD_TILT):
    """
    A square timber from a to b, run on by `over` at both ends.

    Copied from upgrade_grow_designs.py rather than imported, because that script runs
    its whole build on import. Square rather than round: everything on the post is sawn,
    and a turned member here would be the one part not made by the same hands. The turn
    is the shortest one from +z onto the timber, so a brace leaning in one plane is not
    also twisted about its own length.
    """
    a = Vector(a)
    b = Vector(b)
    axis = b - a

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(a + b) * 0.5)
    obj = bpy.context.active_object
    obj.scale = (width, width if depth is None else depth, axis.length + over * 2.0)
    turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized())
    lean = Euler((math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)))).to_quaternion()
    obj.rotation_euler = (lean @ turn).to_euler()
    obj.data.materials.append(material(mat))
    return obj


def tube(points, radius, mat, sides=5, name="tube"):
    """
    One swept tube along a polyline: the rope, the fall and the hook, each a single mesh.

    Built as a mesh rather than out of parts, because the two shapes that carry this
    piece are both curves. A coil made of separate rings costs a torus apiece (a 13x5
    torus is 130 triangles before the bevel touches it, and there are nine turns), and a
    hook made of short boxes is a chain of sticks - the rule about limbs applies to iron
    as much as to branches.

    The frame is parallel-transported from one ring to the next rather than rebuilt from
    a fixed up-vector: on a helix the tangent swings through every direction there is, so
    an up-vector frame flips where the tangent passes through vertical and the tube turns
    itself inside out at that ring.
    """
    pts = [Vector(p) for p in points]

    tangents = []
    for i in range(len(pts)):
        if i == 0:
            t = pts[1] - pts[0]
        elif i == len(pts) - 1:
            t = pts[-1] - pts[-2]
        else:
            t = pts[i + 1] - pts[i - 1]
        if t.length < 1e-6:
            t = Vector((0.0, 0.0, 1.0))
        tangents.append(t.normalized())

    seed = Vector((0.0, 0.0, 1.0))
    if abs(tangents[0].dot(seed)) > 0.9:
        seed = Vector((1.0, 0.0, 0.0))
    normal = (seed - tangents[0] * seed.dot(tangents[0])).normalized()

    rings = []
    for i, p in enumerate(pts):
        if i > 0:
            normal = tangents[i - 1].rotation_difference(tangents[i]) @ normal
            normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
        binormal = tangents[i].cross(normal)
        rings.append([p + normal * (math.cos(a) * radius)
                        + binormal * (math.sin(a) * radius)
                      for a in (2.0 * math.pi * s / sides for s in range(sides))])

    centre = sum((v for ring in rings for v in ring), Vector()) / (len(rings) * sides)

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [[bm.verts.new(v - centre) for v in ring] for ring in rings]
    for i in range(len(rings) - 1):
        for s in range(sides):
            t = (s + 1) % sides
            bm.faces.new((verts[i][s], verts[i][t], verts[i + 1][t], verts[i + 1][s]))
    # Capped at both ends. An open tube is a single-sided surface, and the end of the
    # rope is looked straight into from standing.
    bm.faces.new(list(reversed(verts[0])))
    bm.faces.new(verts[-1])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = centre
    obj.data.materials.append(material(mat))
    TUBES.append(obj)
    return obj


def helix(x0, x1, radius, turns, steps, height=AXLE_Z):
    """
    The centreline of a rope wound on a drum lying along x.

    Whole turns, and the phase chosen so both ends of the coil sit at the front of the
    drum: the fall has to leave the coil at a point where a rope would actually leave it,
    and a coil that ends underneath has the fall climbing out from behind its own drum.
    """
    count = int(turns * steps)
    pts = []
    for i in range(count + 1):
        t = i / float(count)
        angle = 2.0 * math.pi * turns * t
        pts.append((x0 + (x1 - x0) * t,
                    math.cos(angle) * radius,
                    height + math.sin(angle) * radius))
    return pts


def arc(centre, radius, start, end, steps, plane_x):
    """A run of points on a circle in the y-z plane at a fixed x - the hook's curl."""
    cy, cz = centre
    pts = []
    for i in range(steps + 1):
        angle = math.radians(start + (end - start) * i / float(steps))
        pts.append((plane_x, cy + math.cos(angle) * radius,
                    cz + math.sin(angle) * radius))
    return pts


def loop(centre, wide, tall, steps, depth=0.012):
    """
    A closed ring standing in the x-z plane: the hank of spare rope on its peg.

    In the x-z plane on purpose. The camera looks along -y, so anything on the drum's own
    axis - a ratchet wheel was tried here first - is seen edge-on and reads as a plate
    rather than as a wheel. A loop standing across that axis is the one round shape this
    machine can show the player face-on.
    """
    cx, cy, cz = centre
    pts = []
    for i in range(steps + 1):
        angle = math.radians(90.0 + 360.0 * i / float(steps))
        # A little sway out of plane, so it hangs like rope rather than like a hoop.
        pts.append((cx + math.cos(angle) * wide,
                    cy + math.cos(2.0 * angle) * depth,
                    cz + math.sin(angle) * tall))
    return pts


# --------------------------------------------------------------------------- design

def winch():
    """
    A windlass beside the post: drum across the view, crank on the outboard end, a hank
    of spare rope on the post side, the working rope wound on and falling to a hook.

    Read from the ground up. Two stone pads, because the post stands on stone and this is
    the same yard. Two uprights with a sill beam through them low down and a knee brace
    up to each - built first as two posts and a drum, it read as a swing frame, and the
    sill and braces are what make it a machine that can be leant on. Iron bearing blocks
    where the axle passes through, which is the one place a timber frame would actually
    carry metal.

    The drum is flanged at both ends, which is both true - the flanges are what keep a
    coil from walking off the end - and useful: they give the coil a hard edge to be read
    against at eye height.

    The crank is out on the far side from the post so it is never behind it, and it is a
    real crank: a stub of axle, an arm off it, a grip parallel to the axle. A handle
    modelled as one bar sticking out of the end is a lever, not a crank, and the
    difference is the whole of whether the piece reads as something you turn.

    The drum is wound only part way along, and the rope leaves the coil clear of both
    uprights. Wound full, the fall came off at the flange and hung down the front of the
    near upright: the hook ended up among the braces in shadow, where the one part of the
    machine that says "load goes here" could not be picked out at all. A part-wound drum
    is also the truer thing - a winch with rope paid out is a winch in use.
    """
    # ------------------------------------------------------------------ frame
    for side in (-1, 1):
        x = side * 0.30
        box((0.28, 0.40, 0.16), (x, 0.0, 0.08), "stone", tilt=WOOD_TILT)
        box((0.15, 0.24, 0.84), (x, 0.0, 0.51), "wood", tilt=WOOD_TILT)

        # Knee brace from the sill out to the upright. Kept in the x-z plane, on the
        # camera's side of nothing - a brace behind the frame is a brace nobody sees.
        beam((side * 0.11, 0.0, 0.27), (side * 0.27, 0.0, 0.70), 0.075, "wood")

        # The bearing. Iron, and wide enough to overlap the upright on every face, so it
        # reads as a block let into the timber rather than a plate stuck on it.
        box((0.19, 0.28, 0.13), (x, 0.0, AXLE_Z), "iron", tilt=WOOD_TILT)

    # Sill through both uprights. Without it the two posts are two sticks.
    box((0.74, 0.18, 0.14), (0.0, 0.0, 0.22), "wood", tilt=WOOD_TILT)

    # ------------------------------------------------------------------ drum
    # One axle straight through the frame, long enough to carry the crank at one end and
    # to stand proud at the other. Seven sides: odd, so it never presents a flat face.
    taper(0.042, 0.042, 1.00, (-0.10, 0.0, AXLE_Z), "iron", sides=7, rot_y=90.0,
          tilt=WOOD_TILT)
    box((0.05, 0.10, 0.10), (0.39, 0.0, AXLE_Z), "iron", tilt=2.0)

    taper(0.115, 0.115, 0.44, (0.0, 0.0, AXLE_Z), "wood", sides=11, rot_y=90.0,
          tilt=WOOD_TILT)
    for side in (-1, 1):
        taper(0.155, 0.155, 0.045, (side * 0.20, 0.0, AXLE_Z), "wood", sides=13,
              rot_y=90.0, tilt=WOOD_TILT)

    # ------------------------------------------------------------------ rope
    # Six turns at 0.138 - the drum's 0.115 plus most of the rope's own thickness, so the
    # coil bites into the drum instead of hovering over it. Whole turns, so the coil ends
    # where it began, at the front of the drum, and the fall leaves it cleanly.
    tube(helix(0.175, -0.10, 0.138, 6, 8), 0.026, ROPE, sides=5, name="coil")

    # The fall, drifting forward as it goes down. A plumb-straight rope reads as a rod,
    # and coming forward puts the hook clear of the frame behind it.
    tube(((-0.10, 0.138, AXLE_Z), (-0.108, 0.196, 0.71), (-0.116, 0.240, 0.63),
          (-0.122, 0.254, 0.57), (-0.126, 0.258, 0.54)), 0.026, ROPE, sides=5,
         name="fall")

    # Whipping where the rope is made off to the hook.
    box((0.055, 0.055, 0.05), (-0.126, 0.258, 0.545), "iron", tilt=2.0)

    # The hook: 290 degrees of curl, so the tip comes back up past its own centre and the
    # thing is visibly a hook rather than a ring or a crook. It hangs in the gap between
    # the drum and the sill - the only patch of open ground this machine has - and it is
    # 20cm across, which is large for the piece and still only just reads at three metres.
    tube(arc((0.258, 0.44), 0.10, 90.0, 380.0, 14, -0.126), 0.026, "iron", sides=5,
         name="hook")

    # ------------------------------------------------------------------ crank
    beam((-0.58, 0.0, AXLE_Z), (-0.58, 0.14, 0.96), 0.065, "iron", tilt=1.0)
    box((0.055, 0.09, 0.09), (-0.58, 0.14, 0.96), "iron", tilt=2.0)
    taper(0.048, 0.048, 0.14, (-0.65, 0.14, 0.96), "wood", sides=9, rot_y=90.0,
          tilt=WOOD_TILT)

    # ------------------------------------------------------------- spare rope
    # An iron peg driven through the head of the post-side upright, with a hank hung on
    # it. It balances the crank at the other end, and it is the second rope shape - which
    # is what a winch is for, said twice.
    taper(0.030, 0.026, 0.18, (0.30, 0.13, 0.86), "iron", sides=7, rot_x=90.0, tilt=1.0)
    tube(loop((0.30, 0.205, 0.745), 0.075, 0.115, 20), 0.024, ROPE, sides=5, name="hank")

    # Two boxes: the footing and sill, and the frame with the drum in it. The crank and
    # the hanging rope are deliberately outside both - a collider round a handle is a
    # collider you catch your shoulder on walking past.
    collide((0.0, 0.0, 0.20), (0.92, 0.44, 0.40))
    collide((0.0, 0.0, 0.72), (0.88, 0.38, 0.50))


# --------------------------------------------------------------------------- staging

def stage():
    """Ground, sun 1.4, fill 0.35, world 0.28 - the numbers that keep timber timber."""
    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0, 0, 0))
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


def post(x=0.0):
    """The shipped stowing post, imported with the same axes it was exported with."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=POST, forward_axis="Z", up_axis="Y")
    for obj in bpy.context.selected_objects:
        obj.location.x += x


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


def bevel_frame(width=0.014):
    """
    bevel_all(), one segment, but skipping the swept tubes.

    A bevel is what stops a box reading as a raw primitive, and every timber and iron
    part here gets one. A tube is already round, so the chamfer buys nothing visible on
    it and costs a great deal: a 5-sided rope has a 72-degree turn at each of its
    longitudinal edges, so every one of them is over the 30-degree limit and the coil
    doubles. Skipping the four tubes took this piece from 3,456 triangles to a little
    over two thousand, with nothing to see for it.
    """
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or obj in TUBES:
            continue

        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("chamfer", "BEVEL")
        modifier.width = width
        modifier.segments = 1
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(30.0)
        modifier.harden_normals = False
        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        except RuntimeError:
            obj.modifiers.remove(modifier)


def build():
    """Bevel per object, one segment, then join - finish() would bevel with two."""
    clear_scene()
    del TUBES[:]
    winch()
    bevel_frame()
    return finish(NAME, bevel=False)


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    obj = build()
    colliders = len(COLLIDERS)

    # Into variants/ until it is picked, where the csproj cannot copy it.
    export(obj, NAME, VARIANTS)
    write_col(os.path.join(VARIANTS, NAME + ".col"))

    tris = triangles(obj)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    # Moved only after the export, so the asset stays centred on its own origin.
    obj.location.x += BESIDE
    post()

    tint()
    stage()
    # The camera stands on +y looking at -y, so -x is on the right of the frame: the post
    # is centre-left and the winch to its right.
    #
    # The cube stands two and a half metres behind rather than beside. At this lens and
    # distance the frame is 2.9m wide where the two pieces stand and they already take
    # 2.73m of it, so there is no floor left at their own depth: put out there the cube
    # was half out of shot and standing over the crank. Set back it reads against open
    # ground behind the gap between them, clear of the coil. The scale that matters in
    # this render is the post anyway - a shipped piece 1.74m tall, right there in frame,
    # which is the whole reason for staging against it.
    reference_cube((-0.78, -2.55, 0.50))

    # Eye height, 3.70m back, 42mm, no orbit.
    #
    # The distance and the aim were measured with world_to_camera_view rather than
    # guessed, and twice: the first guess clipped the post's canopy off the left edge and
    # the second clipped the crank off the right, because the export flips x and a check
    # that reads the .obj as if it did not comes out mirrored - which looks like a fit.
    # Post, floor and winch are 2.73m of frame; at 42mm that is 3.70m back, and the aim
    # sits left of the pair's midpoint because the post is the wider of the two.
    camera((-0.80, 3.70, 1.70), (-0.80, 0.0, 0.80), lens=42)
    render(os.path.join(PREVIEWS, NAME + ".png"), 950, 720)

    icon()

    flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
    print("DESIGN_OK %s tris=%d colliders=%d size=%.2fx%.2fx%.2f%s"
          % (NAME, tris, colliders, max(xs) - min(xs), max(ys) - min(ys),
             max(zs) - min(zs), flag))
    if tris > LIMIT:
        raise SystemExit("%s is over %d triangles" % (NAME, LIMIT))


def icon():
    """
    The crafting post's icon rig: orthographic, front-on, transparent, its own two suns
    and its own exposure, 128px, the model yawed 25 degrees. Into previews/ until the
    piece is picked - an icon for a rejected design is a render, not an asset.
    """
    clear_scene()
    bpy.ops.wm.obj_import(filepath=os.path.join(VARIANTS, NAME + ".obj"),
                          forward_axis="Z", up_axis="Y")
    imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    for obj in imported:
        obj.rotation_euler.z += math.radians(25.0)
    bpy.context.view_layer.update()

    lo = [1e9] * 3
    hi = [-1e9] * 3
    for obj in imported:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            for axis in range(3):
                lo[axis] = min(lo[axis], world[axis])
                hi[axis] = max(hi[axis], world[axis])
    centre = [(lo[i] + hi[i]) * 0.5 for i in range(3)]
    for obj in imported:
        obj.location -= Vector(centre)
    radius = max(hi[0] - lo[0], hi[2] - lo[2]) * 0.5

    tint()

    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(0.0, 2.93, 2.93 * math.tan(math.radians(12.0))))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = radius * 2.55
    cam.rotation_euler = (math.radians(78.0), 0.0, math.radians(180.0))
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(-1.6, 2.0, 1.6))
    key = bpy.context.active_object
    key.data.energy = 2.8
    key.rotation_euler = (math.radians(56.0), 0.0, math.radians(-148.0))

    bpy.ops.object.light_add(type="SUN", location=(1.8, 1.8, -0.6))
    fill = bpy.context.active_object
    fill.data.energy = 1.0
    fill.rotation_euler = (math.radians(106.0), 0.0, math.radians(218.0))

    world = bpy.data.worlds.new("icon")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.62, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.35

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = os.path.join(PREVIEWS, NAME + "_icon.png")
    bpy.ops.render.render(write_still=True)

    # Scene state, not render state: left on, the next preview in the same run gets a
    # white void for a sky and reads as a blown exposure.
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    print("ICON_OK %s radius=%.2f" % (NAME, radius))


main()
