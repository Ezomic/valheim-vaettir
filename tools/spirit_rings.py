"""
Spirit: how many rings of beads, now that the ring itself is invisible.

    blender --background --python tools/spirit_rings.py

Three changes are being looked at together, because each one changes how the
others read:

  - the hoop mesh goes. Only the beads are drawn, so the circle is implied by
    where they are rather than stated by a torus they are threaded on.
  - the beads move together instead of each at its own rate. Drift used to give
    every bead a random speed and direction, which reads as a swarm. In step, the
    same beads read as one object turning - which is the whole difference between
    a cloud of flies and an orrery.
  - more than one ring, crossed like an atom.

Nothing here is a mesh that ships. The beads and the heart are already exported;
this only answers how many circles there are and at what angles, which at runtime
is a number in the config. So these renders are arrangements, not models.

Bead positions are the same maths the runtime uses: a circle in the XY plane
rotated about X by the ring's share of 180 degrees, which interleaves the circles
evenly instead of stacking them.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (camera, clear_scene, orb, reference_cube, render,
                     stage_scene, tint)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PREVIEWS = os.path.join(ROOT, "assets", "variants", "previews")

ORBIT = 0.34
BEAD = 0.040
HOVER = 1.15

# Six, not the seven the single ring shipped with. Seven was chosen so no two beads
# sat opposite each other on one circle; with several circles crossing, that no
# longer buys anything and an even count divides more cleanly across rings.
PER_RING = 6


def heart():
    """The mass at the centre, unchanged - it is what the rings are around."""
    orb(0.150, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.30)
    orb(0.104, (0.0, 0.0, HOVER), "core", subdivisions=2, stretch=1.55)


def beads(rings):
    """
    Every bead, on every ring. No torus - the circle is implied by the beads.

    Rings share the 180 degrees between them rather than 360, because a circle
    rotated 180 degrees is the same circle: giving four rings 0/90/180/270 would
    draw two of them twice and look like two.
    """
    for r in range(rings):
        # Starts vertical, not flat. A ring at zero tilt lies horizontal, and from eye
        # height a horizontal circle is seen edge on - the first render of the single
        # ring came out as four beads in a line with no circle readable at all. Ninety
        # degrees is the plane the shipped hoop already used.
        tilt = math.radians(90.0 + 180.0 / rings * r)

        for i in range(PER_RING):
            angle = 2.0 * math.pi / PER_RING * i + math.radians(12.0 * r)

            x = math.cos(angle) * ORBIT
            flat = math.sin(angle) * ORBIT

            # Rotate the flat circle about X, which tips it out of horizontal.
            y = flat * math.cos(tilt)
            z = flat * math.sin(tilt)

            orb(BEAD, (x, y, HOVER + z), "core", subdivisions=1, tilt=0.0)


def preview(label, rings):
    clear_scene()
    heart()
    beads(rings)

    bpy.ops.object.select_all(action="SELECT")
    joined = bpy.context.selected_objects[0]
    bpy.context.view_layer.objects.active = joined
    bpy.ops.object.join()
    bpy.ops.object.shade_flat()

    # Half strength. The beads and the heart are all emissive here, and at the usual
    # 1.15 the whole arrangement clips to one white mass with no separation between
    # the beads and the thing they are going round.
    tint(strength=0.5)

    stage_scene()
    reference_cube((1.05, -0.30, 0.5))

    # Eye height, close enough to be the view you get when you walk up to commune.
    camera((-1.55, -1.80, 1.62), (0.0, 0.0, HOVER), lens=46)

    render(os.path.join(PREVIEWS, "grove_spirit_rings_%s.png" % label),
           width=640, height=560)

    print("DESIGN_OK grove_spirit_rings_%s beads=%d" % (label, rings * PER_RING))


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for rings in (1, 2, 3, 4):
        preview(str(rings), rings)


main()
