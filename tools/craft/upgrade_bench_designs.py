"""
Three designs for the bench upgrade: the stowing post's accessory that serves the workshop.

    blender --background --python tools/craft/upgrade_bench_designs.py

Built beside a stowing post, the way a chopping block stands beside a workbench and Kynda's
Tun beside a smelter. With it, crafting at a bench or forge near the post reads the chests
around the post, and the spirit flies over to the bench for show. It costs a heartwood, so
it holds one where you can see it - the rule post_heartwood.py was written for.

tools/craft/post_designs.py drew four full-size crafting posts for this job before it
became an upgrade. They do not survive the change of role as they stand: a 1.9m gantry or
tripod beside a 1.8m post is a rival, not an accessory. What does survive is the jib's
idea - an arm reaching away from the post, towards the workshop, is the trip the spirit
makes - and the fork's, which sat below the bench like a chopping block. The third is new.

The upgrade must not read as a second lantern, because the SPIRIT upgrade is exactly that
and a player has to tell the two apart across a room. So none of these houses its
heartwood under a little roof the way the post does. Each holds it with a *tool* instead:
tongs, a cradle on a chopping block, a cradle on a sawhorse. The tool is what says
"workshop"; the timber, iron, stone and glow are what say "stowing post family".

    jib      a short hoist, its tongs holding the heartwood out away from the post - a gamma
    block    a chopping block with an axe in it and the heartwood set in its top    - a stub
    trestle  a sawhorse with the heartwood cradled on its beam and a hammer by it  - an A

Staged beside the shipped stowing post, imported rather than rebuilt, at the distance a
player would put it. No workbench block: the post is the neighbour this is judged against.
The runtime puts a model on its piece at scale 1, so these render raw.

Lighting is CLAUDE.md's, not stage_scene's: sun 1.4, fill 0.35, world 0.28.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
# The stowing post's heartwood is imported, not copied: an upgrade holding a different
# lump from the post it serves would be a different substance.
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math
import random

from mathutils import Euler, Quaternion, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     ring, taper, tint, write_col, COLLIDERS)

from post_heartwood import GLOW, WOOD_TILT, heartwood

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped. Every render has it in frame, because this piece is
# never seen without one.
SIBLING = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the post and the upgrade stand. The camera is on +y looking at -y, so +x is the
# left of the frame: the post on the left, the upgrade to its right. The post is 1.2m
# wide, so its edge is at x 0.25; the upgrade's origin at -0.60 leaves about half a
# metre of floor between them - close enough to belong, far enough to walk past.
POST_X = 0.85
PIECE_X = -0.60

LIMIT = 10000


# --------------------------------------------------------------------------- parts
#
# pole, hoop and cradle are post_designs.py's, copied because that script runs its whole
# build on import. They are small, and the crafting posts are shelved.

def pole(a, b, r0, r1, mat, sides=7, tilt=1.5):
    """
    A tapering round from point a to point b. The spin is composed onto the pole's own
    axis before the turn; added to the euler's z it swings the pole round the vertical.
    """
    a = Vector(a)
    b = Vector(b)
    axis = b - a

    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1,
                                    depth=axis.length, location=(a + b) * 0.5)
    obj = bpy.context.active_object
    turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized())
    spin = Quaternion((0.0, 0.0, 1.0), math.radians(random.uniform(0.0, 360.0)))
    lean = Euler((math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)), 0.0)).to_quaternion()
    obj.rotation_euler = (lean @ turn @ spin).to_euler()
    obj.data.materials.append(bpy.data.materials.get(mat) or bpy.data.materials.new(mat))
    return obj


def band(radius, z, at=(0.0, 0.0), height=0.04):
    """A hoop round something solid is one thin cylinder; its caps are buried in the wood."""
    return taper(radius, radius, height, (at[0], at[1], z), "iron", sides=13, tilt=1.0)


def hoop(radius, z, at=(0.0, 0.0), thickness=0.016):
    """A hoop round air is a thin torus. A capped cylinder with nothing in it is a lid."""
    return ring(radius, thickness, (at[0], at[1], z), "iron", major=13, minor=4,
                rot_x=0.0, tilt=1.0)


def cradle(at, radius=0.085, fingers=3):
    """
    Three iron fingers under the lump and a hoop to hold them - never a cup, which is a
    lid, and the lump has to be visible from below as well as above.
    """
    x, y, z = at
    for i in range(fingers):
        angle = math.radians(360.0 / fingers * i + 90.0)
        box((0.030, 0.030, 0.17),
            (x + math.cos(angle) * radius, y + math.sin(angle) * radius, z + 0.02),
            "iron", rot_x=math.degrees(math.sin(angle)) * 0.20,
            rot_y=-math.degrees(math.cos(angle)) * 0.20, tilt=1.0)
    hoop(radius + 0.012, z - 0.045, (x, y))


# --------------------------------------------------------------------------- designs

def jib():
    """
    A short hoist whose tongs hold the heartwood out, away from the post.

    The crafting post's jib at 80% of its height and with its lantern taken away. The
    arm points away from the post - towards wherever the workshop is - so the one
    asymmetric outline here also says which way the spirit goes. What hangs off it is a
    pair of smith's tongs gripping the heartwood: a forge tool, carrying the light, and
    nothing like the lantern the SPIRIT upgrade will hang. Tongs rather than a cage is
    the whole difference between "a lamp" and "a thing being fetched".

    Risk: the tallest of the three, at about the height of the post's top rail, and the
    arm wants the piece turned the right way before placing.
    """
    box((0.40, 0.40, 0.12), (0.0, 0.0, 0.06), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.06), (0.40, 0.40, 0.12))

    box((0.13, 0.13, 1.46), (0.0, 0.0, 0.83), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.83), (0.16, 0.16, 1.46))

    # A collar where the upright meets the stone, so it stands in it rather than on it.
    box((0.20, 0.20, 0.08), (0.0, 0.0, 0.15), "iron", tilt=1.0)

    # The arm runs to -x, away from the post at +x.
    box((0.88, 0.11, 0.11), (-0.33, 0.0, 1.50), "wood", tilt=WOOD_TILT)
    box((0.17, 0.17, 0.06), (0.0, 0.0, 1.52), "iron", tilt=1.0)

    # The brace is what makes the arm read as carrying something. rot_y +45 lifts its
    # -x end, so it runs from low on the upright to under the arm.
    box((0.54, 0.075, 0.075), (-0.19, 0.0, 1.30), "wood", rot_y=45.0, tilt=WOOD_TILT)

    # A pulley block at the arm's end, sunk into its underside.
    hx = -0.64
    box((0.11, 0.09, 0.13), (hx, 0.0, 1.41), "wood", tilt=WOOD_TILT)
    taper(0.045, 0.045, 0.11, (hx, 0.0, 1.40), "iron", sides=9, rot_x=90.0, tilt=1.0)

    # The fall: one iron rod, into the block above and through the ring below.
    taper(0.011, 0.011, 0.26, (hx, 0.0, 1.24), "iron", sides=5, tilt=0.5)
    ring(0.042, 0.012, (hx, 0.0, 1.13), "iron", major=11, minor=4, tilt=1.0)

    # The tongs. Two bars crossing at a rivet; the handles go up to a crossbar under the
    # ring, the jaws come down and bite into the lump's sides at its equator. The bars
    # are thicker than tongs would be, because at three metres a 2cm rod is one pixel.
    pivot = 0.985
    lump = pivot - 0.14
    for side in (-1, 1):
        pole((hx + side * 0.085, 0.0, pivot + 0.14), (hx - side * 0.085, 0.0, lump),
             0.019, 0.016, "iron", sides=5, tilt=0.5)
    box((0.21, 0.03, 0.03), (hx, 0.0, pivot + 0.135), "iron", tilt=0.5)
    taper(0.024, 0.024, 0.07, (hx, 0.0, pivot), "iron", sides=7, rot_x=90.0, tilt=0.5)

    heartwood((hx, 0.0, lump), size=0.95)

    collide((-0.40, 0.0, 1.18), (0.72, 0.30, 0.74))


def block():
    """
    A chopping block: a sawn round of timber with an axe in it and the heartwood set in
    its top.

    The fork's idea - squat, below a bench top, the thing that stands beside a workbench
    - without the fork, which grew a figure's raised arms. A chopping block with an axe
    in it is the one object every Valheim player already reads as "the workshop starts
    here", because vanilla puts one beside the workbench. The heartwood sits in a cradle
    at the back of the top where the next log would go.

    Risk: low and solid, so from across a room it is a stump and a glow; the axe handle
    is the only part that breaks the outline.
    """
    taper(0.44, 0.46, 0.08, (0.0, 0.0, 0.04), "stone", sides=9, tilt=1.0)
    collide((0.0, 0.0, 0.04), (0.88, 0.88, 0.08))

    # A round sawn off a trunk: wider than it is tall, and flaring at the foot the way
    # the butt of a log does. The first cut was taller than wide with an iron band top
    # and bottom, and read as a barrel - which is Kynda's Tun, not a workshop. So no
    # bands: the iron here is the axe and the cradle, the things in use.
    top = 0.50
    taper(0.40, 0.335, top - 0.06, (0.0, 0.0, 0.06 + (top - 0.06) * 0.5), "wood",
          sides=9, tilt=WOOD_TILT)
    collide((0.0, 0.0, top * 0.5), (0.74, 0.74, top))

    # The heartwood at the back of the top, in a cradle whose feet bite into the wood.
    lx, ly = 0.09, -0.08
    cradle((lx, ly, top + 0.05), radius=0.080)
    heartwood((lx, ly, top + 0.10), size=0.95)

    # The axe, bitten into the front of the top: the head near upright with its edge in
    # the wood, the handle rising out across the top and past the edge, away from the
    # post. The blade faces the camera. Turned the other way, as it first was, the head
    # was seen end-on as a 4cm sliver and the whole axe read as a pick.
    ax, ay = -0.10, 0.10
    box((0.13, 0.05, 0.22), (ax, ay, top + 0.02), "iron", rot_y=30.0, tilt=1.0)
    eye = Vector((ax + 0.05, ay, top + 0.10))
    reach = Vector((-0.85, 0.20, 0.49)).normalized() * 0.52
    pole(eye, eye + reach, 0.030, 0.026, "wood", sides=5, tilt=0.5)

    collide((lx, ly, top + 0.10), (0.26, 0.26, 0.22))


def trestle():
    """
    A sawhorse with the heartwood cradled in the middle of its beam and a hammer on it.

    The one outline here that is wider than it is tall and open underneath: four splayed
    legs and a beam, which no lantern, stump or post can be mistaken for. A sawhorse is
    the first thing built in any workshop and the thing everything else is built on, and
    the heartwood rides on it the way a workpiece would.

    Risk: the least mass of the three, so the least timber colour to carry the family,
    and a sawhorse is common enough that the glow has to do the work of saying "magic".
    """
    beam_z = 0.64
    box((1.00, 0.14, 0.12), (0.0, 0.0, beam_z), "wood", tilt=WOOD_TILT)

    # Legs splay hard in x as well as y. The first cut splayed them mostly in y, which
    # the camera cannot see, and from the front a straight-legged sawhorse is a table.
    for sx in (-1, 1):
        for sy in (-1, 1):
            pole((sx * 0.33, sy * 0.03, beam_z + 0.04), (sx * 0.52, sy * 0.28, 0.0),
                 0.044, 0.038, "wood", sides=7, tilt=WOOD_TILT)

        # A stretcher across each pair of legs, low down, so a pair reads as an A. No
        # rail along the length between them: with one it was a table again.
        box((0.07, 0.46, 0.06), (sx * 0.47, 0.0, 0.16), "wood", tilt=WOOD_TILT)

        # Iron straps where the legs meet the beam: this is nailed joinery, not lashing.
        box((0.12, 0.19, 0.16), (sx * 0.33, 0.0, beam_z - 0.01), "iron", tilt=1.0)

    collide((0.0, 0.0, beam_z * 0.5 + 0.03), (1.08, 0.60, beam_z + 0.06))

    # The heartwood in the middle of the beam, in the cradle the other two share.
    top = beam_z + 0.06
    cradle((0.0, 0.0, top + 0.05), radius=0.080)
    heartwood((0.0, 0.0, top + 0.10), size=0.95)

    # A hammer lying on the beam towards the post's end. Large on purpose - a hand-sized
    # hammer at three metres is a smudge.
    hx = 0.30
    box((0.34, 0.045, 0.045), (hx, 0.03, top + 0.025), "wood", rot_z=18.0, tilt=0.5)
    box((0.07, 0.16, 0.08), (hx + 0.17, 0.08, top + 0.04), "iron", rot_z=18.0, tilt=0.5)

    collide((0.0, 0.0, top + 0.10), (0.26, 0.26, 0.22))


DESIGNS = (
    ("upgrade_bench_jib", jib),
    ("upgrade_bench_block", block),
    ("upgrade_bench_trestle", trestle),
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


def post(x):
    """The shipped stowing post, as the game will show it: imported, at scale 1."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=SIBLING, forward_axis="Z", up_axis="Y")
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
    """Bevel per object with one segment, then join - finish()'s own bevel would be two."""
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

        # Into variants/ until one is picked: the csproj copies assets\*.obj one level
        # deep, so a candidate cannot reach the plugin folder by accident.
        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tris = triangles(obj)
        zs = [v.co.z for v in obj.data.vertices]
        xs = [v.co.x for v in obj.data.vertices]
        report.append((name, tris, colliders, max(zs) - min(zs), max(xs) - min(xs)))

        obj.location.x += PIECE_X
        post(POST_X)
        tint(GLOW)
        stage()
        # The cube well behind the gap between the two. Off to the upgrade's far side it
        # sat straight behind the jib's hung heartwood at every distance that kept it in
        # frame, and a glow in front of a grey block is a glow with nothing to judge.
        reference_cube((-0.25, -3.00, 0.50))

        # Eye height, 3.7m back, a 42mm lens, the pair framed between them.
        camera((0.0, 3.70, 1.70), (0.0, -0.30, 0.80), lens=42)
        render(os.path.join(PREVIEWS, name + ".png"), 900, 700)

    lineup()
    icons()

    for name, tris, colliders, height, width in report:
        flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
        print("DESIGN_OK %s tris=%d colliders=%d height=%.3f width=%.3f%s"
              % (name, tris, colliders, height, width, flag))
    if any(r[1] > LIMIT for r in report):
        raise SystemExit("a bench upgrade is over %d triangles" % LIMIT)


