"""
Five original stowing-post designs, built and rendered for comparison.

    blender --background --python tools/post_designs.py

Hand-built rather than grafted from a vanilla prop, because a borrowed crate always
reads as a crate with a new label. Nothing here paints anything: material names are
group names only, and at runtime each group is skinned with a real material lifted
off a game prefab, so the piece is made of the game's own wood and iron rather than
an approximation of them.

Shaped by the rules Stoker's seven rejected models paid for:

  * every part must overlap its neighbour - a 5cm gap reads as a detached stick
  * few large parts beat many small ones - small parts read as rubble, not detail
  * heaps of little cubes look like confetti; use one mass, not twelve lumps
  * a capped cone is a lid, so an open mouth is built from separate boards
  * primitive_cube_add(size=1.0) is already a unit cube - scale by size, not size/2

And one rule this piece adds: a stowing post has to look like somewhere you put
things down. Every design here has a visible opening at roughly waist height,
because a sealed box says "storage" and the whole point is that this is not storage.

The five differ in silhouette on purpose. If two share an outline there is really
only one design, which is what made Stoker's first batch feel like no choice at all.
"""

import bpy
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

COLLIDERS = []

# Preview only. The runtime uses borrowed vanilla materials; these are just close
# enough that a render says something useful about the shape.
TINTS = {
    "wood": (0.30, 0.19, 0.10, 1.0),
    "iron": (0.19, 0.19, 0.21, 1.0),
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


def material(name):
    mat = bpy.data.materials.get(name)
    return mat if mat else bpy.data.materials.new(name)


def collide(centre, size):
    COLLIDERS.append((centre, size))


def box(size, location, mat, rot_x=0.0, rot_y=0.0, rot_z=0.0, hit=False):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.scale = (size[0], size[1], size[2])
    obj.rotation_euler = (math.radians(rot_x), math.radians(rot_y), math.radians(rot_z))
    obj.data.materials.append(material(mat))
    if hit:
        collide(location, size)
    return obj


def cyl(radius, length, location, mat, axis="z", sides=10):
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=length, vertices=sides,
                                        location=location)
    obj = bpy.context.active_object
    if axis == "x":
        obj.rotation_euler = (0.0, math.radians(90), 0.0)
    elif axis == "y":
        obj.rotation_euler = (math.radians(90), 0.0, 0.0)
    obj.data.materials.append(material(mat))
    return obj


def frame(width, depth, z, thick, height, mat):
    """
    Four thin boxes forming a rectangle, never one wide one.

    The first pass banded every open top with a single full-area plate, which is the
    same mistake as a capped cone by another route: from eye height it reads as a lid
    and the piece becomes a sealed box. A band has to have a hole in it.
    """
    box((width, thick, height), (0.0, -depth / 2 + thick / 2, z), mat)
    box((width, thick, height), (0.0, depth / 2 - thick / 2, z), mat)
    box((thick, depth, height), (-width / 2 + thick / 2, 0.0, z), mat)
    box((thick, depth, height), (width / 2 - thick / 2, 0.0, z), mat)


def funnel(bottom, top, height, z, mat, sides=4):
    """
    A four-sided frustum with both caps deleted - a genuine open mouth.

    Built from leaning boards first, which does not work: rotating a box about its
    centre sweeps its lower inside edge towards the axis, so four boards tilted to
    meet at the top cross each other at the bottom and the middle of the funnel comes
    out a jagged mess. A cone gets the taper exactly right; it only needs its lid and
    its floor taken off, which a box can never have in the first place.
    """
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top,
                                    depth=height, location=(0.0, 0.0, z),
                                    rotation=(0.0, 0.0, math.radians(45)))
    obj = bpy.context.active_object

    mesh = obj.data
    bpy.ops.object.mode_set(mode="EDIT")

    # Face select mode, and it has to be set before anything is selected. In the
    # default vertex mode, deleting "faces" deletes every face whose vertices are
    # selected - and on a frustum every vertex belongs to either the top cap or the
    # bottom one, so selecting both caps silently selects the whole mesh and the
    # funnel vanishes. It cost one render to notice, because a missing part looks
    # exactly like a part that was never added.
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


def tray(width, depth, floor_z, wall, mat, rim=0.05):
    """
    An open box: a floor and four walls, never a capped cube.

    The walls overlap the floor rather than sitting on it, because a wall that only
    touches shows a seam from every angle the sun is not directly behind.
    """
    box((width, depth, 0.07), (0.0, 0.0, floor_z), mat)
    box((width, rim, wall), (0.0, -depth / 2 + rim / 2, floor_z + wall / 2 - 0.02), mat)
    box((width, rim, wall), (0.0, depth / 2 - rim / 2, floor_z + wall / 2 - 0.02), mat)
    box((rim, depth, wall), (-width / 2 + rim / 2, 0.0, floor_z + wall / 2 - 0.02), mat)
    box((rim, depth, wall), (width / 2 - rim / 2, 0.0, floor_z + wall / 2 - 0.02), mat)


