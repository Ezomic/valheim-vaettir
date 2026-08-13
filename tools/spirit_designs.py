"""
Four forest spirits, built and rendered for comparison.

    blender --background --python tools/spirit_designs.py

The first attempt is the thing being argued with. It was symmetrical, upright, evenly
limbed and topped with a small featureless head - which is the shape of a wooden
mannequin, and a mannequin with a lamp in its chest is what it read as. Every design
here breaks at least two of those four.

What a forest spirit has to say that a greydwarf does not: it is not an animal, it
grew, and it is older than you. So none of these are built to a human plan - the mass
sits wrong on purpose, and three of the four have no proper head at all.

Same rules as everything else here:

  * every part must overlap its neighbour - a 5cm gap reads as a detached stick
  * few large parts beat many small ones - small parts read as rubble, not detail
  * a solid mass around the core hides the core; a cage does not
  * the preview camera stands on +y, so anything meant to be behind goes on -y
"""

import bpy
import math
import os
import random

from mathutils import Euler, Vector

# Jitter is seeded, not arbitrary. A rebuild has to produce the same mesh or the
# .obj churns in git and no render can be compared with the one before it.
SEED = 20260814

# How far anything may wander. Small on purpose: this is meant to remove the
# machined look, not to make the creature drunk.
TILT = 4.0
SHIFT = 0.008

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

COLLIDERS = []

TINTS = {
    "bark":  (0.24, 0.17, 0.11, 1.0),
    "moss":  (0.20, 0.28, 0.14, 1.0),
    "core":  (0.62, 0.78, 0.42, 1.0),
    "stone": (0.44, 0.43, 0.40, 1.0),
}


# --------------------------------------------------------------------------- helpers

def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects,
                  bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            if item.users == 0:
                block.remove(item)
    del COLLIDERS[:]
    random.seed(SEED)


def material(name):
    mat = bpy.data.materials.get(name)
    return mat if mat else bpy.data.materials.new(name)


def wobble(tilt=TILT, shift=SHIFT):
    """
    A few degrees and a few millimetres of nothing-in-particular.

    Every part being exactly axis-aligned is most of why the first models read as
    machined. Nothing in a forest is square to anything else, and the eye picks that
    up long before it can say why - so every part gets a little of this, and parts
    that are meant to be carpentry get less rather than none.
    """
    return ((random.uniform(-tilt, tilt), random.uniform(-tilt, tilt),
             random.uniform(-tilt, tilt)),
            (random.uniform(-shift, shift), random.uniform(-shift, shift),
             random.uniform(-shift, shift)))


def box(size, location, mat, rot_x=0.0, rot_y=0.0, rot_z=0.0, tilt=TILT):
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    bpy.ops.mesh.primitive_cube_add(
        size=1.0, location=(location[0] + dx, location[1] + dy, location[2] + dz))
    obj = bpy.context.active_object
    obj.scale = (size[0], size[1], size[2])
    obj.rotation_euler = (math.radians(rot_x + jx), math.radians(rot_y + jy),
                          math.radians(rot_z + jz))
    obj.data.materials.append(material(mat))
    return obj


def taper(bottom, top, height, location, mat, sides=6, rot_x=0.0, rot_y=0.0, tilt=TILT):
    (jx, jy, jz), (dx, dy, dz) = wobble(tilt)

    # Odd side counts by default. An even-sided cylinder presents a flat face square
    # to the camera and reads as a box with the corners knocked off; an odd one
    # always shows an edge, which is what makes it read as round.
    bpy.ops.mesh.primitive_cone_add(
        vertices=sides, radius1=bottom, radius2=top, depth=height,
        location=(location[0] + dx, location[1] + dy, location[2] + dz),
        rotation=(0.0, 0.0, math.radians(random.uniform(0.0, 360.0))))
    obj = bpy.context.active_object
    obj.rotation_euler = (math.radians(rot_x + jx), math.radians(rot_y + jy),
                          obj.rotation_euler.z + math.radians(jz))
    obj.data.materials.append(material(mat))
    return obj


