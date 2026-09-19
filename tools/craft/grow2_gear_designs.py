"""
GEAR - the carrying half of the stowing post's GROW upgrade.

    blender --background --python tools/craft/grow2_gear_designs.py

GROW does two things: the post holds more (6x2 becomes 8x3) and the spirit carries
twice as much a trip. The first round of designs answered only the first half - three
pieces of storage furniture standing next to a piece of storage furniture - and was
rejected for exactly that. This one answers the second half instead, and answers it
with an object rather than with a container: the spirit's carrying gear, set down on
its rack and waiting to be picked up.

So the subject is the CARRIER. A shoulder yoke cradled across a short A-frame trestle,
a pannier slung from each tip on three cords, a coil of line on one peg and a spare
strap with an iron buckle hung over the bar. Nothing here holds anything the post
holds; the baskets are the spirit's, not yours.

What makes it read as its own object rather than as a fourth shelf:

  * the mass is two ROUND baskets. Everything else in this mod - the post, the bench
    jib, the perch - is sawn and square. A pair of woven cylinders is a silhouette the
    family does not own yet.
  * it is slung, and it is slung outside its own stand. The yoke is 1.16 between tips
    over a trestle 0.60 across, so both panniers hang in clear air with daylight under
    them and between them and the frame. That overhang is the whole silhouette.
  * it is low and wide - 0.95 tall and 1.60 across, against a post that is 1.8 tall and
    1.20 across. It sits under the post rather than arguing with it, and the sibling
    upgrades are both tall and both hold a light. This holds no light and carries no
    heartwood, because it houses no spirit.
  * there is no upright anywhere on it, which is what keeps it clear of the winch.

Materials are the post's own groups and no others - wood, iron, stone, plus `bark` for
cordage, wicker and leather, which falls through to the wood donor at runtime the way
the sibling scripts already use it. Four groups is the post's own count; a fifth for
"rope" would have bought one more palette on screen and nothing at runtime.

Two stands were built and thrown away before this one, and both failures are recorded
against the code that replaced them - a trestle as wide as the yoke (panniers stand on
its own feet) and a single tall post with a bracket (a scarecrow). Neither was a
material or a lighting problem, so neither would have been fixed by more detail.

Lighting is CLAUDE.md's: sun 1.4, fill 0.35, world 0.28, Standard view transform.
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

import bpy
import math
import random

from mathutils import Euler, Vector

from vhbuild import (bevel_all, box, clear_scene, collide, export, finish, material,
                     ring, shell, taper, tint, write_col, COLLIDERS)

from post_heartwood import WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

NAME = "grow2_gear"

# The stowing post that shipped: the neighbour in the render.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the piece stands relative to the post. The post is 1.20 across at the canopy,
# so its side is at x -0.60; this piece is 1.60 wide because the yoke spans, so it
# needs a little more room than a compact one would. 1.70 leaves a third of a metre of
# floor between them - room to walk round, close enough that the link motes are short.
BESIDE = -1.70

LIMIT = 10000

# The yoke's arch, as z = APEX - BOW * x^2. Gentle: a shoulder yoke is curved to clear
# the neck, not bent into a bow, and an exaggerated one reads as a broken branch.
APEX = 0.84
BOW = 0.30
TIP = 0.54

# The whole load hangs in the trestle's own depth plane. An earlier build threw it
# forward on a bracket off a single tall post, and that was a bad trade: it solved the
# panniers standing on the stand's feet and bought a scarecrow. One upright with two
# splayed rakers under it and a symmetrical arm across the top is a torso, legs and
# outstretched arms, and once seen it cannot be unseen.
FRONT = 0.0

# Where each pannier hangs. Outside the yoke's tip so the cords splay outwards rather
# than running straight down, which is what stops them reading as two posts, and well
# outside the trestle so there is daylight between stand and load. The rim sits at 0.46
# and the base at 0.18: a basket on the ground has been put down, one hanging free is
# still ready to go.
SLING = 0.58
RIM = 0.46
RIM_R = 0.21
DROP = 0.28

# The basket shells and their hoops, kept so the bevel pass can skip them - see
# bevel_frame(). Two panniers are four hoops, two walls and two floors, and between them
# they were the single biggest line in this piece's triangle bill.
WICKER = []


# --------------------------------------------------------------------------- parts

def beam(a, b, width, mat, depth=None, over=0.0, tilt=WOOD_TILT):
    """
    A square timber - or, thin enough, a cord - from a to b, run on by `over` at both
    ends.

    Square rather than round because everything on the post is sawn, and the turn is
    the shortest one from +z onto the axis, so a leg that leans in one plane only is
    not also twisted about its own length.
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