# --------------------------------------------------------------------------- designs

def design_rack():
    """
    Pigeonhole rack. Tall, flat and wide - the outline of a thing you file into.

    Six open compartments read as sorting from across the room without anything
    having to be written on it, which is the one idea none of the others carry.
    """
    box((1.10, 0.52, 0.14), (0.0, 0.0, 0.07), "stone", hit=True)

    box((0.12, 0.46, 1.16), (-0.49, 0.0, 0.70), "wood", hit=True)
    box((0.12, 0.46, 1.16), (0.49, 0.0, 0.70), "wood", hit=True)

    # Back board on -y. The preview camera stands on +y, and the first pass put this
    # between the lens and the compartments - six open pigeonholes photographed from
    # behind is indistinguishable from a crate.
    box((1.10, 0.08, 1.10), (0.0, -0.21, 0.68), "wood")

    for z in (0.44, 0.86):
        box((0.98, 0.44, 0.07), (0.0, 0.02, z), "wood")
    for x in (-0.17, 0.17):
        box((0.07, 0.42, 0.78), (x, 0.02, 0.68), "wood")

    box((1.20, 0.56, 0.10), (0.0, 0.0, 1.28), "wood", hit=True)
    box((1.16, 0.06, 0.05), (0.0, 0.24, 0.44), "iron")
    box((1.16, 0.06, 0.05), (0.0, 0.24, 0.86), "iron")
    box((1.14, 0.54, 0.06), (0.0, 0.0, 0.16), "iron")


def design_chute():
    """
    Chute post. Narrow, vertical, flaring at the top - the only one with a real
    funnel, and the only one that says "in" rather than "on".

    The mouth is four leaning boards rather than a cone, because a cone comes out of
    Blender capped and a capped cone is a lid.
    """
    box((0.74, 0.74, 0.18), (0.0, 0.0, 0.09), "stone", hit=True)
    box((0.30, 0.30, 0.86), (0.0, 0.0, 0.58), "wood", hit=True)

    # Boards wide enough to overlap at the corners and long enough to reach the post.
    # The first pass left both gaps and the funnel read as four sticks leaning near
    # each other rather than as one mouth.
    funnel(0.22, 0.74, 0.58, 1.16, "wood")

    frame(1.10, 1.10, 1.42, 0.11, 0.10, "iron")
    box((0.38, 0.38, 0.08), (0.0, 0.0, 0.94), "iron")


def design_table():
    """
    Sorting table. Low, wide and horizontal - the only one you could stand another
    piece beside without the room feeling crowded.
    """
    box((1.36, 0.74, 0.10), (0.0, 0.0, 0.84), "wood", hit=True)

    # Back board and its rail on -y, so the tray in the top faces the room.
    box((1.36, 0.09, 0.42), (0.0, -0.32, 1.08), "wood", hit=True)

    for x in (-0.58, 0.58):
        for y in (-0.29, 0.29):
            box((0.13, 0.13, 0.86), (x, y, 0.43), "wood")

    box((1.24, 0.09, 0.09), (0.0, -0.29, 0.26), "wood")
    box((1.24, 0.09, 0.09), (0.0, 0.29, 0.26), "wood")

    tray(0.86, 0.52, 0.90, 0.20, "wood")

    # Hugging the table edge rather than hanging in front of it. At y=0.35 with the
    # top only 0.74 deep, the first one floated clear of the piece entirely and read
    # as a bar someone had left leaning against it.
    box((1.30, 0.06, 0.10), (0.0, 0.35, 0.845), "iron")
    frame(0.92, 0.58, 1.02, 0.06, 0.06, "iron")


def design_barrow():
    """
    Handled bin. Chunky, mid-height, with the two shafts breaking the outline - the
    only silhouette here that is not a rectangle.

    The shafts are what make it read as something you fill and then deal with,
    rather than something that lives where it stands.
    """
    box((0.94, 0.64, 0.09), (0.0, 0.0, 0.46), "wood", hit=True)

    box((0.94, 0.08, 0.50), (0.0, -0.33, 0.68), "wood", rot_x=-13)
    box((0.94, 0.08, 0.50), (0.0, 0.33, 0.68), "wood", rot_x=13)
    box((0.08, 0.70, 0.50), (-0.46, 0.0, 0.68), "wood", rot_y=13)
    box((0.08, 0.70, 0.50), (0.46, 0.0, 0.68), "wood", rot_y=-13)

    # Legs under the bin, shafts running out past it. The first pass made both too
    # short and they read as stubs stuck to the side rather than as a frame the bin
    # sits in.
    # Shafts short and high, tucked against the bin. The first pair were 1.86m and sat
    # at axle height, so they reached the ground on both sides and read as two logs
    # lying beside the piece rather than as handles on it.
    for x in (-0.40, 0.40):
        box((0.13, 0.13, 0.56), (x, -0.22, 0.20), "wood", rot_x=8)
        cyl(0.06, 1.06, (x, 0.24, 0.60), "wood", axis="y")

    frame(1.04, 0.74, 0.90, 0.08, 0.07, "iron")
    frame(1.00, 0.70, 0.52, 0.07, 0.06, "iron")