def limb(base, length, segments, thick, taper_to, pitch, yaw, curve, mat, sides=5):
    """
    A bent, tapering branch built from overlapping segments.

    One rotated box is a stick, and a creature made of sticks is the thing being
    argued with. A limb that narrows along its length and changes direction as it
    goes is the single biggest difference between this and a scarecrow - and it costs
    three cones instead of one box.

    Segments overlap by 8% of their length, because a butt joint between two cones
    at different angles leaves a visible wedge of daylight on the outside of a bend.
    """
    pos = Vector(base)
    step = length / float(segments)
    angle = pitch

    for i in range(segments):
        t0 = i / float(segments)
        t1 = (i + 1) / float(segments)
        r0 = thick + (taper_to - thick) * t0
        r1 = thick + (taper_to - thick) * t1

        euler = Euler((math.radians(angle + random.uniform(-2.0, 2.0)), 0.0,
                       math.radians(yaw + random.uniform(-3.0, 3.0))), "XYZ")
        heading = Vector((0.0, 0.0, 1.0))
        heading.rotate(euler)

        centre = pos + heading * (step / 2.0)

        bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1,
                                        depth=step, location=centre)
        obj = bpy.context.active_object
        obj.rotation_euler = euler
        obj.data.materials.append(material(mat))

        pos = pos + heading * step * 0.92
        angle += curve

    return pos


def shell(bottom, top, height, location, mat, sides=8, rot_x=0.0):
    """An open frustum: both caps deleted, so the core inside can be seen."""
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top,
                                    depth=height, location=location)
    obj = bpy.context.active_object
    obj.rotation_euler = (math.radians(rot_x), 0.0, 0.0)

    mesh = obj.data
    bpy.ops.object.mode_set(mode="EDIT")
    # Face mode first, and before anything is selected. In vertex mode every vertex
    # of a frustum belongs to one cap or the other, so selecting both caps selects
    # the whole mesh and the shell vanishes.
    bpy.ops.mesh.select_mode(type="FACE")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")

    for face in mesh.polygons:
        if abs(face.normal.z) > 0.9:
            face.select = True

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="FACE")
    bpy.ops.object.mode_set(mode="OBJECT")

    obj.data.materials.append(material(mat))
    return obj


def ribcage(centre_z, height, radius, staves=5, stave=0.055):
    """
    Core in a cage. The one shape every version of this creature keeps.

    The core is a tapered mass rather than a block, and smaller than the cage rather
    than filling it - a bright rectangle between two bars is a lamp, and the creature
    kept reading as a lantern on legs for exactly that reason. Light should be found
    inside it, not mounted on the front of it.

    Ribs are limbs so they bow outwards over their length. Straight vertical bars are
    the most cage-like thing available, and a cage is not what this is.
    """
    taper(radius * 1.02, radius * 0.66, height * 0.74, (0.0, 0.0, centre_z), "core",
          sides=7, tilt=1.5)

    # Ribs bow out only slightly. The first bowed version swung them clear of the
    # core and left it a bright sliver floating in a gap, which is the lantern
    # reading again by another route - they have to sit close enough to be ribs.
    for i in range(staves):
        yaw = 360.0 / staves * i + 18.0
        rad = math.radians(yaw)
        limb((math.cos(rad) * radius * 0.96, math.sin(rad) * radius * 0.96,
              centre_z - height / 2.0),
             height, 3, stave, stave * 0.88, -5.0, yaw, 5.0, "bark", sides=5)


def roots(at, spread, count=5, length=0.34, thick=0.075):
    """
    Root feet instead of a foot.

    A block on the end of a leg is a boot, and a boot is the single most human thing
    a silhouette can have. Roots cost a few extra parts and remove the reading.

    Each root is a limb rather than a tilted box, so it tapers to a point and bends
    outward as it reaches the ground - a straight taper still reads as a spike.
    """
    x, y, z = at
    for i in range(count):
        yaw = 360.0 / count * i + random.uniform(-14.0, 14.0)
        rad = math.radians(yaw)

        limb((x + math.cos(rad) * spread * 0.5, y + math.sin(rad) * spread * 0.5, z),
             length, 3, thick, thick * 0.35, 118.0, yaw, 16.0, "bark", sides=5)


