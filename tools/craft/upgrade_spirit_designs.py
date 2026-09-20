"""
Three designs for the SPIRIT upgrade: a second home beside a stowing post.

    blender --background --python tools/craft/upgrade_spirit_designs.py

The upgrade houses a second spirit, so two fly at once. It is its own piece, built on the
ground beside a stowing post the way a chopping block stands beside a workbench, and the
station-link motes run from it to the post. So it is only ever seen next to the post, and
every render here has the shipped post in frame beside it.

What it has to say is "a second home, a second lantern". The stowing post's own lantern is
a *house*: three walls, a pitched roof, an iron sill and the heartwood sitting inside,
open to the front (post_canopy_designs.housing). That is the idiom every variant here
repeats - a small copy of the house the first spirit lives in - and it is also what keeps
this apart from the BENCH upgrade, which holds a heartwood too. The bench's idiom is the
workshop's: a lantern hung off an arm or a beam, a tool. A spirit upgrade never hangs its
light; it *houses* it. At a glance: hung light is the bench, housed light is a spirit.

Three outlines, three ways a small spirit-house can stand beside a big one:

    perch   the house up on a single round pole, like a dovecote         - a T, tall and thin
    hut     the house on the ground, wide and low, roof nearly to the stone - a squat gable
    cairn   the house on a heap of big faceted stones                    - a mound with a cap

All three are smaller than the post (1.73m tall, 1.10m wide) and carry its palette: the
same wood, iron, stone and core groups, so at runtime they wear the same borrowed skins.

Lighting is CLAUDE.md's, as in post_designs.py: sun 1.4, fill 0.35, world 0.28, Standard.
The runtime puts a post model at scale 1, so these render raw.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
# The heartwood is imported, not copied: two lumps in one mod have to be the same lump.
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math
import random

from mathutils import Vector

from vhbuild import (bevel_all, box, clear_scene, collide, export, finish, material,
                     orb, taper, tint, write_col, COLLIDERS)

from post_heartwood import GLOW, WOOD_TILT, heartwood

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped: the neighbour in every render.
SIBLING = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the upgrade stands relative to the post. The camera is on +y looking at -y, so
# -x is the right-hand side of the frame. 1.45m centre to centre leaves about 0.6m of
# ground between the post's side and the upgrade - near enough to be read as belonging
# to it, far enough apart that a player could walk round both.
BESIDE = -1.45

LIMIT = 10000


# --------------------------------------------------------------------------- parts

def house(z, width=0.50, depth=0.36, wall=0.30, pitch=20.0, lump=0.80, floor=True):
    """
    The stowing post's lantern house at a smaller size: floor, back wall, two side
    walls, a two-board roof with a ridge, an iron sill across the open front, and the
    heartwood inside. This is the family resemblance; the thing it stands on is the
    design.

    Walls rather than bars, because bars around a light is a lantern hung in a workshop
    - which is the bench upgrade's idiom and exactly what this must not be mistaken for.
    Open at the front, never a door or a plate: the spirit has to be seen to be able to
    leave, and a closed front is a lid.
    """
    top = z + wall

    if floor:
        box((width + 0.04, depth + 0.04, 0.05), (0.0, 0.0, z), "wood", tilt=WOOD_TILT)

    # Back wall on -y. The camera is on +y and so is the player who built it.
    box((width, 0.045, wall + 0.02), (0.0, -depth * 0.5 + 0.02, z + wall * 0.5),
        "wood", tilt=WOOD_TILT)
    for side in (-1, 1):
        box((0.045, depth, wall + 0.02), (side * (width * 0.5 - 0.02), 0.0,
                                          z + wall * 0.5), "wood", tilt=WOOD_TILT)

    # Roof boards overhang the walls by 7cm each way and lap under the ridge, so the
    # two halves meet in timber rather than on a line of daylight.
    run = width * 0.5 + 0.07
    rise = run * math.tan(math.radians(pitch))
    length = run / math.cos(math.radians(pitch)) + 0.04
    for side in (-1, 1):
        box((length, depth + 0.10, 0.034),
            (side * run * 0.5, -0.01, top + rise * 0.5 + 0.005),
            "wood", rot_y=side * pitch, tilt=WOOD_TILT)
    box((0.09, depth + 0.13, 0.042), (0.0, -0.01, top + rise + 0.012), "wood",
        tilt=WOOD_TILT)

    box((width + 0.02, 0.05, 0.045), (0.0, depth * 0.5 - 0.01, z + 0.045), "iron",
        tilt=1.0)

    heartwood((0.0, 0.02, z + 0.035 + 0.115 * lump), size=lump)

    collide((0.0, 0.0, z + (wall + rise) * 0.5), (width + 0.14, depth + 0.10, wall + rise))
    return top + rise + 0.035


def boulder(radius, at, stretch=0.75, squash=(1.0, 1.0)):
    """
    A faceted stone: one subdivision of an icosphere, flattened and turned.

    One subdivision rather than the heartwood's two - a stone that is too round reads as
    a ball, and the flat facets are what say it was split rather than grown. Big ones,
    few of them: a heap of little stones is gravel.
    """
    obj = orb(radius, at, "stone", subdivisions=1, stretch=stretch, tilt=12.0)
    obj.scale = (squash[0], squash[1], stretch)
    obj.rotation_euler.z = math.radians(random.uniform(0.0, 360.0))
    return obj


# --------------------------------------------------------------------------- designs

def perch():
    """
    The house up on a single round pole, like a dovecote: a second home on a stake.

    A T - the only outline of the three that is tall and thin, and the one that puts the
    second light nearest the post's own, so the pair read as two lanterns side by side.
    Kept a clear 30cm under the post's roof so the post stays the master and this the
    accessory. Round and tapered where everything on the post is square: a stake driven
    into the ground, not a piece of joinery.

    Risk: at 1.45m it is the nearest thing to a rival for the post's height, and the
    house on a pole could be taken for a signpost from far enough away.
    """
    box((0.40, 0.40, 0.14), (0.0, 0.0, 0.07), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.07), (0.40, 0.40, 0.14))

    taper(0.080, 0.058, 1.00, (0.0, -0.02, 0.60), "wood", sides=7, tilt=WOOD_TILT)
    collide((0.0, -0.02, 0.60), (0.16, 0.16, 1.00))

    # The post's own iron collar where timber meets stone, and a band under the house.
    taper(0.092, 0.092, 0.06, (0.0, -0.02, 0.19), "iron", sides=13, tilt=1.0)
    taper(0.070, 0.070, 0.05, (0.0, -0.02, 0.98), "iron", sides=13, tilt=1.0)

    # Knee braces from pole to floor, left and right, so the house is carried rather
    # than balanced. A house on a bare pole is a lollipop.
    for side in (-1, 1):
        box((0.30, 0.06, 0.06), (side * 0.11, -0.02, 0.92), "wood",
            rot_y=-side * 42.0, tilt=WOOD_TILT)

    house(1.04, width=0.50, depth=0.38, wall=0.30, pitch=22.0, lump=0.82)


def hut():
    """
    The house on the ground, wide and low, its roof run down nearly to the stone.

    A squat gable at knee height - the only outline that sits *under* the post's
    pigeonholes, and so the most obviously subordinate: a small house at the foot of a
    big one. The steep roof is what makes it a dwelling and not a box on the floor; the
    light shows under the eaves, straight out of the front at the height a spirit
    coming home would land.

    Risk: at 0.8m it is behind anything standing in front of it, and low enough that a
    player may read it as a kennel.
    """
    for x in (-0.23, 0.23):
        box((0.40, 0.62, 0.14), (x, 0.0, 0.07), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.07), (0.86, 0.62, 0.14))

    # A stone hearth inside for the lump to sit on, so it is bedded, not dropped.
    box((0.26, 0.24, 0.07), (0.0, 0.02, 0.17), "stone", tilt=WOOD_TILT)

    house(0.15, width=0.66, depth=0.50, wall=0.26, pitch=34.0, lump=0.92)

    # Corner posts standing proud of the walls at the front, so the opening has jambs
    # and the roof has something to rest its front edge on.
    for side in (-1, 1):
        taper(0.034, 0.028, 0.34, (side * 0.31, 0.24, 0.30), "wood", sides=7,
              tilt=WOOD_TILT)


def cairn():
    """
    The house on a heap of big split stones: a spirit's home built up out of the ground.

    A mound with a small roof on it - the only outline of the three that is rounded,
    wider at the foot than at the head and with no timber below waist height. The
    stowing post is carpentry and so is every bench in the workshop; a heap of stones
    is the one thing next to them that says the forest put it there. The house is the
    smallest of the three and sits in the top of the heap, stones biting into its sides.

    Risk: stone mass takes the eye, and the heap could read as rubble with a birdhouse
    on it if the house is too small.
    """
    boulder(0.40, (0.0, -0.02, 0.20), stretch=0.62, squash=(1.20, 0.95))
    boulder(0.27, (0.30, 0.10, 0.17), stretch=0.70, squash=(1.0, 0.9))
    boulder(0.26, (-0.31, 0.02, 0.18), stretch=0.72, squash=(1.0, 1.0))
    # The capstone: wide and flat, so the house sits on it as on a seat. Round and
    # small, as it first was, it read as a ball the house was balanced on.
    boulder(0.34, (0.03, -0.06, 0.50), stretch=0.48, squash=(1.10, 1.00))
    collide((0.0, 0.0, 0.33), (1.00, 0.70, 0.66))

    # Iron straps down the front of the capstone from the house's floor, so the house
    # is tied to the heap. An iron hoop laid round the top course was tried first and
    # photographed as a ring floating between the stones and the floor.
    for side in (-1, 1):
        box((0.045, 0.03, 0.26), (side * 0.17, 0.19, 0.58), "iron",
            rot_x=-18.0, tilt=1.0)

    house(0.64, width=0.46, depth=0.36, wall=0.28, pitch=24.0, lump=0.80)


DESIGNS = (
    ("upgrade_spirit_perch", perch),
    ("upgrade_spirit_hut", hut),
    ("upgrade_spirit_cairn", cairn),
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


def sibling(x=0.0):
    """The shipped stowing post, imported with the export's own two axis settings."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=SIBLING, forward_axis="Z", up_axis="Y")
    for obj in bpy.context.selected_objects:
        obj.location.x += x


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
    """Bevel per object with one segment, then join - finish() would bevel with two."""
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

        # Into variants/ until one is picked; the csproj copies assets\*.obj one level
        # deep only, so a candidate cannot reach the plugin folder by accident.
        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tris = triangles(obj)
        zs = [v.co.z for v in obj.data.vertices]
        xs = [v.co.x for v in obj.data.vertices]
        report.append((name, tris, colliders, max(zs) - min(zs), max(xs) - min(xs)))

        # Moved beside the post only after export, so the .obj stays centred on its
        # own origin the way the runtime expects.
        obj.location.x = BESIDE
        sibling()

        tint(GLOW)
        stage()
        # +x is the left of the frame: cube there, behind the post's shoulder.
        # Set back, so it shows past the post's shoulder rather than hiding behind it.
        reference_cube((1.15, -1.45, 0.50))

        # Eye height 1.7m, about 3.6m back, 42mm - never an orbit. Aimed between the two
        # so the post and its upgrade are judged together, the way they are built.
        camera((-0.60, 3.60, 1.70), (-0.60, -0.30, 0.78), lens=42)
        render(os.path.join(PREVIEWS, name + ".png"), 900, 700)

    lineup()
    icons()

    for name, tris, colliders, height, width in report:
        flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
        print("DESIGN_OK %s tris=%d colliders=%d height=%.3f width=%.3f%s"
              % (name, tris, colliders, height, width, flag))
    if any(r[1] > LIMIT for r in report):
        raise SystemExit("a spirit upgrade is over %d triangles" % LIMIT)


