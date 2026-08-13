"""
The four sapling stages and the forest spirit, built and rendered.

    blender --background --python tools/grove_designs.py

Hand-built. Nothing vanilla is grafted on: material names here are group names only,
and at runtime each group is skinned with a real material lifted off a game prefab,
so the shapes are ours and the surfaces are the game's.

Rules carried over from Stoker's seven rejected models and Stow's four posts:

  * every part must overlap its neighbour - a 5cm gap reads as a detached stick
  * few large parts beat many small ones - small parts read as rubble, not detail
  * heaps of little cubes look like confetti; use one mass, not twelve lumps
  * a capped cone is a lid, so an open mouth is built from separate boards
  * primitive_cube_add(size=1.0) is already a unit cube - scale by size, not size/2
  * the preview camera stands on +y, so anything meant to be behind goes on -y

The four stages have to read as one thing growing, not four things. So they share a
trunk line and a lean, and what changes between them is mass and height - the seed
splits, the shoot thickens, the pod swells, the pod opens. Reading them as a strip is
the point; each is rendered alone and then all four together.
"""

import bpy
import math
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
PREVIEWS = os.path.join(ASSETS, "previews")

COLLIDERS = []

TINTS = {
    "bark":  (0.24, 0.17, 0.11, 1.0),
    "moss":  (0.20, 0.28, 0.14, 1.0),
    "stone": (0.44, 0.43, 0.40, 1.0),
    "core":  (0.62, 0.78, 0.42, 1.0),
    "seed":  (0.34, 0.28, 0.16, 1.0),
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


def taper(bottom, top, height, location, mat, sides=6, rot_x=0.0, rot_y=0.0):
    """A capped frustum. Trunks and limbs are solid, so caps are wanted here."""
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=bottom, radius2=top,
                                    depth=height, location=location)
    obj = bpy.context.active_object
    obj.rotation_euler = (math.radians(rot_x), math.radians(rot_y), 0.0)
    obj.data.materials.append(material(mat))
    return obj


def shell(radius, height, location, mat, sides=6, rot_x=0.0):
    """
    An open pod: a frustum with both caps deleted.

    Face select mode, and set before anything is selected. In the default vertex mode
    deleting "faces" deletes every face whose vertices are selected, and on a frustum
    every vertex belongs to one cap or the other - so selecting both caps silently
    selects the whole mesh and the pod vanishes.
    """
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius, radius2=radius * 0.55,
                                    depth=height, location=location)
    obj = bpy.context.active_object
    obj.rotation_euler = (math.radians(rot_x), 0.0, 0.0)

    mesh = obj.data
    bpy.ops.object.mode_set(mode="EDIT")
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


def mound():
    """
    Disturbed earth, shared by every stage so the base of the piece never jumps.

    Small. The first one was 0.44 across and taller than the seed sitting on it, so
    every stage read as a green hill with something stuck in the top of it - the
    mound was the silhouette and the sapling was the detail, which is backwards.
    """
    taper(0.28, 0.21, 0.09, (0.0, 0.0, 0.035), "moss", sides=8)


# --------------------------------------------------------------------------- stages

def stage_one():
    """
    Just planted. The seed is still a seed - split, half-buried, one pale shoot.

    Deliberately small enough to be easy to lose, because the thing you do next is
    walk away and go killing, and coming back to something conspicuous would rob the
    later stages of their reveal.
    """
    mound()
    taper(0.17, 0.13, 0.22, (0.0, 0.0, 0.17), "seed", sides=6, rot_x=6)
    box((0.05, 0.05, 0.24), (0.03, -0.02, 0.32), "core", rot_x=9)
    box((0.16, 0.05, 0.04), (0.0, 0.0, 0.24), "bark", rot_x=-14)


