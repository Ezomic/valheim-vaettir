"""
The seedlings for the wild plants: what a planted berry, mushroom, thistle or
dandelion looks like while it is coming up.

    blender --background --python tools/thicket_designs.py

Four families, three variants each, because four of the eight plants share one mesh
and the differences between the eight are cost, biome and level rather than shape.
A raspberry, a blueberry and a cloudberry seedling are the same young bush; what
tells them apart in the build menu is the icon, and in the world the hover name.

    bush        raspberry, blueberry, cloudberry
      sprig     one woody stem with three side branches, sparse
      tuft      three stems fanning from one base, leaves along their length
      crown     a short thick stem under a low dome of leaves

    mushroom    brown, yellow and blue mushrooms
      pair      two caps of different size on their own stalks
      button    one fat cap barely opened on a thick stalk
      cluster   four small caps of varying height from one clump

    thistle
      spike     one upright stem, narrow leaves, a bud at the top
      rosette   a low ring of pointed leaves and a bud in the middle
      fork      two stems from a low rosette, each with its own bud

    dandelion
      rosette   a flat ring of broad leaves, nothing else
      bloom     the rosette plus one short stem and an open head
      pair      the rosette with two stems, one open and one closed

Every one is rendered from standing height looking down, which is the only angle a
20cm thing on the ground is ever seen from. The metre cube is there to say how
small it is. A grey slab standing in for the grown bush was tried and removed: at
0.8m a metre behind a 0.3m seedling, seen from standing height, it fills half the
frame whatever you do with it, and the metre cube already answers the only question
it was there for.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

import vhbuild

from vhbuild import (bevel_all, box, camera, clear_scene, export, finish, limb,
                     reference_cube, render, stage_scene, taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SHIPPED = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(SHIPPED, "variants")
PREVIEWS = os.path.join(VARIANTS, "previews")

# Group colours for the render only. The runtime skins every group off a vanilla
# prefab, so these say nothing about the finished surface - they exist so a preview
# reads as a plant rather than as four greys, and so a cap is distinguishable from a
# leaf in a still image.
vhbuild.TINTS["cap"] = (0.40, 0.27, 0.17, 1.0)
vhbuild.TINTS["bloom"] = (0.68, 0.58, 0.13, 1.0)
vhbuild.TINTS["bud"] = (0.38, 0.24, 0.42, 1.0)

# Which variant of each family ships, and the filename WildPlant.Model names.
#
# Set from the renders. Until a pick is made these are a first choice rather than a
# chosen one, so that the branch always builds something complete - a missing .obj
# is a plant the mod refuses to register at all.
WINNERS = {
    "bush": "tuft",
    "mushroom": "cluster",
    "thistle": "rosette",
    "dandelion": "bloom",
}

SHIPPED_NAMES = {
    "bush": "thicket_bush",
    "mushroom": "thicket_mushroom",
    "thistle": "thicket_thistle",
    "dandelion": "thicket_dandelion",
}


# --------------------------------------------------------------------------- parts

def leaf(at, length, width, pitch, yaw, mat="moss", thickness=0.008):
    """
    One leaf: a thin box, laid out along its own yaw and tipped by its pitch.

    A box rather than a triangle because a leaf seen at this size is a blade of
    colour and its outline is two pixels of the render. Where a point is wanted -
    thistle, dandelion - it comes from a three-sided taper instead.

    Pitch is degrees from horizontal, so 0 lies flat on the ground and 90 stands up.
    """
    rad = math.radians(yaw)

    # 0.5 would put the leaf's inner edge exactly on the point it grows from, and a
    # joint with no overlap at all reads as a detached leaf floating beside a twig -
    # which is what the first render of the sprig looked like. 0.30 buries a fifth of
    # the leaf inside the branch.
    reach = length * 0.30 * math.cos(math.radians(pitch))
    rise = length * 0.30 * math.sin(math.radians(pitch))

    return box((length, width, thickness),
               (at[0] + math.cos(rad) * reach,
                at[1] + math.sin(rad) * reach,
                at[2] + rise),
               mat, rot_y=-pitch, rot_z=yaw)


def _blade(at, length, width, pitch, yaw, mat):
    """
    taper() takes no rot_z, so a pointed leaf is built lying along +z and then turned
    by hand. Doing it through taper's own rotation arguments would swing the blade to
    face a random horizontal direction instead - the same trap the chest discs hit.
    """
    obj = taper(width, 0.004, length, (0.0, 0.0, 0.0), mat, sides=3, spin=False)

    rad = math.radians(yaw)
    obj.rotation_euler = (math.radians(90.0 - pitch), 0.0, rad)

    # Same overlap as leaf(): the base of the blade sits inside the crown rather than
    # touching it.
    reach = length * 0.32 * math.cos(math.radians(pitch))
    rise = length * 0.32 * math.sin(math.radians(pitch))
    obj.location = (at[0] + math.cos(rad) * reach,
                    at[1] + math.sin(rad) * reach,
                    at[2] + rise)
    return obj


def rosette(at, count, length, width, pitch=22.0, mat="moss", pointed=True):
    """The ring of leaves every low plant here starts from."""
    for i in range(count):
        yaw = 360.0 / count * i + (i * 7.0)
        if pointed:
            _blade(at, length, width, pitch, yaw, mat)
        else:
            leaf(at, length, width, pitch, yaw, mat)


def cap(at, radius, height, mat="cap"):
    """
    A mushroom cap: a dome from a wide-bottomed taper, not a hemisphere.

    A capped cone is a lid, which is exactly what a mushroom cap is, so this is the
    one place in the repo where that reads correctly rather than as a mistake.
    """
    taper(radius, radius * 0.30, height, (at[0], at[1], at[2] + height * 0.5),
          mat, sides=9)


def stalk(at, height, thick, mat="bark", lean=0.0, yaw=0.0):
    return limb(at, height, 2, thick, thick * 0.78, lean, yaw, 2.0, mat, sides=5)


# --------------------------------------------------------------------------- bush

def bush_sprig():
    """
    One stem, three side branches, and not much on them.

    The sparse one. A seedling that already looks like a bush has nothing left to
    become, and the whole reason this piece exists is that it turns into a bush an
    hour later.
    """
    tip = stalk((0.0, 0.0, 0.0), 0.30, 0.020)

    for i, (height, yaw, length) in enumerate(
            [(0.10, 25.0, 0.14), (0.17, 155.0, 0.16), (0.24, 275.0, 0.12)]):
        base = (0.0, 0.0, height)
        branch = limb(base, length, 2, 0.016, 0.009,
                      58.0, yaw, 6.0, "bark", sides=5)

        # Along the branch, not on the end of it. Hung off the tip, three leaves make
        # a clump floating past where the wood stops, and at 6mm the branch under them
        # is invisible - so the whole thing read as leaves scattered on the ground.
        for j in range(3):
            t = 0.45 + j * 0.28
            at = (base[0] + (branch[0] - base[0]) * t,
                  base[1] + (branch[1] - base[1]) * t,
                  base[2] + (branch[2] - base[2]) * t)
            leaf(at, 0.080, 0.050, 16.0 + j * 9.0, yaw - 40.0 + j * 40.0)

    # Two at the very top, so the stem does not end in a bare spike.
    leaf(tip, 0.075, 0.048, 28.0, 60.0)
    leaf(tip, 0.070, 0.045, 34.0, 230.0)


def bush_tuft():
    """
    Three stems from one base, leaves the whole way up.

    Fuller than the sprig and reads as a young shrub rather than as a twig. The risk
    is that it already looks finished.
    """
    for stem, (yaw, lean, height) in enumerate(
            [(20.0, 14.0, 0.32), (140.0, 22.0, 0.27), (262.0, 18.0, 0.29)]):
        tip = stalk((0.0, 0.0, 0.0), height, 0.016, lean=lean, yaw=yaw)

        for j in range(3):
            at = (tip[0] * (0.35 + j * 0.22), tip[1] * (0.35 + j * 0.22),
                  height * (0.38 + j * 0.24))
            leaf(at, 0.090, 0.056, 14.0 + j * 8.0, yaw + 55.0)
            leaf(at, 0.084, 0.052, 18.0 + j * 8.0, yaw - 55.0)

        leaf(tip, 0.072, 0.046, 32.0, yaw)


def bush_crown():
    """
    A short thick stem under a low dome.

    Few large parts rather than many small ones, which is the rule that usually wins.
    The question is whether a dome of leaves at this size reads as a plant or as a
    cabbage.
    """
    stalk((0.0, 0.0, 0.0), 0.13, 0.028)

    for i in range(7):
        yaw = 360.0 / 7.0 * i + 11.0
        leaf((0.0, 0.0, 0.125), 0.125, 0.078, 12.0, yaw)

    leaf((0.0, 0.0, 0.165), 0.100, 0.070, 26.0, 40.0)
    leaf((0.0, 0.0, 0.170), 0.095, 0.066, 30.0, 210.0)


# --------------------------------------------------------------------------- mushroom

def mushroom_pair():
    """Two, one taller. A single mushroom reads as a prop; two read as a patch."""
    stalk((0.0, 0.0, 0.0), 0.075, 0.014, lean=6.0, yaw=30.0)
    cap((0.008, 0.004, 0.070), 0.048, 0.030)

    stalk((0.070, 0.030, 0.0), 0.048, 0.011, lean=9.0, yaw=200.0)
    cap((0.066, 0.028, 0.044), 0.034, 0.022)


def mushroom_button():
    """
    One fat cap barely opened, on a thick short stalk.

    The clearest silhouette of the three and the least interesting. It is here
    because a mushroom is one of the few things in the game whose whole shape is
    legible at 10cm, and throwing that away for detail would be a bad trade.
    """
    stalk((0.0, 0.0, 0.0), 0.055, 0.024)
    cap((0.0, 0.0, 0.046), 0.062, 0.040)


def mushroom_cluster():
    """Four, none of them the same height. Closest to how they actually grow."""
    for i, (x, y, height, radius) in enumerate(
            [(0.0, 0.0, 0.070, 0.040), (0.055, 0.020, 0.048, 0.031),
             (0.020, 0.058, 0.038, 0.026), (-0.045, 0.030, 0.028, 0.021)]):
        stalk((x, y, 0.0), height, 0.012 - i * 0.001,
              lean=6.0 + i * 3.0, yaw=40.0 * i)
        cap((x, y, height - 0.006), radius, radius * 0.62)


# --------------------------------------------------------------------------- thistle

def thistle_spike():
    """One stem, narrow leaves up it, a bud on top. The tallest of the three."""
    tip = stalk((0.0, 0.0, 0.0), 0.30, 0.014)

    for i in range(5):
        _blade((0.0, 0.0, 0.06 + i * 0.048), 0.10, 0.020, 34.0, 72.0 * i + 15.0, "moss")

    taper(0.026, 0.010, 0.055, (tip[0], tip[1], tip[2] + 0.024), "bud", sides=7)


def thistle_rosette():
    """
    A low ring of pointed leaves with a bud in the middle, and no stem at all.
    Flattest of the three, and the hardest to trip over on a path.
    """
    rosette((0.0, 0.0, 0.012), 7, 0.135, 0.028, pitch=17.0)
    taper(0.024, 0.008, 0.050, (0.0, 0.0, 0.038), "bud", sides=7)


def thistle_fork():
    """Two stems out of a low rosette, each with a bud. Reads as a clump."""
    rosette((0.0, 0.0, 0.010), 5, 0.105, 0.024, pitch=14.0)

    for yaw, height in [(35.0, 0.20), (215.0, 0.15)]:
        tip = stalk((0.0, 0.0, 0.02), height, 0.012, lean=16.0, yaw=yaw)
        _blade((tip[0] * 0.5, tip[1] * 0.5, height * 0.55), 0.075, 0.018,
               30.0, yaw + 90.0, "moss")
        taper(0.022, 0.008, 0.045, (tip[0], tip[1], tip[2] + 0.020), "bud", sides=7)


# --------------------------------------------------------------------------- dandelion

def dandelion_rosette():
    """
    Broad leaves and nothing else. What a dandelion actually is for most of its life,
    and the only variant here that could be mistaken for grass.
    """
    rosette((0.0, 0.0, 0.010), 6, 0.130, 0.048, pitch=13.0, pointed=False)


def dandelion_bloom():
    """The rosette plus one open head. The one that says which plant this is."""
    rosette((0.0, 0.0, 0.010), 6, 0.120, 0.046, pitch=13.0, pointed=False)

    tip = stalk((0.0, 0.0, 0.015), 0.13, 0.009, lean=8.0, yaw=70.0)
    taper(0.040, 0.030, 0.018, (tip[0], tip[1], tip[2] + 0.008), "bloom", sides=11)


def dandelion_pair():
    """Two heads, one open and one still closed, which is how a patch looks."""
    rosette((0.0, 0.0, 0.010), 6, 0.115, 0.044, pitch=13.0, pointed=False)

    tip = stalk((0.0, 0.0, 0.015), 0.14, 0.009, lean=10.0, yaw=60.0)
    taper(0.038, 0.029, 0.017, (tip[0], tip[1], tip[2] + 0.008), "bloom", sides=11)

    closed = stalk((0.020, -0.015, 0.015), 0.10, 0.008, lean=14.0, yaw=240.0)
    taper(0.020, 0.014, 0.030, (closed[0], closed[1], closed[2] + 0.014), "moss", sides=7)


# --------------------------------------------------------------------------- output

FAMILIES = [
    ("bush", [("sprig", bush_sprig), ("tuft", bush_tuft), ("crown", bush_crown)]),
    ("mushroom", [("pair", mushroom_pair), ("button", mushroom_button),
                  ("cluster", mushroom_cluster)]),
    ("thistle", [("spike", thistle_spike), ("rosette", thistle_rosette),
                 ("fork", thistle_fork)]),
    ("dandelion", [("rosette", dandelion_rosette), ("bloom", dandelion_bloom),
                   ("pair", dandelion_pair)]),
]


def scale_block(edge, at):
    """
    A grey cube of a known size, for the tight crops.

    A metre cube is the right reference for a 30cm bush and useless for a 9cm
    mushroom: at the lens needed to see the mushroom at all, the cube is off the side
    of the frame. So the small families get a 20cm block instead, from the same
    viewpoint - the eye height does not change, only how much of the frame the subject
    is given.
    """
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    cube = bpy.context.active_object
    cube.scale = (edge, edge, edge)

    mat = bpy.data.materials.new("scale")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value =         (0.52, 0.52, 0.56, 1)
    cube.data.materials.append(mat)


def build(family, label, maker):
    name = family + "_" + label

    clear_scene()
    maker()

    # 6mm and one segment. The default 14mm is most of the thickness of a leaf here
    # and would round one away entirely, and a second segment doubles what a bevel
    # adds for a chamfer that is a pixel wide at the distance this is seen from.
    bevel_all(width=0.006, segments=1)
    obj = finish(name, bevel=False)

    export(obj, name, VARIANTS)

    if WINNERS.get(family) == label:
        export(obj, SHIPPED_NAMES[family], SHIPPED)

    tris = len(obj.data.polygons)
    height = max(v.co.z for v in obj.data.vertices)

    clear_scene()
    maker()
    bevel_all(width=0.006, segments=1)
    finish("preview", bevel=False)

    tint()

    # Sun well down from the helper's default, and the sky with it. At 3.2 and 0.65
    # every green in the model lands on the same value and no silhouette can be
    # judged at all - the tell is dark leaves rendering as pale beige.
    stage_scene(sun=2.3)
    bpy.context.scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.46

    # Behind and to the far side, not beside the camera. At 1.05, -0.75 it sat almost
    # in the lens and ate a third of the frame, making the metre cube the subject of a
    # render meant to show a 30cm plant.
    # Standing height either way. The lens is what changes, not the viewpoint: a 9cm
    # mushroom photographed at the framing that suits a 33cm bush is forty pixels of
    # brown in the middle of a field, and no design decision can be made from it.
    tight = height < 0.20

    if tight:
        scale_block(0.20, (0.34, 0.30, 0.10))
    else:
        reference_cube((1.05, 1.25, 0.5))

    camera((-0.72, -0.92, 1.42), (0.0, 0.0, height * 0.45),
           lens=110 if tight else 50)

    render(os.path.join(PREVIEWS, name + ".png"), width=640, height=560, bloom=False)

    shipped = "  [SHIPPED as %s]" % SHIPPED_NAMES[family] \
        if WINNERS.get(family) == label else ""
    print("DESIGN_OK %-18s tris=%4d  height=%.2fm%s" % (name, tris, height, shipped))


def main():
    os.makedirs(PREVIEWS, exist_ok=True)
    os.makedirs(VARIANTS, exist_ok=True)

    for family, variants in FAMILIES:
        for label, maker in variants:
            build(family, label, maker)


main()
