"""
Three designs for GROW, the stowing post's capacity upgrade.

    blender --background --python tools/craft/upgrade_grow_designs.py

GROW is its own piece, built on the ground beside a stowing post the way a chopping
block stands beside a workbench: the post holds more (6x2 becomes 8x3) and the spirit
carries twice as much a trip. It is made of ordinary materials and houses no spirit,
so it carries **no heartwood** - the two upgrades that cost one show it, and this one
showing nothing is how a player tells at a glance that it is the other kind.

What it has to say is "more room". So each of these is a piece of storage joinery,
and each is subordinate to the post: lower than its top rail, narrower than it, and
built from its groups - the same wood, the same iron strap across an open front, the
same stone underfoot. Family by palette and idiom; three outlines of its own:

    leanto   a lean-to store: two big bays under a single-pitch roof  - a wedge, ◺
    hopper   a boarded hopper on a splayed stand, open at the top     - a funnel, ▽
    tiers    an A-frame rack whose shelves narrow as they rise        - a step pyramid, △

None of them is a small copy of the post. The post is six pigeonholes, tall and flat;
the lean-to has four and they are twice the size, and the other two are not racks of
compartments at all. A GROW that read as a second post would promise a second post.

Every render stages the shipped stowing post (assets/stow_post_canopy.obj) with the
piece built beside it at a player's distance, because that is the only place this is
ever seen. The runtime puts a model on its piece at scale 1, so these render raw.

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

from mathutils import Euler, Quaternion, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     material, taper, tint, write_col, COLLIDERS)

from post_heartwood import GLOW, WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped: the neighbour in every render.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the piece stands relative to the post. The post is 1.10 wide, so its side is at
# x -0.55; at -1.50 the piece's centre is 1.5m off the post's and there is half a metre
# of floor between them - room to walk round, close enough that the link motes are short.
BESIDE = -1.50

LIMIT = 10000


# --------------------------------------------------------------------------- parts

def slab(corners, thick, mat, tilt=WOOD_TILT):
    """
    A board of any four-cornered outline, `thick` through, centred on the face given.

    box() can only make rectangles, and a hopper wall and a lean-to's end are
    trapezoids. A rectangle standing in for one either pokes its corners out past the
    neighbouring wall or leaves a wedge of daylight, and both read from eye height.
    Built as a convex hull and then dissolved back to quads, so it bevels like a box.
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

    Square, not round, because everything on the post is sawn: a round leg on this
    piece would be the one thing in the pair that was not made by the same hands.
    The turn is the shortest one from +z onto the timber, so a leg that leans only in
    one plane is not twisted about its own length.
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


def strap(length, at, rot_y=0.0):
    """The post's iron strap across an open front - the one idiom every variant keeps."""
    return box((length, 0.06, 0.05), at, "iron", rot_y=rot_y, tilt=WOOD_TILT)


# --------------------------------------------------------------------------- designs

def leanto():
    """
    A lean-to store: two bays over two, under one pitch of roof falling away from the
    post.

    The closest of the three to the post, on purpose - the same stone plinth, the same
    back board, the same straps - and so the one that says "an added bay" most plainly:
    it is the post's own storage, built on at the side. Four compartments to the post's
    six, and each twice the size, so it reads as more room rather than a second post.
    The roof is single-pitched, high side towards the post, which is what a lean-to is
    and what gives it the wedge.

    Risk: it is the most likely of the three to be taken for a thing you can open. It
    cannot, and the hover text has to carry that.
    """
    def eave(x):
        # The roofline: 1.10 at the post side, 0.78 at the far side.
        return 0.94 + 0.3636 * x

    box((0.96, 0.54, 0.12), (0.0, 0.0, 0.06), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.06), (0.96, 0.54, 0.12))

    for x in (-0.41, 0.41):
        top = eave(x) + 0.02
        box((0.09, 0.46, top - 0.10), (x, 0.0, (top + 0.10) * 0.5), "wood",
            tilt=WOOD_TILT)

    # Back board on -y, following the roofline. The camera stands on +y, and bays shot
    # from behind are a crate.
    slab(((-0.45, -0.20, 0.10), (0.45, -0.20, 0.10),
          (0.45, -0.20, eave(0.45) + 0.02), (-0.45, -0.20, eave(-0.45) + 0.02)),
         0.07, "wood")

    box((0.84, 0.44, 0.06), (0.0, 0.02, 0.50), "wood", tilt=WOOD_TILT)
    box((0.07, 0.42, eave(0.0) - 0.10), (0.0, 0.02, (eave(0.0) + 0.10) * 0.5),
        "wood", tilt=WOOD_TILT)

    # Straps at the plinth and across the shelf front, as on the post.
    strap(0.94, (0.0, 0.24, 0.50))
    box((0.92, 0.52, 0.05), (0.0, 0.0, 0.14), "iron", tilt=WOOD_TILT)

    # The roof: one board, overhanging front and far end, and a batten along its high
    # edge so the top reads as a roof finished off rather than a plank laid on.
    pitch = math.degrees(math.atan(0.3636))
    box((1.22, 0.64, 0.04), (-0.03, 0.03, eave(-0.03) + 0.045), "wood",
        rot_y=-pitch, tilt=WOOD_TILT)
    box((0.08, 0.66, 0.06), (0.52, 0.03, eave(0.52) + 0.07), "wood", tilt=WOOD_TILT)

    collide((0.0, 0.0, 0.47), (0.92, 0.50, 0.74))
    collide((0.20, 0.03, 0.97), (0.62, 0.64, 0.30))


