"""
GROW, second round: the hand-barrow.

    blender --background --python tools/craft/grow2_barrow_designs.py

The first round offered three pieces of storage joinery and all three were refused,
rightly: a shelf beside the post says "here is a shelf", and the upgrade does not add
a shelf. It adds room in the post (6x2 becomes 8x3) and doubles what the spirit takes
in one trip. The second of those is the half nobody modelled.

So this is a vehicle at rest. A hand-barrow stood on its legs beside the post, wheel
forward, handles laid over towards the post as if whoever loads it has just set it
down - and loaded, heaped a little proud of its rim, because an empty barrow says
"could carry" and a full one says "carries this much". The reading is "more goes in
one trip", which is what the upgrade actually does, and it is the only one of the
four second-round pieces that is a machine for moving rather than a place to put.

Its outline is the argument. The post is a tall flat cabinet, 1.20 wide and 1.74 up;
this is 1.66 long, 0.74 across and 1.06 to the top of the sack, so it lies along the
ground where the post stands and comes up to the post's second shelf. The eye reads
two different objects rather than a big one and a small copy of it.
The other two upgrades are vertical and both carry a light - the bench's jib and the
perch's lantern house - so a low horizontal thing with a wheel in it shares nothing
with either. It houses no spirit, so there is no heartwood and nothing glows here.

Ordinary materials, and the post's own three groups so the pair reads as one
household's work: sawn timber, iron at the tyre and the strapping, and the dark cloth
of a sack. No stone - a barrow that has stonework on it is not a barrow.

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

import bmesh
import bpy
import math
import random

from mathutils import Euler, Matrix, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     material, orb, ring, taper, tint, write_col, COLLIDERS)

from post_heartwood import WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

NAME = "grow2_barrow"

# The stowing post that shipped: the neighbour in every render.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the barrow stands relative to the post. The post is 1.20 wide, so its side is
# at x -0.60; the barrow is 1.66 long lying across the view, so at -1.78 its handle
# end stops just short of 0.3m from the post - parked beside it, not leaning on it.
BESIDE = -1.78

LIMIT = 10000

# --------------------------------------------------------------------------- the frame
#
# Everything is modelled in the barrow's own frame - x along the shafts from the axle
# back to the grips, y across, z up off the shaft line - and then raked and dropped
# into place by W(). A barrow at rest sits nose down on its wheel and tail up on its
# legs, and building it level and tipping it afterwards is the only way the tray, the
# load and the shafts all lean by the same seven degrees. Modelling each part at its
# own angle instead is how a load ends up lying flat in a tilted tray.

RAKE = 7.0
WHEEL_R = 0.245          # the timber disc; the iron tyre wraps it out to 0.288
TYRE_R = 0.258
TYRE_T = 0.030

_RAKED = Matrix.Rotation(math.radians(-RAKE), 3, "Y")

# x centres the model on its own origin (the runtime puts the mesh on the piece at the
# origin); z drops the tyre 7mm into the ground, because a wheel that exactly kisses a
# flat plane reads as hovering the moment the terrain is not flat.
ORIGIN = Vector((-0.551, 0.0, 0.281))


def W(x, y, z):
    """A point in the barrow's frame, raked and placed."""
    return _RAKED @ Vector((x, y, z)) + ORIGIN


def shaft_y(x):
    """The shafts converge towards the grips, as a barrow's do, so the hands come in."""
    return 0.205 - 0.055 * (x / 1.36)


# --------------------------------------------------------------------------- parts

