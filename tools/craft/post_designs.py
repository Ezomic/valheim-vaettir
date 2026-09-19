"""
Four designs for the crafting post, the stowing post's sibling.

    blender --background --python tools/craft/post_designs.py

The crafting post houses a spirit that fetches for a workshop: craft at a bench near it
and the panel counts what is in the chests around it, and the spirit flies a trip from
the chest to the bench for show. Nothing is ever put *into* this post, which is the one
thing its shape has to say that the stowing post's does not. The stowing post is a
pigeonhole rack because you put things down there; this one must not read as a chest,
a crate or a shelf, or people will try to open it.

So none of these has a compartment, a lid or a surface at waist height. Each holds a
heartwood up where you can see it - the spirit lives in it, and a post that cost a
heartwood with none showing is the dishonesty post_heartwood.py was written to fix -
and each is built from the stowing post's own palette: the same wood, iron and stone
groups, and the same small pitched roof over the light. That roof is the family
resemblance. The outline is where they part company.

What a workshop spirit lives in, four ways, and four outlines:

    jib     a hoist: one upright and an arm, the heartwood hung off the end    - a gamma
    tripod  three poles lashed at the top, the heartwood hung in the middle   - a triangle
    fork    a sawn-off forked trunk holding the heartwood in its crotch       - a Y, and low
    gantry  two posts and a beam, the heartwood hung from the centre          - a portal

Everything the workshop already has is square and waist-high: the workbench, the forge,
the chests. So every one of these puts its light *above* a bench top or well clear of it,
where it reads across a room full of furniture.

Rendered beside a grey block the size of a vanilla workbench, because this post is only
ever seen next to one. The block is the bench's New/high renderer as ripped on
2026-08-15 - 3.05 wide, 1.85 tall, 1.33 deep - so it is the bench's bounding mass, roof
included, not its table. The runtime puts the model on the piece at scale 1
(PostModel.cs sets localScale to Vector3.one), so these render raw.

Lighting is CLAUDE.md's, not stage_scene's: sun 1.4, fill 0.35, world 0.28. stage_scene
still carries the old 3.2 sun and 0.65 world that lands every albedo on one value, and
changing it there would change every other script's renders under them.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
# The stowing post's heartwood is imported, not copied. Two posts in one mod holding
# the same lump have to hold the *same* lump, and a second copy would be free to drift.
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math
import random

from mathutils import Euler, Quaternion, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     limb, material, orb, ring, taper, tint, write_col, COLLIDERS)

from post_heartwood import GLOW, WOOD_TILT, heartwood

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped, for the family comparison in the lineup.
SIBLING = os.path.join(ASSETS, "stow_post_canopy.obj")

# piece_workbench, New/high, from the devkit rip: Unity (x, y, z) = (3.051, 1.847,
# 1.331), so width, height, depth. Blender wants width, depth, height.
BENCH = (3.05, 1.33, 1.85)

LIMIT = 10000


# --------------------------------------------------------------------------- parts

def pole(a, b, r0, r1, mat, sides=7, tilt=1.5):
    """
    A tapering round from point a to point b.

    taper() only stands things up or lays them over on one axis, and a tripod leg runs
    diagonally in two. Rotation is taken as the shortest turn from +z onto the pole's
    own direction, then jittered like everything else.

    The random spin is composed onto the pole's own axis, as a quaternion, before the
    turn. Added to the euler's z it was applied last, in world space, and swung every
    leg round the vertical - the tripod came out as three poles standing near-parallel
    that never met at the lashing.
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
    obj.data.materials.append(material(mat))
    return obj


def band(radius, z, at=(0.0, 0.0), height=0.035):
    """
    A hoop is one thin cylinder, never a ring of blocks. Whatever it binds sits inside
    it, so only the outer wall is ever seen and the rest would be triangles for nothing.
    """
    return taper(radius, radius, height, (at[0], at[1], z), "iron", sides=13, tilt=1.0)


def hoop(radius, z, at=(0.0, 0.0), thickness=0.016):
    """
    A band with nothing inside it. band() is a capped cylinder, which is right round a
    trunk - the caps are buried in the wood - and wrong round air: the first lanterns
    came out with a solid iron plate top and bottom, and a capped cylinder with nothing
    in it is a lid. Still one piece, a thin torus, never a ring of blocks.
    """
    return ring(radius, thickness, (at[0], at[1], z), "iron", major=13, minor=4,
                rot_x=0.0, tilt=1.0)