def hopper():
    """
    A boarded hopper on a splayed stand, open at the top.

    The one that says "pour more in": wide at the mouth, narrow at the throat, and the
    only outline of the three that is heaviest at the top. Four trapezoid walls rather
    than a shell, because a single-sided surface is invisible from inside in Unity and
    you look straight down into this from standing. Iron at the rim and down the
    corners, which is what keeps a hopper's boards from springing.

    Risk: a funnel on legs is also a feeding trough, and nothing on it is a
    compartment - it says capacity, but not the post's kind of storage.
    """
    rim = 1.08
    floor = 0.56
    top_x, top_y = 0.42, 0.30
    low_x, low_y = 0.17, 0.12

    # Front and back walls run full width and the sides sit between them, so each
    # corner overlaps rather than meeting on a line.
    for side in (-1, 1):
        slab(((-top_x - 0.02, side * top_y, rim), (top_x + 0.02, side * top_y, rim),
              (low_x + 0.02, side * low_y, floor), (-low_x - 0.02, side * low_y, floor)),
             0.045, "wood")
        slab(((side * top_x, -top_y, rim), (side * top_x, top_y, rim),
              (side * low_x, low_y, floor), (side * low_x, -low_y, floor)),
             0.045, "wood")

    box((0.38, 0.28, 0.05), (0.0, 0.0, floor + 0.01), "wood", tilt=WOOD_TILT)

    # Rim: four straps laid on the wall tops.
    for side in (-1, 1):
        box((top_x * 2.0 + 0.10, 0.06, 0.045), (0.0, side * top_y, rim + 0.01), "iron",
            tilt=1.0)
        box((0.06, top_y * 2.0 + 0.06, 0.045), (side * top_x, 0.0, rim + 0.01), "iron",
            tilt=1.0)

    # Corner irons down the arrises, on the outside so they stand proud of the boards.
    for sx in (-1, 1):
        for sy in (-1, 1):
            beam((sx * (top_x + 0.01), sy * (top_y + 0.01), rim),
                 (sx * (low_x + 0.01), sy * (low_y + 0.01), floor), 0.05, "iron",
                 tilt=1.0)

    # A collar under the throat for the legs to stand into.
    box((0.44, 0.34, 0.10), (0.0, 0.0, floor - 0.03), "wood", tilt=WOOD_TILT)

    # The stand splays, or a wide mouth on four upright sticks looks ready to fall.
    for sx in (-1, 1):
        for sy in (-1, 1):
            foot = (sx * 0.27, sy * 0.21, 0.04)
            beam(foot, (sx * 0.17, sy * 0.12, floor + 0.02), 0.08, "wood", over=0.02)
            box((0.15, 0.15, 0.08), foot, "stone", tilt=2.0)

    # Rails between the legs, low, front-back and side-side.
    for side in (-1, 1):
        box((0.52, 0.06, 0.06), (0.0, side * 0.19, 0.24), "wood", tilt=WOOD_TILT)
        box((0.06, 0.40, 0.06), (side * 0.25, 0.0, 0.24), "wood", tilt=WOOD_TILT)

    collide((0.0, 0.0, 0.30), (0.56, 0.46, 0.56))
    collide((0.0, 0.0, 0.82), (0.86, 0.62, 0.54))