def stage_two():
    """Rooted and climbing. A knee-high trunk with the first two limbs."""
    mound()
    taper(0.15, 0.09, 0.62, (0.0, 0.0, 0.36), "bark", sides=6, rot_x=4)

    box((0.07, 0.07, 0.34), (0.15, -0.04, 0.52), "bark", rot_y=-38)
    box((0.07, 0.07, 0.28), (-0.14, 0.03, 0.58), "bark", rot_y=34)

    taper(0.11, 0.04, 0.17, (0.0, -0.01, 0.72), "moss", sides=6)
    box((0.05, 0.05, 0.14), (0.02, -0.05, 0.68), "core", rot_x=8)


def stage_three():
    """Waist high, and something is swelling in it. The pod is closed."""
    mound()
    taper(0.20, 0.11, 0.94, (0.0, 0.0, 0.50), "bark", sides=6, rot_x=3)

    box((0.09, 0.09, 0.46), (0.21, -0.05, 0.66), "bark", rot_y=-42)
    box((0.09, 0.09, 0.40), (-0.20, 0.05, 0.76), "bark", rot_y=38)
    box((0.08, 0.08, 0.32), (0.05, 0.18, 0.88), "bark", rot_x=36)

    # A pod, not a lampshade. At 0.24 across on a 0.11 trunk the first one was twice
    # the width of what carried it and the whole stage read as a mushroom.
    taper(0.16, 0.13, 0.26, (0.0, -0.02, 1.02), "moss", sides=8)
    taper(0.13, 0.04, 0.14, (0.0, -0.02, 1.20), "moss", sides=8)
    box((0.06, 0.06, 0.18), (0.0, -0.12, 1.02), "core", rot_x=4)


def stage_four():
    """
    Full grown and about to open. The pod is a shell rather than a lump, because a
    closed mass at the top of the last stage says "finished" instead of "imminent".
    """
    mound()
    taper(0.26, 0.14, 1.24, (0.0, 0.0, 0.64), "bark", sides=6, rot_x=2)

    box((0.11, 0.11, 0.60), (0.28, -0.06, 0.84), "bark", rot_y=-44)
    box((0.11, 0.11, 0.52), (-0.26, 0.06, 0.98), "bark", rot_y=40)
    box((0.10, 0.10, 0.44), (0.07, 0.23, 1.10), "bark", rot_x=38)

    shell(0.24, 0.46, (0.0, -0.02, 1.40), "moss", sides=8)
    box((0.14, 0.14, 0.32), (0.0, -0.02, 1.38), "core")

    # Four staves splaying off the open pod, leaning outwards. Kept to four and kept
    # large: this is the stage that has to read as *about to open*, and a ring of
    # small twigs would read as decoration.
    for angle in (45, 135, 225, 315):
        rad = math.radians(angle)
        box((0.07, 0.07, 0.40), (math.cos(rad) * 0.21, math.sin(rad) * 0.21 - 0.02, 1.68),
            "bark", rot_x=math.sin(rad) * 22.0, rot_y=-math.cos(rad) * 22.0)


# --------------------------------------------------------------------------- spirit