def nudge(obj, tilt=2.0):
    """
    Seeded jitter for the parts vhbuild builds dead straight.

    shell() and the horizontal discs take their rotation as an argument and apply no
    wobble of their own, so a basket built from them comes out perfectly upright and
    perfectly concentric with its own bands - which is the machined look the jitter
    exists to remove, showing up on the one part of the model that is meant to be
    hand-woven.
    """
    obj.rotation_euler.x += math.radians(random.uniform(-tilt, tilt))
    obj.rotation_euler.y += math.radians(random.uniform(-tilt, tilt))
    obj.rotation_euler.z += math.radians(random.uniform(-tilt, tilt))
    obj.location.x += random.uniform(-0.006, 0.006)
    obj.location.y += random.uniform(-0.006, 0.006)
    return obj


def arch(x):
    """Height of the yoke's underside centreline at x."""
    return APEX - BOW * x * x


def pannier(side, load):
    """
    One woven basket, slung from the yoke's tip and hanging clear of the ground.

    Built as an open shell with a separate floor rather than as a capped cone: a cone's
    top cap is a lid, and a pannier you cannot see into is a barrel. From eye height
    you look down into this one and see its far inside wall, which is most of what
    makes it read as woven rather than turned.

    Bands are single thin cylinders standing proud of the weave, not rings of blocks -
    only their outer wall is ever seen, and a ring of blocks costs ten times the
    triangles to say the same thing.

    Thirteen sides, and dropping to eleven to save triangles makes it MORE expensive,
    not less. bevel_all limits itself to edges over 30 degrees: at thirteen sides
    adjacent faces meet at 27.7 and no vertical edge is chamfered at all, at eleven they
    meet at 32.7 and every one of them is. Measured, 11 sides cost 168 triangles more
    across the piece than 13 - and read less round.
    """
    x = side * SLING
    # One hangs a little lower than the other. Level, the pair read as the pans of a
    # balance - a beam on a fulcrum with a dish on each end is a scale before it is
    # anything else, and the cure is slack in one set of cords, which is what real hung
    # gear looks like anyway.
    rim = RIM + (0.05 if side < 0 else -0.02)
    base = rim - DROP

    WICKER.append(nudge(shell(0.155, RIM_R, DROP, (x, FRONT, base + DROP * 0.5), "bark",
                              sides=13)))
    WICKER.append(nudge(taper(0.158, 0.158, 0.04, (x, FRONT, base + 0.035), "bark",
                              sides=13, tilt=1.0, spin=False)))

    # Rim band and a waist band, each a hand's width of hoop proud of the weave.
    WICKER.append(nudge(shell(RIM_R + 0.013, RIM_R + 0.013, 0.05, (x, FRONT, rim - 0.012),
                              "bark", sides=13), tilt=1.2))
    WICKER.append(nudge(shell(0.183, 0.183, 0.04, (x, FRONT, base + 0.085), "bark",
                              sides=13), tilt=1.2))

    # An iron ring at the yoke's tip, and three cords down to the rim. A tripod sling,
    # not two parallel cords: two would be a swing, and three is why a carried basket
    # does not spin.
    # Seven by four rather than nine by five. At 3.8cm across and hung under a timber it
    # is nine pixels of iron at eye height, so the segments buy nothing and the two rings
    # together were 180 triangles of the piece's bill.
    tip = (side * TIP, FRONT, arch(TIP))
    ring(0.038, 0.013, (tip[0], tip[1], tip[2] - 0.034), "iron", major=7, minor=4,
         rot_x=90.0)

    for index in range(3):
        angle = math.radians(90.0 + index * 120.0 + side * 18.0)
        beam(tip,
             (x + math.cos(angle) * (RIM_R - 0.02),
              FRONT + math.sin(angle) * (RIM_R - 0.02), rim - 0.01),
             0.020, "bark", over=0.012, tilt=2.0)

    if load:
        # Split billets standing in the near basket. Something has to be in one of them
        # or the piece says "empty baskets" rather than "more per trip", and billets
        # read at eye height where loose ore would be three pebbles in a bowl.
        for bx, by, height, ry, rx in ((-0.050, -0.040, 0.34, 9.0, 6.0),
                                       (0.055, 0.045, 0.30, -11.0, -5.0),
                                       (-0.012, 0.068, 0.25, 4.0, 10.0)):
            box((0.072, 0.072, height), (x + bx, FRONT + by,
                                         base + height * 0.5 + 0.03),
                "wood", rot_y=ry, rot_x=rx, tilt=WOOD_TILT)
    else:
        # A folded hide in the far one, sitting just proud of the rim, so the pair are
        # not one shape twice.
        box((0.27, 0.19, 0.07), (x, FRONT, rim - 0.025), "bark", rot_y=3.0, tilt=2.5)