def lineup():
    """All three in a row, each beside nothing, with one stowing post at the left end."""
    clear_scene()

    spacing = 1.75
    for index, (name, design) in enumerate(DESIGNS):
        offset = -index * spacing - 0.2
        before = set(bpy.data.objects)
        design()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    bevel_all(segments=1)
    finish("lineup", bevel=False)

    post(1.55)
    tint(GLOW)
    stage()
    reference_cube((-2.0 * spacing - 1.45, -0.6, 0.50))

    camera((-1.15, 7.2, 1.70), (-1.15, 0.0, 0.80), lens=30)
    render(os.path.join(PREVIEWS, "upgrade_bench_lineup.png"), 1600, 620)
    print("DESIGN_OK upgrade_bench_lineup")


def icons():
    """
    post_designs.py's icon rig: orthographic, front-on, transparent, its own two suns and
    its own exposure, 128px, the model yawed 25 degrees. Into previews/, since none of
    these is picked and an icon for a rejected piece is a render, not an asset.
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

        # Scene state, not render state: left on, the next render in the same run gets
        # a white void for a sky and reads as a blown exposure.
        scene.render.film_transparent = False
        scene.render.image_settings.color_mode = "RGB"
        print("ICON_OK %s radius=%.2f" % (name, radius))


main()
