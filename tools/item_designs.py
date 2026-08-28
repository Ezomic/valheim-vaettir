"""
Heartwood: the thing the spirit gives you, and its inventory icon.

**Superseded by heartwood_designs.py. Running this overwrites the shipped mesh
and icon with the old design.**

The split billet below was built when the heartwood was the spirit's *heart*,
taken out of it. It is now the spirit's *home*, which wants something closed and
occupied rather than opened and emptied - and at 48 pixels this one read as two
brown blobs either side of a pale card. Kept because everything it says about
icon legibility still holds and was learned the hard way.

    blender --background --python tools/item_designs.py

Named for the dense wood at the centre of a trunk, which is what it looks like -
a split billet of pale grain with the spirit's gold still in the seam. It has to
read at two sizes that could not be less alike: held in the world at about 20cm,
and as a 64-pixel square in a grid of other icons. The second is the hard one, so
the silhouette is deliberately simple and the gold is deliberately a single stripe
rather than a scatter - detail below about six pixels is mud.

The icon is rendered here rather than generated in game. Valheim builds item icons
from a camera rig in the editor, and there is none of that at runtime; a PNG beside
the dll and Sprite.Create is the whole of it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (bevel_all, box, camera, clear_scene, export, finish, orb,
                     render, stage_scene, taper, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Into variants/, never assets/ - heartwood_designs.py owns the shipped
# grove_heartwood.obj now, and two scripts exporting one filename means
# whichever ran last silently swaps the model the game loads.
ASSETS = os.path.join(ROOT, "assets", "variants")
PREVIEWS = os.path.join(ROOT, "assets", "previews")

NAME = "grove_heartwood"


def heartwood():
    """
    A split billet: two halves of pale wood with the seam between them lit.

    Two masses and one stripe. An earlier instinct was to model bark, growth rings
    and a broken end, all of which vanish at icon size and only cost triangles - so
    everything here is either part of the outline or part of the one gold line.
    """
    # The two halves, held far enough apart to leave a real gap. The first pass put
    # them at -0.033 and 0.034 with a radius of 0.06, so they overlapped across the
    # middle and swallowed the seam entirely - the icon came out as two beige blobs
    # with no gold anywhere in it.
    # Slimmer and further apart. At a 0.029 gap across a 0.26 span the gold was 11%
    # of the icon and disappeared at grid size; this puts it near a third, which is
    # the difference between "a lit thing" and "two beige blobs".
    taper(0.050, 0.040, 0.25, (-0.088, 0.0, 0.0), "bark", sides=7, rot_y=-6.0)
    taper(0.047, 0.038, 0.23, (0.089, 0.0, 0.006), "bark", sides=7, rot_y=7.0)

    # The seam, filling the gap and standing proud of both halves towards the viewer.
    # Proud matters: at y=-0.020 with the halves reaching -0.058, they were in front
    # of it and occluded the very thing the icon needed to show.
    box((0.086, 0.056, 0.225), (0.0, -0.034, 0.0), "core", tilt=1.0)

    # One bead, set into the seam rather than perched on top of the billet. On top it
    # read as a lollipop; embedded, it is a knot in the light.
    orb(0.030, (0.002, -0.052, 0.052), "core", subdivisions=1, tilt=0.0)


def build_icon():
    """
    Front-on, orthographic, transparent, square.

    Orthographic rather than perspective: an icon is a symbol, and perspective on
    something 20cm across just makes the near end fatter than the far one for no
    gain. Transparent film, because an icon with a sky behind it is a postage stamp.
    """
    scene = bpy.context.scene
    scene.render.film_transparent = True

    bpy.ops.object.camera_add(location=(0.0, -1.2, 0.02))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 0.30
    cam.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    scene.camera = cam

    # Suns, and gentle ones. Area lights at energy 90 sit a metre from a 25cm object
    # and blow every channel to white - the dark brown bark rendered pale beige and
    # the gold seam rendered as nothing at all, which read as the seam being missing
    # rather than as the lighting being wrong. A sun has no falloff to get wrong.
    bpy.ops.object.light_add(type="SUN", location=(-0.6, -1.0, 0.7))
    key = bpy.context.active_object
    key.data.energy = 2.6
    key.rotation_euler = (math.radians(58.0), 0.0, math.radians(-34.0))

    bpy.ops.object.light_add(type="SUN", location=(0.8, -0.9, -0.3))
    fill = bpy.context.active_object
    fill.data.energy = 0.9
    fill.rotation_euler = (math.radians(104.0), 0.0, math.radians(36.0))

    world = bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.0


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    # ------------------------------------------------------------- the mesh
    clear_scene()
    heartwood()
    obj = finish(NAME)
    export(obj, NAME, ASSETS)
    print("DESIGN_OK %s tris=%d" % (NAME, len(obj.data.polygons)))

    # ------------------------------------------------------------- in the world
    clear_scene()
    heartwood()
    finish("held")
    tint()
    stage_scene()
    camera((-0.42, 0.58, 0.30), (0.0, 0.0, 0.0), lens=52)
    render(os.path.join(PREVIEWS, NAME + ".png"), width=560, height=520)
    print("DESIGN_OK %s_world" % NAME)

    # ------------------------------------------------------------- the icon
    clear_scene()
    heartwood()
    finish("icon")

    # Barely any emission, unlike the world render. At 1.6 the gold clipped straight
    # to white and then vanished against the transparent background - an icon that
    # was two beige blobs and a hole. An icon has to keep its hue; the glow can be
    # implied by the colour rather than by blowing the channel out.
    tint(strength=0.9)

    build_icon()

    # 128, not 64. Valheim scales icons down and a sharp source survives that far
    # better than a source rendered at the size it will be shown.
    render(os.path.join(ASSETS, NAME + "_icon.png"), width=128, height=128, bloom=False)
    print("DESIGN_OK %s_icon" % NAME)


main()