# --------------------------------------------------------------------------- design

def gear():
    """
    The whole piece: a short trestle, the yoke cradled across its back, and a pannier
    hanging from each tip well outside it.

    Three stands were tried before this one and the two that failed are worth keeping
    in writing. A trestle as wide as the yoke puts the panniers over its own feet, so
    the stand and the load fight for the same ground and it reads as tubs on a box. A
    single tall post with the yoke on a bracket clears that, and reads as a scarecrow -
    torso, splayed legs, arms out, a head where the iron cap is. Neither was a material
    or a lighting problem and no amount of detail would have fixed either.

    What works is making the stand SHORT in every direction: an A-frame trestle half a
    metre across under a yoke three times that, so the yoke overhangs enormously and the
    panniers hang in clear air on both sides. Nothing is upright long enough to be a
    body, and the whole piece is low and wide where the post it stands beside is tall
    and narrow.

    It is also not the winch: that is a drum slung BETWEEN two uprights, and there is no
    upright here at all.
    """
    # ONE stone sill under the whole trestle, not a bar under each pair of feet, and this
    # is half of the animal cure rather than a tidy-up. Two separate pads under two pairs
    # of timbers is four FEET, and four feet under a body with a bag hanging off each side
    # is a pack animal however the timbers above them are arranged. A single continuous
    # plinth is a footing, and nothing with a footing is alive. It is also what the post
    # next to it stands on, so the pair read as one household's work.
    box((0.80, 0.58, 0.10), (0.0, 0.0, 0.05), "stone", tilt=WOOD_TILT)

    # Two CROSSED frames - a sawbuck - one behind the other, each in the plane the piece
    # is seen from. In the depth plane they would be foreshortened to nothing from in
    # front.
    #
    # They were splayed A-frames for three rounds and the lineup is what caught it: four
    # legs raking outwards under a horizontal bar with a hanging body on each end has a
    # four-legged-animal read, faint but present, and it was the one candidate of the four
    # whose subject could not be named at lineup distance. The earlier single-post version
    # was worse and for the same reason - see the note in the docstring - so the failure is
    # not "too few uprights" or "too many", it is that a symmetrical pair of legs under a
    # spine is an animal whatever is on top of it.
    #
    # A cross has no legs. Two timbers passing each other at half height read as one
    # X-shaped object, and an X carrying a bar is a sawhorse before it is anything else -
    # which is exactly the right lie, because a sawhorse is a thing you SET GEAR DOWN ON.
    for depth in (-0.19, 0.19):
        for side in (-1, 1):
            # Foot outside, head crossing to the other side and dying into the head
            # block. They were run on to 0.91 first, as a real sawbuck's horns are, and
            # that was a mistake for this piece: standing proud of an ARCHED bar they
            # are two spikes either side of a curve, and the render came back reading as
            # something with ears. The horns only work when the thing they cradle is a
            # straight log filling the notch. Stopped at 0.80 they are inside the head
            # block, and the X below the bar is the whole of what shows.
            beam((side * 0.30, depth, 0.06), (-side * 0.19, depth, 0.80), 0.085, "wood",
                 over=0.025)

    # The head: one block tying both frames, wide enough in depth to stand the yoke on
    # and short enough in width that the yoke overhangs it on both sides by half a metre.
    # That overhang is the whole silhouette.
    box((0.34, 0.54, 0.13), (0.0, 0.0, 0.745), "wood", tilt=WOOD_TILT)

    # ONE stretcher through the crossing, tying both frames where they already meet,
    # instead of the two side rails the A-frame needed. The legs cross at about z 0.50 on
    # the centreline, so a single member lands on all four of them at once - which the
    # old splayed legs could not offer, since by half height they were out at the edges
    # with nothing in the middle to bolt to. Fewer, larger parts, and one less pair.
    box((0.15, 0.52, 0.095), (0.0, 0.0, 0.50), "wood", tilt=WOOD_TILT)

    # No pegs on the head any more. The sawbuck's own horns stand 7cm above the yoke
    # where it crosses them, so the yoke is visibly dropped into a notch and lifted out
    # again, which is what the pegs were bought to say. Two parts saved for nothing lost.

    # The yoke: five overlapping timbers along the arch, each turned to the local slope
    # so the top is a continuous curve and not a five-step stair. One straight bar was
    # tried first and reads as a fence rail resting on a trestle.
    for index in range(5):
        x = -0.432 + index * 0.216
        slope = math.degrees(math.atan(BOW * 2.0 * x))
        thick = 0.105 - 0.038 * abs(x) / 0.432
        box((0.26, 0.095, thick), (x, FRONT, arch(x)), "wood", rot_y=slope,
            tilt=WOOD_TILT)

    # The shoulder pad, in leather. It is the one part that says a body goes under this.
    # Kept thin and run wider than the timber: at 5.5cm it was a crate sitting on a bar.
    box((0.36, 0.20, 0.04), (0.0, FRONT, APEX + 0.048), "bark", tilt=2.0)

    # Loaded on the far side of the frame, empty but for a folded hide on the near one.
    pannier(-1, load=True)
    pannier(1, load=False)

    # Iron: a strap over the head where the yoke bears, and one band round the crossing,
    # which is where a sawbuck is actually bolted. Two leg shoes became one band for the
    # same reason the two rails became one stretcher - the X gives the piece a single
    # centre, so the ironwork has somewhere to be instead of being doubled at the edges.
    box((0.34, 0.09, 0.05), (0.0, 0.24, 0.765), "iron", tilt=WOOD_TILT)
    box((0.11, 0.09, 0.19), (0.0, 0.24, 0.50), "iron", tilt=WOOD_TILT)

    # A peg on the near cheek with a coil of line hung on it. It has to point at the
    # viewer: a coil on a sideways peg lies in the depth plane and is seen edge-on,
    # which is a stick. The coil is a real torus - a bright disc with a dark disc in
    # front of it closes its own hole back up under any bloom at all.
    # Lowered into the head block's own front face now that the two cradle pegs are gone
    # - it used to be tucked up beside them, and left where it was it stood 3cm clear of
    # everything with nothing behind it, which is the 5cm-gap rule as a floating stick.
    taper(0.032, 0.026, 0.20, (0.145, 0.20, 0.760), "wood", sides=7, rot_x=90.0,
          tilt=2.0)
    nudge(ring(0.097, 0.027, (0.145, 0.225, 0.667), "bark", major=13, minor=5,
               rot_x=90.0), tilt=3.0)

    # A spare strap hung over the yoke, out in the gap between the trestle and a
    # pannier: two thongs and a bottom bar, so it is a loop with daylight through it
    # rather than a flat tongue. Hung on the centreline first, where it fell in front of
    # the stand and disappeared - dark leather on dark timber at the same depth.
    for dx, sway in ((-0.045, -0.018), (0.045, 0.020)):
        beam((-0.305 + dx, -0.02, arch(-0.305)), (-0.305 + dx + sway, 0.05, 0.545),
             0.038, "bark", tilt=2.0)
    box((0.17, 0.06, 0.045), (-0.305, 0.045, 0.55), "bark", rot_y=-2.0, tilt=3.0)
    box((0.055, 0.07, 0.055), (-0.305, 0.043, 0.61), "iron", tilt=3.0)

    # A bedroll was propped against the leg here for two rounds, to put some weight on
    # the ground under all the hanging. It is gone, and the reason is worth keeping: a
    # bound cylinder at knee height in front of a timber frame reads as a barrel or a
    # short log whichever way it is leaned, and it stood exactly where the eye needs to
    # find the daylight between the stand and the near pannier. The piece is better with
    # an empty floor - few large parts, and the ones left all say the same thing.

    # Colliders. Boxes only, and coarse on purpose: a player walks round this piece and
    # never into it, so a pannier gets one box rather than a cylinder's worth.
    collide((0.0, 0.0, 0.42), (0.70, 0.62, 0.84))
    collide((0.0, 0.0, 0.83), (1.16, 0.40, 0.26))
    collide((-SLING, FRONT, 0.32), (0.46, 0.46, 0.32))
    collide((SLING, FRONT, 0.32), (0.46, 0.46, 0.32))


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


