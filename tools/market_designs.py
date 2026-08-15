"""
The market: where the visitor gets stationed.

    blender --background --python tools/market_designs.py

Four stalls with genuinely different outlines, because two designs that share a
silhouette are one design rendered twice. Judged at eye height with a metre cube
beside them, which is the only view that answers "how big is that actually".

  trestle  a plank counter on crossed legs under a slanted roof. The stall shape
           everyone already knows, and the only one that reads as a shop from
           across a clearing.
  stump    a felled trunk hollowed into a counter, awning slung between two
           leaning poles. The forest one - looks grown rather than carpentered.
  frame    a heavy A-frame with the counter board hung inside it and a lantern at
           the peak. Tall and narrow where the others are wide.
  cairn    a stone shelf under a timber lintel. Squat, no roof, no legs - the one
           that reads as old rather than built.

Rules these were built against, all of them paid for by earlier rejects: every
part overlaps its neighbour, few large parts beat many small ones, a full plate
across an opening is a lid so frames are separate boxes, odd-sided cylinders read
as round, bevel one segment per object before joining, and a hoop is one thin
cylinder rather than a ring of blocks. Buildable pieces are capped at 10,000
triangles and this fails loudly rather than quietly shipping a piece that costs
sixteen copies of itself in a row of stalls.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math

from vhbuild import (box, camera, clear_scene, collide, disc, export, finish,
                     limb, orb, reference_cube, render, roots, shell,
                     stage_scene, taper, tint, write_col)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Into variants, not assets. Nothing here is shipped: the market was cut from v1
# along with the visitor it was going to station, and the build copies assets/*.obj
# beside the dll. The designs and the reasoning stay in git because the next pass at
# this should not start from nothing - see the notes on limb's pitch in particular,
# which cost three rebuilt arches to learn.
ASSETS = os.path.join(ROOT, "assets", "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

TRI_BUDGET = 10000


# --------------------------------------------------------------------------- parts

def counter(width, depth, height, mat="wood"):
    """
    The board you put things on, and the one part every variant needs.

    Thick. A 4cm plank is a shelf; this is a bench top, and at eye height the
    thickness is most of what says "solid" rather than "cardboard".
    """
    box((width, depth, 0.10), (0.0, 0.0, height), mat, hit=True)


def leg(x, y, height, thick=0.11, mat="wood"):
    box((thick, thick, height), (x, y, height * 0.5), mat)


# --------------------------------------------------------------------------- stalls

def trestle():
    """
    Counter on crossed legs, slanted roof on two back posts.

    The roof is four separate planks with gaps, not one plate. A single slab
    across the top is a lid and reads as a table turned upside down - the gaps are
    what make it a roof you could stand under.
    """
    counter(1.90, 0.72, 0.98)

    # Crossed legs rather than four uprights, so the front has an X in it and does
    # not read as a box on stilts.
    for side in (-0.78, 0.78):
        box((0.10, 0.10, 1.16), (side, -0.22, 0.50), "wood", rot_x=22.0)
        box((0.10, 0.10, 1.16), (side, 0.22, 0.50), "wood", rot_x=-22.0)

    # Back posts carrying the roof, taller than the counter by a good margin.
    for side in (-0.86, 0.86):
        leg(side, 0.36, 2.06, 0.13)

    # Front posts, shorter - the roof slants forward and down.
    for side in (-0.86, 0.86):
        leg(side, -0.36, 1.72, 0.12)

    # The beam each end of the roof sits on.
    box((2.00, 0.12, 0.12), (0.0, 0.36, 2.04), "wood")
    box((2.00, 0.12, 0.12), (0.0, -0.36, 1.70), "wood")

    # Four planks with air between them, laid down the slope.
    for i, x in enumerate((-0.72, -0.24, 0.24, 0.72)):
        box((0.42, 0.94, 0.07), (x, 0.0, 1.90 + (i % 2) * 0.012), "bark",
            rot_x=-20.0)

    # A lantern on the front left post. One warm point, not a string of them.
    orb(0.10, (-0.86, -0.36, 1.62), "core", subdivisions=1)


def stump():
    """
    A felled trunk as the counter, awning slung between two leaning poles.

    The trunk is one big cylinder and everything else hangs off it. Nine sides
    reads as round; an even count presents a flat face to the camera and the whole
    thing goes back to looking like a crate.
    """
    taper(0.52, 0.48, 0.94, (0.0, 0.0, 0.47), "bark", sides=9, rot_x=1.5)

    # The cut face, proud of the trunk so it catches light as a separate surface.
    disc(0.50, 0.07, (0.0, 0.0, 0.96), "wood", sides=9, rot_x=0.0)

    # A second, lower stump beside it - two masses read as a place, one reads as
    # an obstacle.
    taper(0.34, 0.31, 0.62, (0.78, 0.10, 0.31), "bark", sides=9, rot_x=-2.0)
    disc(0.33, 0.06, (0.78, 0.10, 0.64), "wood", sides=9, rot_x=0.0)

    collide((0.0, 0.0, 0.47), (1.00, 0.96, 0.94))

    # Two poles leaning away from each other, carrying the awning.
    box((0.09, 0.09, 2.10), (-0.92, 0.30, 1.02), "wood", rot_y=13.0)
    box((0.09, 0.09, 1.94), (0.96, -0.34, 0.94), "wood", rot_y=-11.0)

    # The awning: three overlapping planks, sagging front to back.
    for i, y in enumerate((-0.30, 0.02, 0.34)):
        box((2.10, 0.40, 0.06), (0.0, y, 1.86 - abs(i - 1) * 0.05), "moss",
            rot_x=6.0 * (i - 1))

    orb(0.09, (0.96, -0.34, 1.86), "core", subdivisions=1)


def frame():
    """
    A heavy A-frame with the counter hung inside it.

    Tall and narrow where the others are wide, which is the whole reason it is in
    the set. The two legs meet above the counter and the lantern hangs in the
    crotch, so the silhouette is a triangle with a bar across it.
    """
    box((0.16, 0.16, 2.40), (-0.62, 0.0, 1.18), "wood", rot_y=15.0)
    box((0.16, 0.16, 2.40), (0.62, 0.0, 1.18), "wood", rot_y=-15.0)

    # A second pair behind, so it is a frame rather than a flat cutout.
    box((0.14, 0.14, 2.30), (-0.58, 0.62, 1.14), "wood", rot_y=15.0)
    box((0.14, 0.14, 2.30), (0.58, 0.62, 1.14), "wood", rot_y=-15.0)

    # The ridge beam joining the two A's at the top.
    box((0.14, 0.80, 0.14), (0.0, 0.31, 2.34), "wood")

    # Cross braces, one each side, at different heights so it is not a ladder.
    box((1.34, 0.10, 0.10), (0.0, 0.0, 1.72), "bark", rot_y=2.0)
    box((1.24, 0.10, 0.10), (0.0, 0.62, 1.52), "bark", rot_y=-2.0)

    counter(1.42, 0.66, 1.02)

    # Legs under the counter, short and set in from the ends.
    for side in (-0.52, 0.52):
        leg(side, 0.0, 1.02, 0.10)

    orb(0.11, (0.0, 0.16, 2.14), "core", subdivisions=1)


def cairn():
    """
    A stone shelf under a timber lintel. Squat, roofless, no legs.

    The only one in the set with no vertical timber carrying anything, so it reads
    as older than the others - something that was already here and got used, which
    is the right note for a land wight that turned up uninvited.
    """
    # Two stacks of stone, deliberately uneven, carrying the shelf.
    for side in (-0.74, 0.74):
        box((0.56, 0.62, 0.42), (side, 0.0, 0.21), "stone", rot_z=6.0)
        box((0.50, 0.56, 0.36), (side, 0.04, 0.58), "stone", rot_z=-9.0)
        box((0.46, 0.52, 0.22), (side, -0.03, 0.86), "stone", rot_z=4.0)

    collide((-0.74, 0.0, 0.48), (0.60, 0.66, 0.96))
    collide((0.74, 0.0, 0.48), (0.60, 0.66, 0.96))

    # The shelf, one heavy slab bridging them.
    box((2.10, 0.78, 0.16), (0.0, 0.0, 1.04), "stone", rot_z=1.0, hit=True)

    # A timber lintel over it on two short uprights - the only wood in the piece.
    for side in (-0.82, 0.82):
        box((0.13, 0.13, 0.72), (side, 0.24, 1.44), "wood")

    box((1.92, 0.15, 0.15), (0.0, 0.24, 1.84), "wood", rot_y=1.5)

    # Moss on the stone, in two large patches rather than a scatter of small ones.
    box((0.66, 0.40, 0.06), (-0.60, -0.22, 1.13), "moss", rot_z=8.0)
    box((0.48, 0.34, 0.06), (0.72, 0.20, 1.13), "moss", rot_z=-5.0)

    orb(0.10, (0.82, 0.24, 1.74), "core", subdivisions=1)


def stump_hollow():
    """
    The trunk itself is the way through: hollowed out, and the hollow is lit.

    Closest to the original stump, and the smallest change that answers "trader of
    things between realms" - the goods come up out of the tree rather than being
    laid on it. shell() is doing the work here, an open frustum with both caps
    deleted, so the light inside is genuinely inside rather than painted on.
    """
    # The outer trunk, wider than the original so the hollow has somewhere to be.
    shell(0.62, 0.56, 1.06, (0.0, 0.0, 0.53), "bark", sides=9)

    # The lit core, standing proud of the shell rather than sunk in it. The first
    # pass buried it: a full disc laid across the top as a rim is a lid, which is
    # the rule this repo already had, and it hid the one thing the design exists
    # to show. So the light comes up out of the opening instead.
    taper(0.42, 0.34, 1.26, (0.0, 0.0, 0.66), "core", sides=9)

    # The rim is four blocks round the outside, not a plate across the middle.
    for i in range(4):
        angle = math.radians(45.0 + 90.0 * i)
        box((0.34, 0.20, 0.11),
            (math.cos(angle) * 0.54, math.sin(angle) * 0.54, 1.04), "wood",
            rot_z=math.degrees(angle))

    collide((0.0, 0.0, 0.53), (1.28, 1.28, 1.06))

    # Roots gripping the ground, so it is a living stump rather than a barrel.
    roots((0.0, 0.0, 0.10), 1.02, count=6, length=0.44, thick=0.10)

    # The counter, a slab laid across the rim and overhanging the front.
    box((1.66, 0.60, 0.12), (0.0, -0.34, 1.10), "wood", rot_z=-2.0, hit=True)

    # One leaning pole and a short awning, kept from the original silhouette so the
    # family resemblance survives.
    box((0.09, 0.09, 2.10), (-0.94, 0.26, 1.04), "wood", rot_y=12.0)
    for i, y in enumerate((-0.18, 0.16)):
        box((1.60, 0.44, 0.06), (-0.30, y, 1.94 - i * 0.04), "moss",
            rot_x=7.0 * (i - 0.5), rot_y=6.0)


def stump_split():
    """
    A trunk split down the middle, the halves stood apart, the gap lit.

    The threshold idea in stump form: two masses with somewhere else between them,
    and a plank bridging the gap as the counter. Reads as a door far more than the
    hollow does, and keeps the grown-not-carpentered feel the stone version loses.
    """
    # Two halves, leaning away from each other at the top so the gap is a wedge.
    # A parallel gap reads as two separate posts; a wedge reads as one thing opened.
    taper(0.50, 0.42, 1.72, (-0.60, 0.0, 0.86), "bark", sides=9, rot_y=-7.0)
    taper(0.50, 0.42, 1.72, (0.60, 0.0, 0.86), "bark", sides=9, rot_y=7.0)

    # The split faces, pale where the bark is dark - a split trunk shows its inside.
    box((0.10, 0.78, 1.60), (-0.34, 0.0, 0.86), "wood", rot_y=-7.0)
    box((0.10, 0.78, 1.60), (0.34, 0.0, 0.86), "wood", rot_y=7.0)

    collide((-0.60, 0.0, 0.86), (1.00, 0.90, 1.72))
    collide((0.60, 0.0, 0.86), (1.00, 0.90, 1.72))

    # The gap, lit. Wider than the first pass, which left a slot you had to be
    # looking for - the whole design is "there is somewhere else between these two
    # halves" and a 30cm slot at four metres is a crack, not a doorway.
    box((0.46, 0.06, 1.42), (0.0, 0.20, 0.96), "core")

    roots((-0.52, 0.0, 0.10), 0.82, count=5, length=0.40, thick=0.09)
    roots((0.52, 0.0, 0.10), 0.82, count=5, length=0.40, thick=0.09)

    # The counter bridging the two halves, and proud of both.
    box((1.90, 0.66, 0.13), (0.0, -0.22, 1.04), "wood", rot_z=1.5, hit=True)

    # A branch arching over the gap, tying the halves together at the top.
    #
    # Starts near vertical and curves over, which is not what the first pass did:
    # limb's pitch is measured off straight up, so the 52 degrees it was given
    # started it half fallen over already and it never crossed the gap. Low pitch
    # plus a strong positive curve is an arch; high pitch is a splay.
    # yaw 90 leans it towards +x, so this one grows from the left half rightwards.
    #
    # Sized off the gap rather than guessed. Horizontal travel is roughly
    # length * sin(mean pitch), and it has to cross 0.96m to land on the far half -
    # so a mean pitch near 70 degrees wants a length near 1.1, and the curve has to
    # take the pitch past 90 by the end or the branch finishes still climbing,
    # which is what left it hanging in mid-air over the gap.
    limb((-0.48, 0.10, 1.62), 1.15, 5, 0.11, 0.05, 20.0, 90.0, 25.0, "bark")


def stump_roots():
    """
    A low stump under an arch of its own roots, with the counter slung inside.

    The one that is a place rather than an object. The arch is what a doorway looks
    like when a tree makes it, so it answers the realms brief without a single
    worked stone - and it is the only version of the five where the thing you walk
    up to is taller than it is wide.
    """
    # A low, wide stump. Deliberately squat: the arch above is the silhouette and a
    # tall trunk would compete with it.
    taper(0.66, 0.60, 0.86, (0.0, 0.10, 0.43), "bark", sides=9, rot_x=2.0)
    disc(0.62, 0.08, (0.0, 0.10, 0.88), "wood", sides=9)

    collide((0.0, 0.10, 0.43), (1.32, 1.26, 0.86))

    # Four roots leaving the stump and meeting overhead. Long, curved hard, and only
    # four - eight would be a birdcage.
    #
    # The first pass gave these pitch 74, which starts a limb three quarters of the
    # way to horizontal. They left the stump sideways, curved outwards and ended in
    # mid-air pointing up: the piece read as two hands rather than an arch. Pitch is
    # measured off vertical, so an arch wants a small one and a strong curve, and
    # yaw 90 / -90 is what sends each pair towards the middle instead of away.
    # Sized the same way as the split's branch. Each root starts 0.56 out and has to
    # arrive over the middle, so horizontal travel is 0.56 = length * sin(mean
    # pitch); a mean near 25 degrees gives length 1.35 and a rise of 1.2m, which is
    # an arch you could stand under. The previous numbers averaged 50 degrees and
    # spent the whole limb going sideways, which is why it came out as a crown.
    for x, y, yaw in ((-0.56, -0.06, 90.0), (0.56, -0.06, -90.0),
                      (-0.46, 0.42, 90.0), (0.46, 0.42, -90.0)):
        limb((x, y, 0.64), 1.35, 6, 0.14, 0.06, 5.0, yaw, 8.0, "bark")

    # Moss where the roots meet, hiding the join - four cones converging on a point
    # is the one place this shape can look assembled.
    orb(0.30, (0.0, 0.18, 1.86), "moss", subdivisions=1, stretch=0.7)

    # The counter, slung across the front of the stump inside the arch.
    box((1.72, 0.56, 0.13), (0.0, -0.40, 0.94), "wood", rot_z=-1.5, hit=True)

    for side in (-0.66, 0.66):
        box((0.14, 0.16, 0.90), (side, -0.44, 0.45), "wood", rot_z=3.0)


def threshold():
    """
    A standing-stone doorway with the counter set across the opening, and the gap
    lit from the far side.

    Built for "a trader of things between realms" rather than for a market. The
    other four are shops - a counter, a roof, somewhere to put your elbows - and a
    shop is the wrong idea for something that deals in what is not from here. This
    is a door with a shelf in it, and the goods arrive through the door.

    The lit plane across the opening breaks the rule that a full-area plate is a
    lid, on purpose. That rule is about structure reading as closed; this is not
    structure, it is the light of somewhere else, and it is inset well behind the
    frame so the uprights and lintel still read as the thing you could walk
    through if the counter were not there.
    """
    # Two uprights, leaning in a little so the opening tapers. Rough stone, three
    # stacked masses each rather than one clean column - a single box reads as a
    # gatepost from a garden centre.
    for side, lean in ((-0.86, 3.0), (0.86, -3.0)):
        box((0.46, 0.52, 1.10), (side, 0.0, 0.55), "stone", rot_y=lean, rot_z=5.0)
        box((0.42, 0.48, 0.94), (side * 1.02, 0.03, 1.54), "stone", rot_y=lean,
            rot_z=-7.0)
        box((0.38, 0.44, 0.52), (side * 1.04, -0.02, 2.22), "stone", rot_y=lean,
            rot_z=3.0)

        collide((side, 0.0, 1.20), (0.50, 0.56, 2.40))

    # The lintel, overhanging both sides. An overhang is most of what separates a
    # doorway from a rectangle.
    box((2.46, 0.62, 0.34), (0.0, 0.0, 2.62), "stone", rot_z=1.0, rot_y=1.0)

    # The far side, seen through the gap. Inset deep and slightly narrower than the
    # opening, so the frame always has stone on both edges of it.
    box((1.28, 0.06, 1.34), (0.0, 0.22, 1.72), "core")

    # The counter, across the opening and proud of it at both ends.
    box((2.10, 0.74, 0.14), (0.0, -0.10, 1.00), "wood", rot_z=-1.0, hit=True)

    # One bracket under each end, so the slab is carried rather than floating.
    for side in (-0.74, 0.74):
        box((0.16, 0.42, 0.16), (side, -0.08, 0.88), "wood", rot_x=6.0)

    # Marks cut into the uprights. Three, large, and only on the near faces -
    # a scatter of small ones reads as damage rather than as writing.
    for side, height in ((-0.86, 1.36), (0.86, 1.72), (-0.86, 2.10)):
        box((0.12, 0.05, 0.30), (side * 1.03, -0.26, height), "core", rot_z=4.0)


# The stump won. These are versions of it rather than a fresh set - same trunk, same
# grown-not-carpentered feel, each taking a different answer to "where does the
# between-realms part show". The originals stay in the tuple because a pick is easier
# against what it came from.
DESIGNS = (
    ("stump", stump),
    ("stump_hollow", stump_hollow),
    ("stump_split", stump_split),
    ("stump_roots", stump_roots),
    ("trestle", trestle),
    ("frame", frame),
    ("cairn", cairn),
    ("threshold", threshold),
)


def build(label, maker):
    clear_scene()
    maker()

    obj = finish("grove_market_" + label)
    tris = len(obj.data.polygons)

    if tris > TRI_BUDGET:
        raise SystemExit("FAIL %s is %d tris, over the %d budget for a buildable"
                         % (label, tris, TRI_BUDGET))

    export(obj, "grove_market_" + label, ASSETS)
    write_col(os.path.join(ASSETS, "grove_market_" + label + ".col"))

    print("DESIGN_OK grove_market_%s tris=%d" % (label, tris))
    return tris


def preview(label, maker):
    clear_scene()
    maker()
    finish("preview")

    # Half the usual emission. At the default the lit opening on the threshold
    # clipped to white and stopped reading as a colour at all - the same thing the
    # heartwood icon did, and the same fix: a preview has to keep its hue, and the
    # glow is implied by the colour rather than by blowing the channel out.
    tint(strength=0.5)

    stage_scene()

    # Eye height, and far enough back that the tallest piece fits with margin. The
    # threshold is 2.8m and the first pass cut its lintel off, which is exactly the
    # detail a pick has to be able to see. Same camera for all five, because a set
    # judged at different distances is not a set.
    reference_cube((2.20, -0.70, 0.5))
    camera((-3.60, -4.40, 1.70), (0.0, 0.0, 1.25), lens=42)

    render(os.path.join(PREVIEWS, "grove_market_" + label + ".png"),
           width=660, height=560)


def main():
    os.makedirs(PREVIEWS, exist_ok=True)

    for label, maker in DESIGNS:
        build(label, maker)
        preview(label, maker)


main()