def slab(corners, thick, mat, tilt=WOOD_TILT):
    """
    A board of any four-cornered outline, `thick` through, centred on the face given.

    box() can only make rectangles, and every side of a barrow's tray is a trapezoid -
    it flares outwards and upwards at once. A rectangle standing in for one either
    pokes its corners out past the neighbouring board or leaves a wedge of daylight
    between them, and both read from eye height. Built as a convex hull and then
    dissolved back to quads, so it bevels like a box.
    """
    pts = [Vector(c) for c in corners]
    normal = (pts[1] - pts[0]).cross(pts[3] - pts[0]).normalized()
    half = normal * (thick * 0.5)
    verts = [p + half for p in pts] + [p - half for p in pts]

    centre = sum(verts, Vector()) / len(verts)

    mesh = bpy.data.meshes.new("slab")
    bm = bmesh.new()
    for v in verts:
        bm.verts.new(v - centre)
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0),
                             verts=list(bm.verts), edges=list(bm.edges))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("slab", mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = centre
    obj.rotation_euler = (math.radians(random.uniform(-tilt, tilt)),
                          math.radians(random.uniform(-tilt, tilt)),
                          math.radians(random.uniform(-tilt, tilt)))
    obj.data.materials.append(material(mat))
    return obj


def beam(a, b, width, mat, depth=None, over=0.0, tilt=WOOD_TILT):
    """
    A square timber from a to b, run on by `over` at both ends.

    Square, not round, because everything on the post is sawn: a round shaft on this
    piece would be the one thing in the pair that was not made by the same hands. The
    turn is the shortest one from +z onto the timber, so a leg that leans only in one
    plane is not twisted about its own length.
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


def raked(size, at, mat, extra_y=0.0, tilt=WOOD_TILT):
    """A box lying in the barrow's frame: placed by W() and leaning with the rest."""
    return box(size, W(*at), mat, rot_y=-RAKE + extra_y, tilt=tilt)


# --------------------------------------------------------------------------- the barrow

def wheel():
    """
    One wheel, forward, between the shaft ends, on an iron pin through them.

    A plank wheel rather than a spoked one: spokes at this size are eight sticks of
    two centimetres, which is the confetti CLAUDE.md warns about, and the Norse
    three-plank disc is both older and a far better silhouette - a solid dark circle
    is the one shape in this whole mod that is unmistakably a vehicle. The two cleats
    across its faces are what say plank rather than coin, and the iron tyre is one
    thin torus, not a ring of blocks, which is where the triangles would have gone.
    """
    at = W(0.0, 0.0, 0.0)

    taper(WHEEL_R, WHEEL_R, 0.085, at, "wood", sides=15, rot_x=90.0, tilt=1.0)
    ring(TYRE_R, TYRE_T, at, "iron", major=13, minor=5, rot_x=90.0)
    taper(0.078, 0.078, 0.205, at, "wood", sides=9, rot_x=90.0, tilt=1.0)

    # The axle runs out past both shafts so the pin is seen to go through them.
    taper(0.036, 0.036, 0.52, at, "iron", sides=7, rot_x=90.0, tilt=0.6)

    # A cleat laid on each face, at different angles, so the wheel is planked rather
    # than turned - and so the two faces are not each other's mirror.
    for side, angle in ((1.0, 34.0), (-1.0, -29.0)):
        box((0.40, 0.026, 0.075), W(0.0, side * 0.052, 0.0), "wood",
            rot_y=angle, tilt=1.0)


def frame():
    """The two shafts, their grips, the legs under the tail, and a stretcher between."""
    for side in (-1, 1):
        beam(W(-0.02, side * shaft_y(0.0), 0.0),
             W(1.36, side * shaft_y(1.36), 0.0), 0.075, "wood", over=0.03)

        # The grips are round where the shafts are square: the one turned part on the
        # barrow, because it is the one part a hand closes on.
        taper(0.037, 0.032, 0.26, W(1.27, side * shaft_y(1.27), 0.0), "wood",
              sides=9, rot_y=90.0 - RAKE, tilt=1.0)

        # Legs stand plumb in the world, not square to the raked shafts - that is what
        # standing on the ground means, and it is most of why the barrow reads as
        # parked rather than as a model tipped over.
        top = W(1.00, side * shaft_y(1.00), -0.01)
        beam(top, Vector((top.x + 0.035, side * 0.215, 0.02)), 0.072, "wood", over=0.02)

    stretcher = W(1.00, 0.0, -0.02)
    box((0.085, 0.46, 0.06), (stretcher.x + 0.02, 0.0, 0.15), "wood", tilt=WOOD_TILT)


def tray():
    """
    The body: a floor on the shafts and four boards flaring out and up from it.

    The front board leans forward over the wheel and the tail board is cut low, so the
    outline falls away towards the handles and the load can be seen over the tail. A
    tray with four boards of one height is a crate, and a crate on a wheel still reads
    as a crate.
    """
    raked((0.74, 0.48, 0.055), (0.70, 0.0, 0.055), "wood")

    for side in (-1, 1):
        slab((W(0.34, side * 0.235, 0.03), W(1.06, side * 0.235, 0.03),
              W(1.14, side * 0.335, 0.40), W(0.20, side * 0.335, 0.40)), 0.042, "wood")

        # A timber along each top edge. Seen square on a 4cm board is its own edge and
        # nothing else, and the flare of the tray disappears with it.
        beam(W(0.22, side * 0.336, 0.41), W(1.15, side * 0.336, 0.41), 0.055, "wood",
             over=0.02)

    slab((W(0.34, -0.245, 0.02), W(0.34, 0.245, 0.02),
          W(0.20, 0.345, 0.40), W(0.20, -0.345, 0.40)), 0.045, "wood")

    slab((W(1.06, -0.245, 0.02), W(1.06, 0.245, 0.02),
          W(1.14, 0.310, 0.30), W(1.14, -0.310, 0.30)), 0.045, "wood")

    # Iron, in the three places a barrow actually wears it: a strap across the front
    # board and a flat one over each shaft where the tray is pinned down to it. This
    # is the post's own idiom - it carries the same straps across its open front - and
    # it is what makes the two objects read as the same household's ironwork.
    box((0.05, 0.62, 0.06), W(0.308, 0.0, 0.224), "iron", tilt=1.0)
    for side in (-1, 1):
        raked((0.24, 0.075, 0.05), (0.42, side * 0.215, 0.045), "iron", tilt=1.0)


def load():
    """
    Split billets across the tray with a sack slumped at the tail.

    Loaded, not empty, and heaped a good hand's width over the rim. The first pass put
    the load level with the boards and it disappeared: from standing you look down
    into a tray whose lip is three quarters of a metre up, the near board takes the
    light, and everything behind it is one shadow - the barrow read as empty. What
    carries a load at eye height is the part of it standing above the side.

    Two courses of fat billets rather than three of thin ones, for the same reason
    every other rule here says fewer and bigger: seven pieces of 18cm timber read as
    firewood, fifteen of 8cm read as a heap of gravel. They lie across with their ends
    to the player, because a sawn end catches the light and a bark flank does not.

    The sack is the only soft shape on the piece and earns its place by contrast -
    everything else is straight timber and one circle. Its neck is tied and flopped
    over to one side: standing straight up with a band round it, it read as the
    stopper of a jug, and the whole thing turned into pottery.

    It is built as TWO lobes with a pinch between them, and the pinch is exaggerated,
    because the first version was one ellipsoid with a small tied neck and read as a
    boulder sitting on the firewood. A sack is not recognised by its body - a bag of
    grain and a rock are the same silhouette - it is recognised by the gather: a wide
    slumped bottom, a narrower shoulder above it, and a throat pinched to a third of
    that with a band round it. At 40cm across and seen from three and a half metres,
    a 5cm neck is four pixels of pinch and carries nothing, so the neck here is half
    again as thick as the first one and the tie is half again as wide, which looks
    coarse in isolation and is the only thing that reads in the render.

    The belly is flattened and set low so it spreads over the billets under it rather
    than balancing on them. A sack takes the shape of what it is put down on, and that
    deformation is the other half of what says cloth instead of stone.
    """
    # Bottom course, then a second riding in its hollows, tailing off towards the
    # sack: a load stacked in two clean rows is a diagram of a load.
    billets = ((0.44, -0.02, 0.215, 0.098, 0.082, 4.0),
               (0.63, 0.03, 0.210, 0.092, 0.092, -6.0),
               (0.82, -0.01, 0.215, 0.100, 0.078, 9.0),
               (0.99, 0.02, 0.205, 0.088, 0.088, -3.0),
               (0.53, 0.01, 0.395, 0.094, 0.076, 7.0),
               (0.73, -0.03, 0.400, 0.086, 0.086, -11.0),
               (0.91, 0.04, 0.385, 0.090, 0.072, 13.0))

    for x, y, z, r0, r1, skew in billets:
        # Five sides, not seven. A split billet has flat faces - that is what splitting
        # is - so the cheaper cylinder is also the more honest one, and seven of them
        # at seven sides were a fifth of the whole piece's triangles.
        taper(r0, r1, 0.43, W(x, y, z), "wood", sides=5, rot_x=90.0, rot_y=skew,
              tilt=2.0)

    # One subdivision, not two. At 40cm across, a 320-triangle sphere spends an eighth
    # of the whole piece's budget on smoothness nothing can see, and the game's own
    # sacks are visibly faceted.
    #
    # The belly: wide, low and squashed, sitting down INTO the top course of billets
    # rather than on it. Its centre is 5cm below the crowns of the wood beneath, so the
    # lower third of it is swallowed and what is left above reads as cloth taking the
    # shape of what is under it.
    belly = orb(0.178, W(1.00, -0.045, 0.418), "bark", subdivisions=1, tilt=3.0)
    belly.scale = (1.28, 1.14, 0.70)

    # The shoulder, smaller and set back over the belly, overlapping it by most of its
    # own radius. Two lobes rather than one, so there is a waist to pinch: a single
    # ellipsoid has no place a neck can come out of and whatever is stuck on top of it
    # reads as something resting on a stone, which is what the first pass looked like.
    shoulder = orb(0.128, W(1.055, -0.028, 0.545), "bark", subdivisions=1, tilt=4.0)
    shoulder.scale = (1.06, 0.96, 0.92)

    # The gathered neck, tied and fallen over the tail board. Deliberately fat - 15cm at
    # the gather against the shoulder's 27 - because the pinch is the whole recognition
    # and a thin one disappears at eye height. It flops 38 degrees off vertical and out
    # to the side, so the throat is seen from the side as a bend rather than end-on as a
    # stub: end-on it was the stopper of a jug.
    taper(0.075, 0.038, 0.19, W(1.115, 0.020, 0.640), "bark", sides=7, rot_x=-38.0,
          rot_y=26.0, tilt=3.0)

    # The binding, at the narrowest point and run wide of the cloth on both sides. A tie
    # flush with the neck is a stripe; one standing proud of it is a knot.
    ring(0.074, 0.021, W(1.090, 0.006, 0.604), "bark", major=9, minor=4, rot_x=-38.0)


def barrow():
    wheel()
    frame()
    tray()
    load()

    # Three colliders, in model space, axis aligned: the wheel and shaft ends, the
    # loaded body, and the handles sticking out behind. Not one box round the lot -
    # that would be a metre and three quarters of invisible wall along the ground
    # between the barrow's grips and the post, which is where a player walks.
    collide((ORIGIN.x, 0.0, ORIGIN.z), (0.58, 0.52, 0.58))

    body = W(0.70, 0.0, 0.30)
    collide((body.x, 0.0, body.z), (0.98, 0.74, 0.66))

    grip = W(1.24, 0.0, 0.01)
    collide((grip.x, 0.0, grip.z), (0.42, 0.42, 0.16))


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


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    clear_scene()
    barrow()
    # Bevel per object, one segment, then join. finish() would bevel with two, which
    # doubles what a chamfer costs for a few pixels at eye distance.
    bevel_all(segments=1)
    obj = finish(NAME, bevel=False)

    colliders = len(COLLIDERS)
    export(obj, NAME, VARIANTS)
    write_col(os.path.join(VARIANTS, NAME + ".col"))

    tris = triangles(obj)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    # Moved after export: the asset stays centred on its own origin. The yaw is the
    # render's, not the model's - a piece is placed at whatever angle the player
    # faces, and dead side on to the camera a barrow reads as a cut-out of a barrow.
    obj.rotation_euler.z = math.radians(-14.0)
    obj.location.x += BESIDE
    post()

    tint()
    stage()
    # The camera stands on +y looking at -y, so -x is on the right of the frame: the
    # barrow lies there with its wheel furthest out. The cube goes two and a half
    # metres back, standing in the gap between the two - which is the only clear
    # ground in the shot. Beside the post it was cut off by the frame and beyond the
    # barrow it stood directly behind the wheel, and the wheel is the whole piece.
    # Note that a cube is placed by its centre but framed by its near face, which is
    # half a metre closer and projects wider: both clipped versions were inside the
    # frame on paper.
    reference_cube((-0.85, -3.50, 0.50))

    # Eye height, 5.2m back, 42mm. Further back than the post's own renders, which
    # stand at 3.5m: the pair is three and a third metres wide lying side by side and
    # 42mm shows three at that distance, so the standard shot crops the wheel off the
    # end - and the wheel is the shape the whole piece rests on.
    camera((-1.05, 5.20, 1.70), (-1.05, -0.30, 0.76), lens=42)
    render(os.path.join(PREVIEWS, NAME + ".png"), 1000, 720)

    icon()

    flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
    print("DESIGN_OK %s tris=%d colliders=%d size=%.2fx%.2fx%.2f%s"
          % (NAME, tris, colliders, max(xs) - min(xs), max(ys) - min(ys),
             max(zs) - min(zs), flag))
    if tris > LIMIT:
        raise SystemExit("the barrow is over %d triangles" % LIMIT)


def icon():
    """
    The crafting post's icon rig: orthographic, front-on, transparent, its own two suns
    and its own exposure, 128px, the model yawed 25 degrees. Into previews/ until this
    is picked - an icon for a piece nobody has chosen is a render, not an asset.
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


main()