def roof(at, width, depth, pitch, board=0.034):
    """
    The stowing post's roof, the one piece of it every variant carries: two boards and
    a ridge. Boards lap at the ridge rather than butting, because two boards meeting on
    a line open a hairline of daylight along it at eye height.
    """
    x, y, z = at
    run = width * 0.5
    for side in (-1, 1):
        box((run + 0.04, depth, board),
            (x + side * run * 0.5 * math.cos(math.radians(pitch)), y,
             z - run * 0.5 * math.sin(math.radians(pitch))),
            "wood", rot_y=side * pitch, tilt=WOOD_TILT)
    box((0.08, depth + 0.03, 0.045), (x, y, z + 0.01), "wood", tilt=WOOD_TILT)


def cage(at, radius=0.155, height=0.40, bars=5):
    """
    Bars and two bands around a hung heartwood: a lantern, open all the way round.

    Bars rather than a shell, because a closed cylinder around a light is a tin can, and
    the spirit has to be seen to be able to leave.
    """
    x, y, z = at
    hoop(radius, z + height * 0.5 - 0.02, (x, y))
    hoop(radius, z - height * 0.5 + 0.02, (x, y))
    for i in range(bars):
        angle = math.radians(360.0 / bars * i + 90.0)
        box((0.022, 0.022, height),
            (x + math.cos(angle) * radius, y + math.sin(angle) * radius, z),
            "iron", tilt=1.0)


def cradle(at, radius=0.085, fingers=3):
    """
    Three iron fingers under the lump and a band to hold them - never a cup. A closed
    cup is a lid, and the lump has to be visible from below as well as above.
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
    A hoist. One upright and an arm, with the heartwood hung off the end in a lantern.

    The only asymmetric outline of the four, and the most obviously a workshop tool:
    this is what lifts things in a yard, and the spirit fetching is the same job. The
    arm reaches over a bench top rather than standing beside it, so the light sits
    where the trip to the bench begins.

    Risk: the arm is 0.9m of timber sticking out at head height, and it wants the post
    turned the right way before placing - an arm pointing at a wall is a stick.
    """
    box((0.56, 0.56, 0.16), (0.0, 0.0, 0.08), "stone", tilt=WOOD_TILT)
    collide((0.0, 0.0, 0.08), (0.56, 0.56, 0.16))

    box((0.17, 0.17, 1.92), (0.0, 0.0, 1.08), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, 1.08), (0.20, 0.20, 1.92))

    # A collar where the post meets the stone, so it stands in it rather than on it.
    box((0.26, 0.26, 0.10), (0.0, 0.0, 0.20), "iron", tilt=1.0)

    box((1.02, 0.13, 0.13), (0.40, 0.0, 1.92), "wood", tilt=WOOD_TILT)

    # The brace is what makes the arm read as carrying something. Without it an arm is
    # a stick nailed to a post.
    box((0.72, 0.09, 0.09), (0.24, 0.0, 1.66), "wood", rot_y=-45.0, tilt=WOOD_TILT)

    box((0.21, 0.21, 0.07), (0.0, 0.0, 1.92), "iron", tilt=1.0)

    taper(0.013, 0.013, 0.18, (0.80, 0.0, 1.78), "iron", sides=5, tilt=1.0)
    cage((0.80, 0.0, 1.46))
    heartwood((0.80, 0.0, 1.46), size=0.95)

    # The lantern's roof, and so the sibling's roof: the heartwood lamp idiom.
    roof((0.80, 0.0, 1.735), 0.40, 0.36, 28.0)

    collide((0.45, 0.0, 1.70), (1.00, 0.40, 0.55))


def tripod():
    """
    Three poles lashed at the top, the heartwood hung in the middle of them.

    A triangle, and the only outline here that is wider at the foot than at the head -
    every other one gets heavier as it rises. Built the way a lifting tripod is built,
    so it is a tool before it is furniture, and it reads the same from every side: the
    one variant that does not care which way it faces.

    Risk: open on all sides, so from across a room it is three sticks and a light, and
    it has the least timber mass to carry the family's wood colour.
    """
    taper(0.80, 0.84, 0.10, (0.0, 0.0, 0.05), "stone", sides=9, tilt=1.0)
    collide((0.0, 0.0, 0.05), (1.60, 1.60, 0.10))

    apex = Vector((0.0, 0.0, 1.86))
    for yaw in (30.0, 150.0, 270.0):
        rad = math.radians(yaw)
        foot = Vector((math.cos(rad) * 0.70, math.sin(rad) * 0.70, 0.08))

        # Run each leg on past the lashing. A tripod whose poles stop at the knot is a
        # cone with the sides missing; poles crossing and carrying on are a tripod.
        head = apex + (apex - foot).normalized() * 0.22
        pole(foot, head, 0.055, 0.040, "wood")

    # The lashing: two turns, not a can. A capped cylinder here sat on the apex like a
    # tin with three sticks through it.
    hoop(0.085, 1.80, thickness=0.024)
    hoop(0.085, 1.86, thickness=0.024)
    collide((0.0, 0.0, 0.95), (0.70, 0.70, 1.80))

    # The lantern hangs low in the legs, not up in the lashing. Up there the poles are
    # a hand's width apart and the roof it first carried came out as a propeller stuck
    # through three sticks. Here the legs stand clear of it and the tripod itself is
    # the roof over the light.
    taper(0.013, 0.013, 0.62, (0.0, 0.0, 1.49), "iron", sides=5, tilt=1.0)
    cage((0.0, 0.0, 1.00), radius=0.150, height=0.38)
    heartwood((0.0, 0.0, 1.00), size=0.95)
    hoop(0.170, 1.18)