# --------------------------------------------------------------------------- designs

def warden():
    """
    Heavy above, short below. Broad mossed shoulders, arms hanging past the knee, and
    the head sunk between them rather than sitting on top.

    The mannequin problem was even limbs on a straight spine. This puts nearly all the
    mass in the top third and leaves the legs stumpy, so it reads as something that
    stands rather than something that walks.
    """
    # Legs bow outward slightly and taper, and the two differ - a matched pair reads
    # as manufactured however good each one is on its own.
    for x, bow in ((-0.20, -7.0), (0.21, 6.0)):
        roots((x, 0.0, 0.14), 0.15)
        limb((x, 0.0, 0.10), 0.58, 3, 0.125, 0.095, bow, 90.0 if x > 0 else -90.0,
             -bow * 0.5, "bark")

    taper(0.30, 0.26, 0.20, (0.0, 0.0, 0.74), "bark", sides=7)
    ribcage(1.06, 0.56, 0.22, staves=5)

    taper(0.26, 0.34, 0.24, (0.0, 0.0, 1.42), "bark", sides=7)

    # A mantle, not a parasol. Fixing the flat-disc version overshot: at 0.48 it was
    # wider than the shoulders carrying it and overhung the whole ribcage, so the
    # creature read as a mushroom. It has to sit *on* the shoulders and stop there.
    taper(0.34, 0.27, 0.20, (0.0, 0.0, 1.58), "moss", sides=9)
    taper(0.37, 0.32, 0.09, (0.0, 0.0, 1.50), "moss", sides=9)

    # Arms hang. Curve at 16 degrees a segment over four segments bent them 48
    # degrees in total, which swept them out sideways and read as tusks; near-vertical
    # with a slight inward drift is a hanging arm.
    for x, yaw, curve in ((-0.40, -90.0, -5.0), (0.41, 90.0, 4.0)):
        limb((x, 0.04, 1.46), 1.00, 4, 0.085, 0.052, 175.0, yaw, curve, "bark")

    taper(0.16, 0.13, 0.22, (0.0, -0.02, 1.72), "bark", sides=7)
    box((0.12, 0.04, 0.05), (0.0, -0.13, 1.74), "core", tilt=1.5)

    # Antlers as forked limbs. Deliberately not mirrored: real antlers never are, and
    # a symmetrical crown was doing more than anything else to make the head read as
    # a manufactured part sitting on top of a manufactured body.
    for x, yaw, spread in ((-0.11, -74.0, 20.0), (0.12, 82.0, 26.0)):
        tip = limb((x, 0.0, 1.80), 0.46, 3, 0.055, 0.035, spread, yaw, 9.0, "bark")
        limb(tip, 0.30, 2, 0.036, 0.018, spread + 34.0, yaw + 16.0, 12.0, "bark", sides=4)
        limb(tip, 0.22, 2, 0.030, 0.015, spread - 26.0, yaw - 22.0, 10.0, "bark", sides=4)


def stag():
    """
    Hunched and leaning forward, on a long neck, under a wide antler sweep.

    The only one built on an animal plan rather than a person's. Nothing about this
    silhouette can be mistaken for a wooden man, which is its whole argument - but it
    is also the least like the thing that came out of the pod.
    """
    for y, lean in ((-0.26, -16), (0.30, 20)):
        for x in (-0.19, 0.19):
            roots((x, y, 0.16), 0.12, count=4)
            taper(0.12, 0.09, 0.62, (x, y, 0.44), "bark", rot_x=lean)

    taper(0.28, 0.24, 0.74, (0.0, 0.02, 1.02), "bark", rot_x=64)
    ribcage(1.06, 0.46, 0.20, staves=5)

    taper(0.20, 0.13, 0.62, (0.0, -0.36, 1.42), "bark", rot_x=42)
    taper(0.15, 0.11, 0.26, (0.0, -0.60, 1.66), "bark", rot_x=24)
    box((0.12, 0.05, 0.05), (0.0, -0.70, 1.68), "core")

    for x in (-0.13, 0.13):
        box((0.06, 0.06, 0.52), (x, -0.56, 1.92), "bark", rot_y=-38 if x < 0 else 38)
        box((0.06, 0.06, 0.42), (x * 3.0, -0.50, 2.12), "bark", rot_y=-64 if x < 0 else 64)
        box((0.05, 0.05, 0.30), (x * 2.1, -0.62, 2.20), "bark", rot_y=-26 if x < 0 else 26)