def tiers():
    """
    An A-frame shelf, the A turned to face you, with three shelves that narrow as they
    rise.

    A side rack. Widest at the floor and closing to a point, so its outline is the
    opposite of the hopper's and nothing like the lean-to's. The sides are boards the
    full depth of the shelves and it is boarded at the back like the post, so it is a
    piece of furniture: built first as two open A-frames of timber, it read as a step
    ladder - rungs between two pairs of legs - and nothing about a ladder says storage.
    Each shelf carries the post's iron strap along its front edge.

    Risk: the top shelf is a hand's width. The capacity is in the bottom two, and the
    point is the outline more than the shelf space.
    """
    apex = 1.18
    foot_x = 0.48
    top_x = 0.07

    def inner(height):
        # The panels' inner faces at a given height.
        return foot_x - (foot_x - top_x) * (height / apex) - 0.03

    for side in (-1, 1):
        # A side board the full depth of the rack, leaning in to the ridge.
        beam((side * foot_x, 0.0, 0.03), (side * top_x, 0.0, apex), 0.05, "wood",
             depth=0.64)

        # A timber along its front edge, standing proud, so the A reads from in front
        # as well as from an angle - seen square on, a board is its own 5cm edge.
        beam((side * (foot_x + 0.01), 0.32, 0.03), (side * (top_x + 0.01), 0.32, apex),
             0.10, "wood", depth=0.07)

        box((0.20, 0.72, 0.08), (side * foot_x, 0.0, 0.04), "stone", tilt=2.0)

    # The back is boarded, as the post's pigeonholes are, and on -y for the same reason:
    # the camera stands on +y.
    # Its edges run into the side boards: cut to their inner faces, jitter opened a
    # hairline of daylight down both sides between the shelves.
    slab(((-inner(0.06) - 0.035, -0.29, 0.06), (inner(0.06) + 0.035, -0.29, 0.06),
          (inner(apex - 0.06) + 0.035, -0.29, apex - 0.06),
          (-inner(apex - 0.06) - 0.035, -0.29, apex - 0.06)), 0.05, "wood")

    # A ridge board capping both side boards where they meet, and an iron strap on it.
    box((0.26, 0.72, 0.09), (0.0, 0.0, apex + 0.02), "wood", tilt=WOOD_TILT)
    strap(0.28, (0.0, 0.36, apex + 0.02))

    for height in (0.28, 0.60, 0.90):
        # Each shelf runs a few centimetres into the side boards, so it is seen to be
        # housed in them rather than floating between.
        half = inner(height) + 0.035
        box((half * 2.0, 0.62, 0.055), (0.0, 0.01, height), "wood", tilt=WOOD_TILT)
        strap(half * 2.0 - 0.02, (0.0, 0.31, height + 0.025))

    collide((0.0, 0.0, 0.32), (0.98, 0.70, 0.64))
    collide((0.0, 0.0, 0.92), (0.50, 0.70, 0.56))


DESIGNS = (
    ("upgrade_grow_leanto", leanto),
    ("upgrade_grow_hopper", hopper),
    ("upgrade_grow_tiers", tiers),
)


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


def build(name, design):
    """Bevel per object, one segment, then join - finish() would bevel with two."""
    clear_scene()
    design()
    bevel_all(segments=1)
    return finish(name, bevel=False)


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    report = []
    for name, design in DESIGNS:
        obj = build(name, design)
        colliders = len(COLLIDERS)

        # Into variants/ until one is picked, where the csproj cannot copy it.
        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tris = triangles(obj)
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        report.append((name, tris, colliders, max(xs) - min(xs), max(ys) - min(ys),
                       max(zs) - min(zs)))

        # Moved after export: the asset stays centred on its own origin.
        obj.location.x += BESIDE
        post()

        tint(GLOW)
        stage()
        # The camera stands on +y looking at -y, so -x is on the right of the frame:
        # the piece is there, and the cube stands behind it and further out, where it
        # can be read against the piece without hiding it.
        reference_cube((BESIDE - 0.62, -1.40, 0.50))

        # Eye height, 3.6m back, 42mm, aimed between the post and the piece.
        camera((-0.72, 3.60, 1.70), (-0.72, -0.30, 0.80), lens=42)
        render(os.path.join(PREVIEWS, name + ".png"), 900, 700)

    lineup()
    icons()

    for name, tris, colliders, w, d, h in report:
        flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
        print("DESIGN_OK %s tris=%d colliders=%d size=%.2fx%.2fx%.2f%s"
              % (name, tris, colliders, w, d, h, flag))
    if any(r[1] > LIMIT for r in report):
        raise SystemExit("a GROW upgrade is over %d triangles" % LIMIT)


def lineup():
    """The three side by side, the shipped post at the left end, and the cube."""
    clear_scene()

    spacing = 1.45
    for index, (name, design) in enumerate(DESIGNS):
        offset = -(index + 1) * spacing
        before = set(bpy.data.objects)
        design()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    bevel_all(segments=1)
    finish("lineup", bevel=False)

    post(0.15)

    tint(GLOW)
    stage()
    reference_cube((-4 * spacing - 0.10, -0.40, 0.50))

    camera((-2.55, 7.4, 1.70), (-2.55, 0.0, 0.75), lens=32)
    render(os.path.join(PREVIEWS, "upgrade_grow_lineup.png"), 1600, 620)
    print("DESIGN_OK upgrade_grow_lineup")


def icons():
    """
    The crafting post's icon rig: orthographic, front-on, transparent, its own two suns
    and its own exposure, 128px, the model yawed 25 degrees. Into previews/ until one
    is picked - an icon for a rejected piece is a render, not an asset.
    """
    for name, _ in DESIGNS:
        clear_scene()
        bpy.ops.wm.obj_import(filepath=os.path.join(VARIANTS, name + ".obj"),
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
        bpy.ops.object.camera_add(
            location=(0.0, 2.93, 2.93 * math.tan(math.radians(12.0))))
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
        scene.render.filepath = os.path.join(PREVIEWS, name + "_icon.png")
        bpy.ops.render.render(write_still=True)

        # Scene state, not render state: left on, the next preview in the same run gets
        # a white void for a sky and reads as a blown exposure.
        scene.render.film_transparent = False
        scene.render.image_settings.color_mode = "RGB"
        print("ICON_OK %s radius=%.2f" % (name, radius))


main()