def design_runepost():
    """
    Rune post. A heavy upright on a broad stone foot, with the tray hung off it -
    the tallest and the narrowest, and the only one that reads as a marker rather
    than as furniture.
    """
    # A smaller foot, one heavy post, and the tray hung *on* it rather than floating
    # near it. The first pass had a wide two-tier plinth, a slim post standing clear
    # of the tray, and three separate iron bands stacked up it - which together read
    # as a birdbath assembled from spare blocks.
    box((0.82, 0.82, 0.22), (0.0, -0.06, 0.11), "stone", hit=True)

    box((0.34, 0.34, 1.42), (0.0, -0.14, 0.93), "wood", hit=True)

    # Bracket first, so the tray has something to sit on and the join is visible.
    box((0.62, 0.30, 0.10), (0.0, 0.02, 0.70), "wood")
    tray(0.86, 0.60, 0.76, 0.26, "wood")

    box((0.40, 0.40, 0.09), (0.0, -0.14, 1.30), "iron")
    box((0.46, 0.46, 0.14), (0.0, -0.14, 1.60), "wood")

    frame(0.90, 0.64, 0.98, 0.07, 0.06, "iron")


# The rune post is cut. Three passes in it still read as a birdbath: a tray on a
# vertical is a shape the eye already has a word for, and it is not this one. Four
# genuinely different silhouettes beat five with a dud among them - the whole reason
# for building variants was to offer a real choice, and a design nobody would pick
# takes up a slot without being one.
VARIANTS = (
    ("stow_post_rack", design_rack),
    ("stow_post_chute", design_chute),
    ("stow_post_table", design_table),
    ("stow_post_barrow", design_barrow),
)


# --------------------------------------------------------------------------- output

def finish(name):
    bpy.ops.object.select_all(action="SELECT")
    joined = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.join()
    joined.name = name
    joined.data.name = name

    # Sharp edges everywhere. Valheim's props are flat-shaded low-poly and smoothing
    # a box just makes the corners look wet.
    bpy.ops.object.shade_flat()
    return joined


def write_col(path):
    # Blender is Z-up and Unity is Y-up, so y and z swap on the way out. The mesh
    # export does this itself; the sidecar has to be told.
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("# box  centre x y z  size x y z  qx qy qz qw\n")
        for (cx, cy, cz), (sx, sy, sz) in COLLIDERS:
            fh.write("box %.3f %.3f %.3f %.3f %.3f %.3f 0 0 0 1\n"
                     % (cx, cz, cy, sx, sz, sy))


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


def stage_and_render(out_png):
    """
    Deliberately not a hero shot. Three-quarter view from 1.7m at three metres is
    what a player actually sees, and a model that only reads from a flattering angle
    is a model that does not read. A 1m cube stands beside it for scale.
    """
    bpy.ops.mesh.primitive_plane_add(size=20.0, location=(0, 0, 0))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    ground.data.materials.append(gm)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(1.65, 0.1, 0.5))
    cube = bpy.context.active_object
    cm = bpy.data.materials.new("ref")
    cm.use_nodes = True
    cm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.52, 0.52, 0.56, 1)
    cube.data.materials.append(cm)

    bpy.ops.object.camera_add(location=(-1.95, 2.70, 1.70))
    cam = bpy.context.active_object
    cam.data.lens = 42
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = (0.0, 0.0, 0.62)
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(3, 4, 6))
    bpy.context.active_object.data.energy = 3.2
    bpy.context.active_object.rotation_euler = (math.radians(52), 0, math.radians(200))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.65

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 620
    scene.render.resolution_y = 520
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(ASSETS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build in VARIANTS:
        clear_scene()
        build()
        obj = finish(name)
        verts, tris = len(obj.data.vertices), len(obj.data.polygons)

        bpy.ops.wm.obj_export(
            filepath=os.path.join(ASSETS, name + ".obj"),
            export_selected_objects=False, export_materials=True,
            export_normals=True, export_uv=True, export_triangulated_mesh=True,
            forward_axis="Z", up_axis="Y", path_mode="AUTO")
        write_col(os.path.join(ASSETS, name + ".col"))

        tint()
        stage_and_render(os.path.join(PREVIEWS, name + ".png"))
        print("DESIGN_OK %s verts=%d tris=%d" % (name, verts, tris))


main()