def shoot(path, width, height):
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


def camera(at, aim, lens=42):
    bpy.ops.object.camera_add(location=at)
    cam = bpy.context.active_object
    cam.data.lens = lens
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = aim
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# --------------------------------------------------------------------------- output

def bevel_frame(width=0.014):
    """
    bevel_all(), one segment, but skipping the wicker.

    A chamfer is what stops a box reading as a raw primitive, so every timber, iron and
    stone part here gets one. A basket is a different case in both directions: it is
    already round, so there is nothing for a bright edge line to describe, and it is the
    most expensive thing on the piece to chamfer because its walls are open shells - four
    boundary rings per pannier, every segment of them cut and re-cornered.

    The audit put this piece at 2,840 triangles against a brief asking for well under
    2,000, and the panniers were named as the cost. The obvious answer - fewer sides - is
    the wrong one and has already been measured: bevel_all only chamfers edges over 30
    degrees, thirteen sides meet at 27.7 and are left alone, eleven meet at 32.7 and are
    all chamfered, so dropping the side count cost 168 MORE triangles. Nine would be
    worse again at 40 degrees. The saving has to come off the bevel, not off the mesh -
    which also keeps the baskets as round as they were.
    """
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or obj in WICKER:
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
    """Bevel per object, ONE segment, then join - finish() would bevel with two."""
    clear_scene()
    del WICKER[:]
    gear()
    bevel_frame()
    return finish(NAME, bevel=False)