def fork():
    """
    A forked trunk, sawn off, with the heartwood held in the crotch of the fork.

    The low one, and the only one that grew rather than was built. A heartwood comes out
    of a tree, and here it is still sitting in one - the spirit's house is a piece of
    the forest brought indoors, bound with iron so it reads as used rather than found.
    Squat on purpose: it sits beside a bench like a chopping block, below the bench's
    roofline, and the light is at chest height where you look while crafting.

    Risk: a stump reads as a stump. The bands and the roof are what say someone keeps
    it, and without them this is a prop.
    """
    taper(0.30, 0.25, 0.72, (0.0, 0.0, 0.36), "bark", sides=9, tilt=1.5)
    collide((0.0, 0.0, 0.36), (0.60, 0.60, 0.72))

    # Roots rather than a flat cut at the ground. A trunk ending in a clean edge is a
    # post; one that flares is something that was growing there.
    for i in range(5):
        yaw = 72.0 * i + 20.0 + random.uniform(-10.0, 10.0)
        rad = math.radians(yaw)
        limb((math.cos(rad) * 0.20, math.sin(rad) * 0.20, 0.16), 0.36, 3,
             0.085, 0.030, 116.0, yaw + 90.0, 14.0, "bark", sides=5)

    # Hoops at 1.07x the trunk's radius there: the trunk is nine-sided and a band
    # sized to its flats is out-sized by its corners, which poke through the iron.
    band(0.300, 0.24, height=0.07)
    band(0.278, 0.56, height=0.07)

    # The fork. limb() pitches towards -y and then yaws, so yaw +90 leans an arm to +x.
    # Each arm starts well down inside the trunk and nearly as thick as it, so the trunk
    # *divides* rather than having two branches stuck on its top. Set on a flat cut, as
    # they first were, and thinner, they read as a pair of raised arms holding the roof
    # up - which made the whole thing a figure.
    for side in (-1, 1):
        limb((side * 0.07, 0.0, 0.52), 0.62, 2, 0.19, 0.14, 34.0,
             side * 90.0, -6.0, "bark", sides=7)
    collide((0.0, 0.0, 1.00), (0.80, 0.40, 0.50))

    heartwood((0.0, 0.0, 0.97), size=1.0)

    # Wider than the fork, so it overhangs the sawn ends and reads as a roof set on
    # the tree rather than a board balanced on two stubs - and low enough that each
    # board sinks into an arm's end. Six centimetres higher it floated.
    roof((0.0, 0.0, 1.20), 1.00, 0.48, 20.0)


def gantry():
    """
    Two posts and a beam, and the heartwood hung from the middle of the beam.

    A portal, the widest of the four, and the one that frames the light rather than
    holding it up: the heartwood hangs in open air with timber all round it. It stands
    beside a bench as a second, empty bench-width frame - the workshop's own proportion
    - and so it is the one that looks most like it belongs in a row of them.

    Risk: posts and a beam is a doorway, and a doorway invites walking through it. The
    footings stand proud and the beam is low for exactly that reason.
    """
    for side in (-1, 1):
        x = side * 0.60
        box((0.30, 0.34, 0.14), (x, 0.0, 0.07), "stone", tilt=WOOD_TILT)
        box((0.15, 0.15, 1.66), (x, 0.0, 0.93), "wood", tilt=WOOD_TILT)
        collide((x, 0.0, 0.88), (0.30, 0.34, 1.76))

        # Knee braces, so the frame stands as joinery rather than as three sticks.
        box((0.44, 0.08, 0.08), (x - side * 0.15, 0.0, 1.57), "wood",
            rot_y=side * 45.0, tilt=WOOD_TILT)

    box((1.62, 0.17, 0.15), (0.0, 0.0, 1.78), "wood", tilt=WOOD_TILT)
    collide((0.0, 0.0, 1.78), (1.62, 0.20, 0.15))

    for side in (-1, 1):
        box((0.22, 0.20, 0.06), (side * 0.60, 0.0, 1.70), "iron", tilt=1.0)

    taper(0.013, 0.013, 0.20, (0.0, 0.0, 1.62), "iron", sides=5, tilt=1.0)
    cradle((0.0, 0.0, 1.36))
    heartwood((0.0, 0.0, 1.42), size=0.95)

    # The roof runs the length of the beam rather than capping the light alone, so the
    # frame reads as one roofed thing and not as a beam with a hat in the middle.
    roof((0.0, 0.0, 2.00), 1.80, 0.40, 14.0)