def hollow():
    """
    No legs and no head. A tall hollow bark shell standing on a knot of roots, with
    the core high in it and antlers rising straight out of the opening.

    The eeriest of the four, and the cheapest: two big masses and a crown. Whether it
    reads as a spirit or as a bin depends entirely on the shell being properly open,
    which is the one thing that has already gone wrong once here.
    """
    for i in range(9):
        rad = math.radians(40.0 * i)
        box((0.09, 0.09, 0.46), (math.cos(rad) * 0.26, math.sin(rad) * 0.26, 0.18),
            "bark", rot_x=math.sin(rad) * 52.0, rot_y=-math.cos(rad) * 52.0)

    taper(0.34, 0.30, 0.34, (0.0, 0.0, 0.40), "bark", sides=8)
    shell(0.30, 0.40, 1.10, (0.0, 0.0, 1.06), "bark", sides=8)
    ribcage(1.24, 0.60, 0.17, staves=4, stave=0.05)

    taper(0.42, 0.34, 0.20, (0.0, 0.0, 1.58), "moss", sides=8)

    for x in (-0.14, 0.14):
        box((0.07, 0.07, 0.54), (x, 0.0, 1.92), "bark", rot_y=-22 if x < 0 else 22)
        box((0.06, 0.06, 0.36), (x * 2.6, 0.02, 2.20), "bark", rot_y=-54 if x < 0 else 54)


def podkin():
    """
    The pod, walked off. Keeps stage four's open shell and splayed staves as the whole
    upper body, and grows limbs under it.

    The strongest continuity argument of the four: you watched that exact shape swell
    and split for an hour, so when it stands up there is no question what it is. The
    risk is the opposite one - that it reads as the sapling with legs stuck on rather
    than as a creature.
    """
    for x in (-0.22, 0.22):
        roots((x, 0.0, 0.15), 0.14, count=4, length=0.32)
        taper(0.12, 0.10, 0.66, (x, 0.0, 0.46), "bark", rot_y=-5 if x < 0 else 5)

    taper(0.28, 0.24, 0.18, (0.0, 0.0, 0.84), "bark")

    for x, tilt in ((-0.32, 14), (0.32, -14)):
        box((0.08, 0.08, 0.62), (x, 0.05, 0.86), "bark", rot_y=tilt)
        box((0.07, 0.07, 0.44), (x * 1.3, 0.07, 0.42), "bark", rot_y=-tilt * 0.6)

    shell(0.26, 0.34, 0.62, (0.0, 0.0, 1.24), "moss", sides=8)
    ribcage(1.20, 0.46, 0.17, staves=4, stave=0.05)

    for angle in (45, 135, 225, 315):
        rad = math.radians(angle)
        box((0.075, 0.075, 0.46), (math.cos(rad) * 0.24, math.sin(rad) * 0.24, 1.68),
            "bark", rot_x=math.sin(rad) * 24.0, rot_y=-math.cos(rad) * 24.0)


DESIGNS = (
    ("grove_spirit_warden", warden),
    ("grove_spirit_stag", stag),
    ("grove_spirit_hollow", hollow),
    ("grove_spirit_podkin", podkin),
)


# --------------------------------------------------------------------------- output