def preview():
    obj = build()
    colliders = len(COLLIDERS)

    # Into variants/ until it is picked, where the csproj cannot copy it.
    export(obj, NAME, VARIANTS)
    write_col(os.path.join(VARIANTS, NAME + ".col"))

    tris = triangles(obj)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    # Moved after export: the asset stays centred on its own origin.
    obj.location.x += BESIDE
    post()

    tint()
    stage()
    # The camera stands on +y looking towards -y, so world -x falls on the RIGHT of the
    # frame: the post is on the left and the piece beside it on the right. The cube
    # goes behind the post, on the open left of the frame - set behind the piece it
    # stood up as a wall across both panniers, and at the piece's own depth it covered
    # one outright.
    reference_cube((BESIDE - 1.58, -2.05, 0.50))

    # Eye height, 42mm, no orbit, aimed between the post and the piece. Further back
    # than the sibling scripts' 3.6-3.7 for one measurable reason: this piece is 1.60
    # across where theirs are under 1.1, and at 3.6 a 42mm lens cut a pannier off at the
    # frame edge. Pulling back keeps the lens and the eye height, which are what the
    # picture is for, and it is also the only way the metre cube gets open ground to
    # stand on - set behind the post it is hidden by the post, and set behind the piece
    # at the piece's own depth it covered a pannier outright.
    camera((-1.02, 4.55, 1.70), (-1.02, -0.30, 0.72), lens=42)
    shoot(os.path.join(PREVIEWS, NAME + ".png"), 1020, 740)

    flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
    print("DESIGN_OK %s tris=%d colliders=%d size=%.2fx%.2fx%.2f%s"
          % (NAME, tris, colliders, max(xs) - min(xs), max(ys) - min(ys),
             max(zs) - min(zs), flag))
    if tris > LIMIT:
        raise SystemExit("GEAR is over %d triangles" % LIMIT)


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
    # Tighter than the sibling scripts' 2.55. Their pieces are roughly as tall as they
    # are wide; this one is 1.60 by 0.95, and a square icon fitted to its width at 2.55
    # left it swimming in a third of the frame.
    cam.data.ortho_scale = radius * 2.12
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


def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)
    preview()
    icon()


main()