def lineup():
    """The three side by side with the post at the left end, so the outlines compare."""
    clear_scene()

    spacing = 1.55
    for index, (name, design) in enumerate(DESIGNS):
        offset = -(index + 1) * spacing
        before = set(bpy.data.objects)
        design()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    bevel_all(segments=1)
    finish("lineup", bevel=False)
    sibling()

    tint(GLOW)
    stage()
    reference_cube((1.35, -0.6, 0.50))

    camera((-2.1, 7.2, 1.70), (-2.1, 0.0, 0.80), lens=30)
    render(os.path.join(PREVIEWS, "upgrade_spirit_lineup.png"), 1600, 620)
    print("DESIGN_OK upgrade_spirit_lineup")


def icons():
    """
    post_designs.py's icon rig: orthographic, front-on, transparent, its own two suns and
    its own exposure, 128px, the model yawed 25 degrees. Into previews/, because an icon
    for an unpicked variant is a render, not an asset.
    """
    for name, _ in DESIGNS:
        clear_scene()
        bpy.ops.wm.obj_import(filepath=os.path.join(VARIANTS, name + ".obj"),
                              forward_axis="Z", up_axis="Y")
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for obj in imported:
            # Added to, never assigned: the import carries its axis conversion on this.
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

        # Scene state, not render state: left on, the next render in the same run gets a
        # white void for a sky and reads as a blown exposure.
        scene.render.film_transparent = False
        scene.render.image_settings.color_mode = "RGB"
        print("ICON_OK %s radius=%.2f" % (name, radius))


main()