def bevel_all(width=0.014, segments=2):
    """
    A chamfer on every edge, applied per object before anything is joined.

    This is the largest single change and the cheapest. Valheim's props are low-poly
    but they are not raw primitives: every edge carries a small chamfer, which gives
    it a bright line where it turns away from the sun. Without one, a box is four
    flat greys meeting at nothing, and the eye reads "untextured primitive" no matter
    how good the proportions are.

    Per object rather than after the join, because beveling a joined mesh works on
    the intersections between parts as well - and parts here overlap deliberately, so
    that produces spikes where two limbs cross.
    """
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue

        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("chamfer", "BEVEL")
        modifier.width = width
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(30.0)
        modifier.harden_normals = False

        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        except RuntimeError:
            obj.modifiers.remove(modifier)


def finish(name):
    bevel_all()

    bpy.ops.object.select_all(action="SELECT")
    joined = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.join()
    joined.name = name
    joined.data.name = name

    # Bake the transform into the mesh. join() adopts the transform of whichever
    # object happened to be first, and every other object's vertices are rewritten
    # into that object's local space to compensate - so on a join target that was a
    # 0.05-thick antler twig, local coordinates come out ~20x. The mesh renders and
    # exports correctly either way (both work in world space), but anything measured
    # off obj.data.vertices is nonsense until this runs, which is how the collider
    # sidecar ended up sizing a 2.2m creature at 4.4m.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bpy.ops.object.shade_flat()
    return joined


def write_col(path, height):
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("# box  centre x y z  size x y z  qx qy qz qw\n")
        fh.write("box 0.000 %.3f 0.000 0.480 %.3f 0.480 0 0 0 1\n"
                 % (height / 2.0, height))


def tint():
    for mat in bpy.data.materials:
        key = mat.name.split(".")[0].lower()
        if key not in TINTS:
            continue
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = TINTS[key]
            bsdf.inputs["Roughness"].default_value = 0.88
            if key == "core":
                bsdf.inputs["Emission Color"].default_value = TINTS[key]
                bsdf.inputs["Emission Strength"].default_value = 1.6


def stage_scene():
    bpy.ops.mesh.primitive_plane_add(size=30.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    plane.data.materials.append(gm)

    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    bpy.context.active_object.data.energy = 3.2
    bpy.context.active_object.rotation_euler = (math.radians(52), 0, math.radians(200))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.65


def reference_cube(at):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    cube = bpy.context.active_object
    cm = bpy.data.materials.new("ref")
    cm.use_nodes = True
    cm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.52, 0.52, 0.56, 1)
    cube.data.materials.append(cm)


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


def render(out_png, width=620, height=580):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(ASSETS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in DESIGNS:
        clear_scene()
        build()
        obj = finish(name)
        verts, tris = len(obj.data.vertices), len(obj.data.polygons)
        top = max(v.co.z for v in obj.data.vertices)

        bpy.ops.wm.obj_export(
            filepath=os.path.join(ASSETS, name + ".obj"),
            export_selected_objects=False, export_materials=True,
            export_normals=True, export_uv=True, export_triangulated_mesh=True,
            forward_axis="Z", up_axis="Y", path_mode="AUTO")
        write_col(os.path.join(ASSETS, name + ".col"), top)

        tint()
        stage_scene()
        reference_cube((1.85, 0.10, 0.50))
        camera((-2.70, 3.90, 1.75), (0.0, 0.0, 1.15))
        render(os.path.join(PREVIEWS, name + ".png"))
        print("DESIGN_OK %s verts=%d tris=%d top=%.2f" % (name, verts, tris, top))

    # All four together, at the size they will actually be met. Height differences
    # between them are a design fact and only visible side by side.
    clear_scene()
    for index, (_, build) in enumerate(DESIGNS):
        before = set(bpy.data.objects)
        build()
        # 1.5 down to -1.5: the camera is on +y, so +x lands on the left of frame.
        for obj in set(bpy.data.objects) - before:
            obj.location.x += (1.5 - index) * 1.35

    finish("strip")
    tint()
    stage_scene()
    reference_cube((-3.55, 0.10, 0.50))
    camera((-0.20, 8.60, 1.80), (0.0, 0.0, 1.15), lens=50)
    render(os.path.join(PREVIEWS, "grove_spirits.png"), width=940, height=520)
    print("DESIGN_OK grove_spirits")


main()