DESIGNS = (
    ("craft_post_jib", jib),
    ("craft_post_tripod", tripod),
    ("craft_post_fork", fork),
    ("craft_post_gantry", gantry),
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

    # From the other side and lower, so the faces the sun misses are dark rather than
    # black - without it every shaded board is one silhouette.
    bpy.ops.object.light_add(type="SUN", location=(-3, 4, 3))
    fill = bpy.context.active_object
    fill.data.energy = 0.35
    fill.rotation_euler = (math.radians(68), 0, math.radians(140))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.28


def grey(name, size, at, colour):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    obj = bpy.context.active_object
    obj.scale = size
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = colour
    obj.data.materials.append(mat)
    return obj


def reference_cube(at):
    grey("ref", (1.0, 1.0, 1.0), at, (0.52, 0.52, 0.56, 1))


def bench(at):
    """The workbench's measured mass, darker than the cube so the two cannot be confused."""
    grey("bench", BENCH, (at[0], at[1], BENCH[2] * 0.5), (0.34, 0.34, 0.36, 1))


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
    """
    Bevel per object with one segment, then join. finish() bevels with vhbuild's
    default of two, which doubles what a bevel adds for a chamfer that is a few pixels
    at eye distance - so it is asked not to, and the bevel is done here first.
    """
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

        # Into variants/ until one is picked. The csproj copies assets\*.obj one level
        # deep, so a candidate cannot reach the plugin folder by accident.
        export(obj, name, VARIANTS)
        write_col(os.path.join(VARIANTS, name + ".col"))

        tris = triangles(obj)
        zs = [v.co.z for v in obj.data.vertices]
        report.append((name, tris, colliders, max(zs) - min(zs)))

        tint(GLOW)
        stage()
        # The camera stands on +y looking at -y, so +x is on the *left* of the frame:
        # the bench goes there, behind the jib's arm, and the cube on the right.
        reference_cube((-1.05, -1.10, 0.50))
        bench((2.15, -1.00))

        # Eye height, a little over 3m back, a 42mm lens.
        camera((0.40, 3.50, 1.70), (0.40, -0.30, 0.95), lens=42)
        render(os.path.join(PREVIEWS, name + ".png"), 900, 700)

    lineup()
    icons()

    for name, tris, colliders, height in report:
        flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
        print("DESIGN_OK %s tris=%d colliders=%d height=%.3f%s"
              % (name, tris, colliders, height, flag))
    if any(tris > LIMIT for _, tris, _, _ in report):
        raise SystemExit("a crafting post is over %d triangles" % LIMIT)


def lineup():
    """
    All four in a row with the stowing post that shipped at the left end, one bench
    block at the right, and the cube - at the distance you stand at to use one.
    """
    clear_scene()

    spacing = 2.05
    for index, (name, design) in enumerate(DESIGNS):
        offset = (index - 1.0) * spacing
        before = set(bpy.data.objects)
        design()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += offset

    bevel_all(segments=1)
    finish("lineup", bevel=False)

    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=SIBLING, forward_axis="Z", up_axis="Y")
    for obj in bpy.context.selected_objects:
        obj.location.x += -2.0 * spacing - 0.15

    tint(GLOW)
    stage()
    # Left of frame is +x, so the stowing post at -x stands at the right-hand end.
    # The bench clears the gantry's roof by 30cm; any closer and it swallowed a post.
    reference_cube((-2.0 * spacing - 1.40, -0.4, 0.50))
    bench((2.0 * spacing + 2.75, -0.30))

    # Further back than the single renders, so seven things fit: the eye stays at 1.7m,
    # and a wider lens at a longer distance keeps them the same size relative to each
    # other, which is all a lineup is for.
    camera((1.0, 10.5, 1.70), (1.0, 0.0, 0.95), lens=26)
    render(os.path.join(PREVIEWS, "craft_post_lineup.png"), 1800, 620)
    print("DESIGN_OK craft_post_lineup")


def icons():
    """
    post_icon.py's rig, pointed at the variants: orthographic, front-on, transparent,
    its own two suns and its own exposure, 128px, the model yawed 25 degrees.

    Into previews/ rather than beside the model, because none of these is picked and an
    icon for a rejected post is a render, not an asset.
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
        # Height set so the 12 degree tilt aims the ray through the model's centre.
        # post_icon.py uses radius * 0.62, which is the same thing only at a radius of
        # 1m - the fork, at 0.65, came out with its roof cut off the top of the icon.
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