def spirit():
    """
    What comes out. Tall, thin and bark-limbed, with a lit core where a chest would
    be - so it reads as the thing that was inside the pod rather than as another
    forest creature.

    Deliberately taller than a player and much narrower than a greydwarf: the whole
    point is that it is not one of the things you killed to grow it.
    """
    for x in (-0.16, 0.16):
        taper(0.10, 0.07, 0.86, (x, 0.0, 0.44), "bark", sides=6)
        box((0.16, 0.22, 0.09), (x, -0.04, 0.05), "bark")

    # A ribcage, not a barrel. The first version put the core inside a solid torso
    # taper, which hid it completely - the only green anyone could see was the neck,
    # and the one idea the creature is built around was invisible.
    #
    # Hips and shoulders are solid and carry the silhouette; between them there is
    # nothing but staves and the light.
    taper(0.23, 0.19, 0.16, (0.0, 0.0, 0.90), "bark", sides=6)
    taper(0.20, 0.23, 0.16, (0.0, 0.0, 1.40), "bark", sides=6)

    box((0.17, 0.17, 0.34), (0.0, 0.0, 1.15), "core")
    for angle in (35, 145, 215, 325):
        rad = math.radians(angle)
        box((0.055, 0.055, 0.50), (math.cos(rad) * 0.19, math.sin(rad) * 0.19, 1.15), "bark")

    taper(0.19, 0.13, 0.20, (0.0, 0.0, 1.56), "moss", sides=6)

    # Arms hung off the shoulder mass and swung further out, so they clear the
    # ribcage instead of merging into it.
    for x, tilt in ((-0.28, 20), (0.28, -20)):
        box((0.07, 0.07, 0.58), (x, 0.03, 1.24), "bark", rot_y=tilt)
        box((0.06, 0.06, 0.46), (x * 1.48, 0.05, 0.84), "bark", rot_y=tilt * 0.5)

    taper(0.15, 0.12, 0.26, (0.0, -0.01, 1.80), "bark", sides=6)
    box((0.11, 0.04, 0.05), (0.0, -0.13, 1.82), "core")

    # Antlers. Two forks a side, kept large - the first pass used six small twigs and
    # they read as static rather than as a crown.
    for x in (-0.11, 0.11):
        box((0.05, 0.05, 0.40), (x, 0.02, 2.06), "bark", rot_y=-28 if x < 0 else 28)
        box((0.05, 0.05, 0.26), (x * 2.6, 0.03, 2.26), "bark", rot_y=-52 if x < 0 else 52)

    collide((0.0, 0.0, 0.95), (0.44, 0.44, 1.90))


# Name, builder, camera position, aim height. The spirit is 2.3m to the antler tips
# and the first stage is 0.4m; one camera cannot frame both, and a head cropped out
# of frame tells you nothing about whether the head works.
DESIGNS = (
    ("grove_sapling_1", stage_one,   (-1.30, 1.85, 1.10), 0.30),
    ("grove_sapling_2", stage_two,   (-1.55, 2.20, 1.30), 0.45),
    ("grove_sapling_3", stage_three, (-1.85, 2.60, 1.55), 0.65),
    ("grove_sapling_4", stage_four,  (-2.15, 3.05, 1.70), 0.90),
    ("grove_spirit",    spirit,      (-2.70, 3.85, 1.80), 1.20),
)


# --------------------------------------------------------------------------- output

def finish(name):
    bpy.ops.object.select_all(action="SELECT")
    joined = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.join()
    joined.name = name
    joined.data.name = name

    # Flat everywhere. Valheim's props are flat-shaded low-poly and smoothing a box
    # just makes the corners look wet.
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
            if key == "core":
                bsdf.inputs["Emission Color"].default_value = TINTS[key]
                bsdf.inputs["Emission Strength"].default_value = 1.4


def stage_scene(ground=True):
    if ground:
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


def render(out_png, width=620, height=560):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(ASSETS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    for name, build, eye, aim in DESIGNS:
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
        stage_scene()
        reference_cube((abs(eye[0]) * 0.72, 0.10, 0.50))
        camera(eye, (0.0, 0.0, aim))
        render(os.path.join(PREVIEWS, name + ".png"))
        print("DESIGN_OK %s verts=%d tris=%d" % (name, verts, tris))

    # The four stages together. Read as a strip they either say "one thing growing"
    # or they do not, and no amount of looking at them one at a time will tell you.
    #
    # Offset runs 1.5 down to -1.5, not up: the camera stands on +y looking towards
    # -y, so +x lands on the left of frame and the obvious ordering put the finished
    # sapling first and the seed last.
    clear_scene()
    for index, (_, build, _eye, _aim) in enumerate(DESIGNS[:4]):
        before = set(bpy.data.objects)
        build()
        for obj in set(bpy.data.objects) - before:
            obj.location.x += (1.5 - index) * 1.05

    finish("strip")
    tint()
    stage_scene()
    reference_cube((-2.90, 0.10, 0.50))
    camera((-0.20, 7.60, 1.75), (0.0, 0.0, 0.85), lens=50)
    render(os.path.join(PREVIEWS, "grove_stages.png"), width=940, height=440)
    print("DESIGN_OK grove_stages")


main()
