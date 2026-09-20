"""
CELLAR - the stowing post's GROW upgrade, second round.

    blender --background --python tools/craft/grow2_cellar_designs.py

The first round offered three pieces of storage furniture standing next to the post and
all three were rejected for the same reason: a shelf beside a shelf says "another shelf",
not "more room". This one is not furniture at all. It is a way down.

A stairhead: a stone-lined mouth in the ground beside the post, its timber hatch thrown
back off the opening, and the floor falling away in two steps into a low roofed passage
that runs on under the lintel and out of sight. The room the upgrade buys is underground,
so the piece itself is almost all mouth - a kerb of stone, a slab of planks and a dark
doorway. Nothing here is a container, which is the point: it promises a cellar rather than
showing you another box.

Why it is built the way it is:

  * The hatch is HINGED AT THE FAR EDGE and thrown right back, not sideways and not left
    standing. A trapdoor hinged on the left or right presents its own thickness to a
    player in front of it - 5cm of edge, a stick planted in the ground - and on a mouth
    a metre across it would swing a metre and a quarter out to the side, doubling the
    piece's width. Hinged at the back it can only go one way, and how far it goes is the
    difference between two readings: an audit of the first version called it a chest with
    its lid standing open, which it was, because a leaf twenty degrees past vertical IS a
    lid. At 43 degrees off the ground, resting on the spoil, it is a door somebody threw
    back. The ring moved off centre in the same pass, for the same reason - a ring in the
    middle of an upright panel is vanilla chest hardware, whatever it is attached to.
  * The hole is a HOLE, not a lidded box. CLAUDE.md's rule is that a plate across an
    opening is a lid however it is dressed, so the mouth carries nothing at all: the near
    kerb is low enough to see the floor over, the areaway is open to the sky, and the only
    thing spanning anything is the head beam and the roof behind it, which cover the
    passage rather than the pit.
  * Nothing on it has a clean edge along the ground. The stonework is banked round with
    half-buried spoil, because a rectangular mass with a straight foot and a shadow under
    it sits ON the grass, and this is supposed to be cut INTO it. It is the only lie
    available: a buildable cannot touch the heightmap, so the ground cannot actually open.
  * NOTHING GOES BELOW z=0. A buildable piece does not cut the terrain - the heightmap
    stays where it is - so a shaft sunk under the ground would have grass rendering across
    its floor. The depth is bought above ground instead: the side walls stand half a metre
    proud, the floor falls twice inside them, and the last drop is a threshold under a
    head beam into a passage roofed end to end and closed behind. That recess is dark
    because it is roofed, not because anything is painted black; there is no dark group to
    paint it with, since the runtime skins "wood", "iron" and "stone" off vanilla donors
    and falls back to wood for anything else. Two numbers govern that dark and they pull
    against each other: the sun drops 0.83m for every metre it runs into the opening, so a
    passage is only black beyond H/0.83 of its own doorway height - which makes a GENEROUS
    doorway a LIGHTER hole, the opposite of the instinct. And the spoil the hatch rests on
    was for three rounds banked across the back, competing with the passage for the same
    ground; heaped on the ROOF instead it stops competing, and the floor can run as far
    back as the dark needs.
  * Three groups rather than two. CLAUDE.md warns that three palettes are three objects'
    worth of paint - but the post beside it is already wood, iron and stone, and matching
    it exactly is the family resemblance. A fourth would be the mistake.

It houses no spirit, so no heartwood and no light: the two upgrades that cost a heartwood
show one, and this one showing none is how a player tells the kinds apart at a glance.

Lighting is CLAUDE.md's: sun 1.4, fill 0.35, world 0.28, Standard view transform.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
# WOOD_TILT is imported rather than restated, so this piece is jittered by the same
# carpenter as the post it stands beside.
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math
import random

from mathutils import Euler, Matrix, Vector

from vhbuild import (bevel_all, box, camera, clear_scene, collide, export, finish,
                     material, ring, taper, tint, write_col, COLLIDERS)

from post_heartwood import GLOW, WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

NAME = "grow2_cellar"

# The stowing post that shipped: the neighbour in every render.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")

# Where the piece stands relative to the post. The post is 1.10 wide, so its side is at
# x -0.55; at -1.50 the piece's centre is 1.5m off the post's, which leaves a walkable
# third of a metre between the kerb and the post's plinth.
BESIDE = -1.50

LIMIT = 10000

# --------------------------------------------------------------------------- the pit
#
# One table of numbers, because every part of this piece is measured off the hole rather
# than off itself. Changing the interior width below moves the kerb, the steps, the
# lintel and the hatch together; changing them one at a time is how a 5cm gap appears.

WALL = 0.17          # thickness of the dressed kerb: a lip, not a parapet
INNER_X = 0.50       # the opening's half width, so the mouth is 1.00 across
KERB_Z = 0.46        # top of the side walls, which the head beam is bedded into
NEAR_Z = 0.28        # top of the near kerb - see below, this number is the whole piece
AREA_Z = 0.19        # the areaway tread, one step down from the kerb
FLOOR_Z = 0.01       # and the passage floor, a deeper step down, running into the dark

NEAR_Y = 0.52        # centre of the near kerb, 0.44..0.60
TREAD_Y = 0.30       # centre of the one lit tread, 0.16..0.44
FLOOR_Y = -0.295     # centre of the passage floor, -0.76..0.17
LINTEL_Y = 0.09      # centre of the timber head beam over the doorway, -0.04..0.22
ROOF_Y = -0.40       # centre of the stone slab roofing the passage behind it
BACK_Y = -0.75       # centre of the back wall that closes the passage

DOOR_Y = 0.22        # the passage mouth: the head beam's front face, overhanging it
DOOR_Z = 0.46        # and its underside, so the dark doorway is 1.00 x 0.45

HINGE_Y = -0.04      # the hatch turns on the head beam's back shoulder
HINGE_Z = 0.64       # and lies on its top when shut

# How far the hatch reaches, and how wide. Both came down from 0.62 in the last pass,
# and it is a composition fix rather than a fit one. Laid back at 43 degrees the leaf is
# the brightest and largest single surface on the piece, and at 0.62 it took seven degrees
# of a standing player's view where the hole took under four - so whatever the stone did,
# the eye landed on a big pale board. Shut, 0.54 by 1.12 still covers a mouth that is 1.00
# across and open from y 0.16 to the doorway, which is all a hatch has to cover.
LEAF = 0.54
LEAF_X = 0.56        # half width, a hand's breadth past the mouth on each side

# Degrees from shut. It was 112 - twenty past vertical - and the audit was right about
# what that looked like: a slab standing upright and very nearly flush with the back of
# the opening, which is a CHEST WITH ITS LID UP and nothing else. A lid stands; a hatch
# thrown open lies back on whatever is behind it.
#
# 143 was tried first and went too far the other way: at 37 degrees off the ground the
# leaf is so foreshortened that it reads as a flat board laid ON the stone - a table top,
# or a lid put down - and it still took seven degrees of the frame where the hole took
# under two. 137 is the settlement. It is comfortably past the 45 the audit asked for, so
# it is plainly thrown back and not standing; its tip comes down onto the spoil bank, so
# it is resting on something rather than hinged in mid air; and it keeps enough face
# turned to the player to be read as planks and battens rather than as a plane.
#
# The cost is written here because it is not obvious: the steeper the leaf, the HIGHER
# the point where it meets the bank, so the bank has to grow to catch it. At 143 it lands
# at 0.92 and at 132 it lands at 1.01, which is a stone wall behind a hole. 137 lands at
# 0.98 and the bank's top sits just under the leaf's own edge from a standing eye, so the
# stone stays hidden behind the timber that rests on it.
LEAN = 137.0

# Why the near kerb is low and the doorway is large, in one paragraph, because the first
# two rounds of this piece got it wrong in both directions.
#
# A player stands 3.5m off with their eye at 1.7m and a little to one side, so the line
# of sight into the pit falls at about 22 degrees. Over a lip 0.20 high it needs half a
# metre of run to reach the floor, which is the whole pit - so a kerb any taller hides
# the inside completely and the piece becomes a closed stone trough. That was the first
# render: it read as a planter.
#
# And the inside cannot be made dark by shading it. The sun here comes in from behind the
# camera at 38 degrees and floods a recess this shallow from lip to floor, so the only
# dark on the piece is the part that is actually roofed. The second render learned that
# and then lost it again by filling the pit with steps: the black was a slot two fingers
# high behind two lit blocks, and a slot is not a hole.
#
# So the steps are spent on getting DOWN to the doorway - kerb, areaway, passage, three
# levels in 14cm - and everything else is given to the doorway itself, which is the full
# width of the mouth and a third of a metre tall. That rectangle is the hole. The flight
# proper is inside it, unlit and barely modelled, which is also the honest answer: the
# cellar is underground and the piece is only its mouth.
#
# The fourth round added the thing all three earlier ones were missing: ONE LIT TREAD.
# The audit's words were that no step is visible from standing and the whole interior is
# a flat dark rectangle, so the promise of depth rests entirely on the black - and black
# is equally the inside of a box. The numbers above are now chosen off the sightline
# rather than off the section. With the lip at 0.22 and its front face at y 0.43, the
# grazing ray drops 0.482 per metre: at y 0.27 it is still 0.143 above the ground and the
# tread is at 0.15, and by y 0.02 it has fallen to 0.022 and the floor is at 0.01. So the
# whole 25cm tread is lit and in view, the riser at its back edge is exactly where sight
# runs out, and the black starts at a nosing instead of at the lip. One step you can see
# is worth more than three you cannot.
#
# And the PASSAGE HAD TO GO BACK TO BEING DEEP, which cost a round to rediscover. The
# fourth round's first cut spent the depth on a generous doorway - 1.00 by 0.45 with only
# 0.55m of floor behind it - and the render came back with the entire inside lit a flat
# mid grey, no black anywhere. The arithmetic is unforgiving and it is worth writing
# down: the sun travels (0.269, -0.740, -0.616), so it comes in over the player's
# shoulder and drops 0.83m for every metre it runs into the hole. A doorway H tall is
# therefore floodlit for H/0.83 metres of floor - 0.54m at H=0.45, which was the whole
# passage. Making the doorway BIGGER makes the hole LIGHTER, which is the opposite of
# every instinct. So the head is back down to 0.39 and the floor runs 0.67m: lit to y
# -0.39, dark from there to the bank, and the far wall never sees the sun at all.


# --------------------------------------------------------------------------- parts

def beam(a, b, width, mat, depth=None, over=0.0, tilt=WOOD_TILT):
    """
    A square timber from a to b, run on by `over` at both ends.

    Square, not round, because everything on the post is sawn: a round stay on this piece
    would be the one thing in the pair that was not made by the same hands. The turn is
    the shortest one from +z onto the timber, so a prop that leans in a single plane is
    not twisted about its own length as well.
    """
    a = Vector(a)
    b = Vector(b)
    axis = b - a

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(a + b) * 0.5)
    obj = bpy.context.active_object
    obj.scale = (width, width if depth is None else depth, axis.length + over * 2.0)
    turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized())
    lean = Euler((math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)))).to_quaternion()
    obj.rotation_euler = (lean @ turn).to_euler()
    obj.data.materials.append(material(mat))
    return obj


# The swing, and the hinge line it turns about. Module level because the stay has to be
# aimed at a point on the door, and the only way to be sure it touches is to put that
# point through the same matrix the door went through.
HINGE = Vector((0.0, HINGE_Y, HINGE_Z))
SWING = (Matrix.Translation(HINGE)
         @ Matrix.Rotation(math.radians(LEAN), 4, "X")
         @ Matrix.Translation(-HINGE))


def on_leaf(x, along, up):
    """
    A point on the shut hatch: `along` out from the hinge, `up` off its face.

    Measured from the hinge rather than from the world, because the swing turns about
    that line. Modelled at y=0 instead - the obvious way, and the first way here - every
    part carries a 30cm arm it should not have, and the whole door lifts off the stone
    and hangs in the air behind the hole. It looked like a signboard on a post.
    """
    return (x, HINGE_Y + along, HINGE_Z + up)


def swung(build):
    """Build the hatch lying shut over the hole, then throw it back."""
    before = set(bpy.data.objects)
    build()
    made = [obj for obj in bpy.data.objects if obj not in before]

    # The depsgraph is behind: box() sets scale and rotation after the add operator, so
    # matrix_world has to be brought up to date before it can be read and re-based.
    bpy.context.view_layer.update()
    for obj in made:
        obj.matrix_world = SWING @ obj.matrix_world
    return made


# --------------------------------------------------------------------------- design

def kerb():
    """
    The lip: two side walls, a lower one across the front, and the mound behind.

    Four big stones rather than a course of small ones. A kerb built of blocks reads as
    masonry a mason laid; this is a hole someone lined with whatever came out of it, and
    at eye height the difference between four stones and forty is confetti.
    """
    for side in (-1, 1):
        box((WALL, 1.38, KERB_Z), (side * (INNER_X + WALL * 0.5), -0.09, KERB_Z * 0.5),
            "stone", tilt=WOOD_TILT, hit=True)

    # The near kerb: the lip you step onto, low enough to see the floor over.
    box((INNER_X * 2.0 + WALL * 2.0, 0.16, NEAR_Z), (0.0, NEAR_Y, NEAR_Z * 0.5),
        "stone", tilt=WOOD_TILT, hit=True)

    # The head beam over the doorway, and it is TIMBER now. Three things bought with one
    # part. It is 21cm deep in section, so it reads as a baulk somebody dropped in rather
    # than as a course of stone. It projects 6cm forward of the riser, so the sun coming
    # in over the player's shoulder throws its shade across the head of the opening -
    # which is most of what is left to deepen the dark, the terrain being uncuttable and
    # there being no black material to paint with. And it puts a metre and a sixth of
    # wood across the widest part of the piece: the audit had this as the only near-white
    # thing in the lineup, seven tenths stone by area where its siblings are one tenth,
    # and the cure is to give the timber more of the FACE rather than to touch a tint the
    # whole family shares and which is correct.
    box((INNER_X * 2.0 + WALL * 1.7, 0.26, 0.18), (0.0, LINTEL_Y, 0.55),
        "wood", tilt=WOOD_TILT)

    # Behind it, a stone slab roofing the passage from the head beam to the bank. Left
    # open to the sky, the sun came in over the back and lit the far end, and a lit far
    # end is a niche - you can see it stop. Roofed the whole way, the one thing visible
    # through the doorway is a floor running into black and nothing says where it ends.
    # Stone and not timber only because no part of it is ever seen.
    box((INNER_X * 2.0 + WALL * 1.2, 0.76, 0.16), (0.0, ROOF_Y, 0.56),
        "stone", tilt=WOOD_TILT)

    # The back wall, closing the passage where the sun has long since stopped reaching.
    box((INNER_X * 2.0 + WALL * 1.2, 0.14, 0.64), (0.0, BACK_Y, 0.32), "stone",
        tilt=WOOD_TILT)

    # Timber cheeks lining the areaway, standing against the inside of each side wall.
    # More wood on the face again, and they turn the mouth from a hollow cut in a block
    # into an opening somebody lined and shored - which is the difference between a
    # trough and a way down.
    for side in (-1, 1):
        box((0.05, 0.28, 0.31), (side * (INNER_X - 0.03), 0.30, 0.345), "wood",
            tilt=WOOD_TILT)

    # The spoil, heaped ON TOP OF THE ROOF rather than banked across the back, and this
    # is the edit that unlocked the whole piece.
    #
    # The hatch has to come down and rest on something, and for three rounds that
    # something was a mass of stone standing at the back of the mouth - which meant the
    # bank and the passage were fighting for the same metre of ground. Every attempt to
    # make the hole darker wanted a DEEPER passage (the sun drops 0.83m per metre it runs
    # in, so only distance from the doorway buys black), and every attempt to lay the
    # hatch further back wanted the bank NEARER, since a leaf only reaches so far. The two
    # cannot both be had along one axis.
    #
    # Put the spoil on the roof and they stop competing: it is above the passage instead
    # of behind it, so the floor can run as far back as it likes underneath. It is also
    # the truer thing - what comes out of a hole gets piled over the hole - and it is what
    # the leaf would actually land on, since a cellar cut into rising ground has its door
    # opening back onto the mound. Three lumps at three heights, each turned differently;
    # one slab at one height is a wall, and a wall behind an opening with a plank leaning
    # on it is a shed.
    # Each lump is RAKED TO THE LEAF'S OWN ANGLE and placed off a point on its underside
    # rather than off the ground, which is the only way to be sure it touches. Flat-topped
    # boxes heaped to eye-judged heights went straight through the planks and stood out
    # above them like rocks on a roof - the leaf climbs 0.933m for every metre it runs
    # back, so a box whose top clears the timber at its back face fouls it at the front by
    # eight centimetres. Raked to 43 degrees the top face is parallel to the underside and
    # the whole of it beds against the door.
    rake = -(180.0 - LEAN)
    # The leaf's underside normal. Shut it is (0, 0, -1); a turn of LEAN about X takes
    # that to (0, sin, -cos) - and the minus on the cosine is not decoration. Written as
    # (0, sin, cos) it points DOWN through the door instead of out of it, so subtracting
    # half a stone along it seats every lump ON TOP of the timber rather than under it,
    # and the piece came out 1.62m tall with boulders sitting on the open hatch.
    normal = Vector((0.0, math.sin(math.radians(LEAN)), -math.cos(math.radians(LEAN))))

    for dx, along, wide, deep, thick, spin in ((-0.36, 0.44, 0.58, 0.44, 0.50, 5.0),
                                               (0.34, 0.50, 0.54, 0.46, 0.50, -4.0)):
        # +0.030, INSIDE the door's far face, not -0.045 on its near one. A thrown-back
        # hatch turns its shut-upper face away from the player, so that is the side the
        # spoil is on; seated against the underside instead, every lump sat between the
        # planks and their own battens and the render came back with the leaf apparently
        # made of stone with two brown bars across it. The 0.030 puts the top plane two
        # centimetres INSIDE a plank that is 0.05 through, rather than two millimetres
        # clear of it: clearance plus four degrees of seeded tilt is a lump that pokes
        # through the timber in patches, and an overlap cannot.
        touch = SWING @ Vector(on_leaf(dx, along, 0.030))
        seat = touch - normal * (thick * 0.5)
        box((wide, deep, thick), (dx, seat.y, seat.z), "stone",
            rot_x=rake, rot_z=spin, tilt=4.0)

    # And one lump behind the leaf's tip, placed in world space because nothing above it
    # constrains it. It was briefly seated off the leaf like the other two, at an `along`
    # past the leaf's own length - which is a point on the door's LINE and not on the
    # door, so the stone climbed to 1.76m and the piece came out taller than the post it
    # is meant to sit under. A seating helper only means anything inside the thing it
    # seats against.
    box((0.56, 0.34, 0.38), (-0.02, -0.80, 0.80), "stone", rot_x=-22.0, rot_z=7.0,
        tilt=5.0)

    # A skirt of spoil banked against the OUTSIDE of the kerb, and this is the fix for
    # the third thing the audit caught: the stone had a clean rectangular bottom edge
    # with a cast shadow under it, so the mass sat ON the grass instead of being cut into
    # it. Nothing modelled can dig - a buildable does not touch the heightmap - so the
    # only way to bed this into the ground is to break the line where it meets the
    # ground. These sit low, lean every which way, overlap the kerb by a third of their
    # own width, and their bottoms run a few centimetres under z=0 where the terrain
    # swallows them. No straight edge survives anywhere along the foot of the piece.
    #
    # Blocks rather than the rough cones tried first: at this size a seven-sided cone
    # reads as a lump of putty, and every stone in Valheim is flat faces and hard edges.
    #
    # Two hard limits on them, both learned in one render. They must stay SHORTER THAN
    # THE NEAR KERB, because the whole sightline above is a budget of a few centimetres
    # and a 28cm boulder standing in front of a 22cm lip does not decorate the hole, it
    # closes it - the first attempt at this skirt hid the mouth completely and the piece
    # came back reading as a bench over a rockfall. And they must stay TIGHT to the kerb:
    # at 0.4m across and set 0.2m proud they took the piece from 1.35 x 1.25 to 1.97 x
    # 1.70, wider than the post it is meant to be subordinate to. Half of each one is
    # inside the stone it leans on and no more than 10cm of it is outside.
    for dx, dy, wide, deep, tall, spin in ((-0.70, 0.34, 0.26, 0.28, 0.22, 24.0),
                                           (-0.70, -0.08, 0.24, 0.32, 0.26, -18.0),
                                           (-0.26, 0.58, 0.30, 0.20, 0.15, 11.0),
                                           (0.26, 0.60, 0.28, 0.18, 0.13, -27.0),
                                           (0.70, 0.30, 0.26, 0.30, 0.24, 15.0),
                                           (0.70, -0.12, 0.24, 0.32, 0.22, -21.0),
                                           (-0.66, -0.40, 0.26, 0.24, 0.32, 29.0),
                                           (0.66, -0.42, 0.24, 0.26, 0.30, -9.0)):
        box((wide, deep, tall), (dx, dy, tall * 0.5 - 0.05), "stone",
            rot_z=spin, rot_x=4.0, tilt=6.0)

    collide((0.0, -0.46, 0.52), (1.34, 0.72, 1.04))


def steps():
    """
    Kerb, areaway, passage: three levels falling away from the lip and under the lintel.

    Solid slabs, each sitting on the one below, rather than treads on stringers - this is
    stone cut into the ground, and a flight of floating planks would read as a ladder.
    Each overlaps its neighbour in y, because jitter on a butt joint opens a hairline of
    daylight that at eye height looks exactly like a missing step.

    The drop at the doorway is the one that matters. Without it the floor runs level into
    the dark and the passage is a cupboard; with it, the last lit thing a player sees is a
    nosing with nothing under it.
    """
    # The one tread that can actually be seen, and every number on it is set by the
    # sightline rather than by the section - see the long note above. 27cm deep, so it
    # is a tread and not a nosing, and it runs back to y 0.08 where the grazing ray
    # meets the floor. That edge is the last lit thing on the piece.
    box((INNER_X * 2.0 + 0.02, 0.28, AREA_Z), (0.0, TREAD_Y, AREA_Z * 0.5), "stone",
        tilt=WOOD_TILT)

    # The passage floor drops 14cm off that nosing, runs on under the lintel and stops at
    # the bank. It is the only part of the piece a player never sees the end of, which is
    # the whole trick: the cellar is wherever this floor goes.
    box((INNER_X * 2.0 + 0.06, 0.93, FLOOR_Z), (0.0, FLOOR_Y, FLOOR_Z * 0.5), "stone",
        tilt=WOOD_TILT)


def hatch():
    """
    The timber hatch, thrown back off the hole and propped.

    Three wide planks, not seven narrow ones: a door is a few big boards and a couple of
    battens, and the bevel is what draws the joints. Battens on the underside because
    that is the face a thrown-back hatch shows you, and the ring on that same face - a
    cellar hatch carries a pull inside as well as out, for closing it over your head.
    """
    def leaf():
        for index in range(3):
            # Planks running out from the hinge, overlapping: three at 0.48 across a 1.24
            # width leaves 7cm of lap between neighbours, so no joint can open. Three wide
            # boards, not seven narrow ones - a door is a few big pieces of timber.
            x = (index - 1) * (LEAF_X - 0.21)
            box((0.48, LEAF, 0.05), on_leaf(x, LEAF * 0.5, 0.025), "wood", tilt=WOOD_TILT)

        # Battens across the planks, on the underside, standing proud of them.
        for along in (0.22, LEAF - 0.13):
            box((LEAF_X * 2.0 - 0.04, 0.11, 0.05), on_leaf(0.0, along, -0.04), "wood",
                tilt=WOOD_TILT)

        # Hinge straps: iron over the planks and round the bottom edge, which is the one
        # detail that says door rather than board. They run back past the hinge line onto
        # the lintel, where the pintles below meet them.
        for x in (-0.38, 0.38):
            box((0.09, 0.38, 0.035), on_leaf(x, 0.11, 0.045), "iron", tilt=WOOD_TILT)

        # The pull, and it is OFF CENTRE, which is the smallest edit on this piece and
        # very nearly the most important. A ring in the middle of an upright plank panel
        # is not "a ring on a door" to anyone who has played this game - it is the exact
        # hardware a vanilla chest wears on its lid, and the audit read the whole piece as
        # a chest standing open largely because of it. Put out near a corner, where a hand
        # actually falls on a hatch you are pulling shut over your head from the steps, it
        # stops being a centred emblem and goes back to being a handle. Smaller, too: the
        # old one was 15cm across, which is a ring you would hang a lamp from.
        ring(0.065, 0.016, on_leaf(-(LEAF_X - 0.24), LEAF - 0.13, -0.055), "iron",
             rot_x=0.0)

    swung(leaf)

    # Pintles on the lintel for the straps to turn on.
    for x in (-0.38, 0.38):
        box((0.10, 0.17, 0.05), (x, HINGE_Y - 0.07, HINGE_Z - 0.03), "iron",
            tilt=WOOD_TILT)

    # No stay any more, and its absence is the point rather than a saving.
    #
    # A door thrown back has to rest on something or it falls shut. At 68 degrees off the
    # ground it rested on a prop, and the prop was a problem in itself: footed behind the
    # leaf it was hidden by the door it held, footed in front of it - which is where it
    # ended up - it only caught the leaf's corner, and the audit saw that clipping from
    # across the lineup. At 37 degrees the door does not need one. Its tip comes down onto
    # the spoil bank behind the mouth, resting on stone along most of its width, which is
    # what a cellar door thrown open actually does and is also why it now reads as thrown
    # open rather than propped up.

    tip = SWING @ Vector(on_leaf(0.0, LEAF, 0.0))
    collide((0.0, (HINGE_Y + tip.y) * 0.5, (HINGE_Z + tip.z) * 0.5),
            (LEAF_X * 2.0, abs(tip.y - HINGE_Y) + 0.14, tip.z - HINGE_Z))


def cellar():
    kerb()
    steps()
    hatch()


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


def post(x=0.0):
    """The shipped stowing post, imported with the same axes it was exported with."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=POST, forward_axis="Z", up_axis="Y")
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


def build():
    """Bevel per object, one segment, then join - finish() would bevel with two."""
    clear_scene()
    cellar()
    bevel_all(segments=1)
    return finish(NAME, bevel=False)


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# --------------------------------------------------------------------------- output

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    obj = build()
    colliders = len(COLLIDERS)

    # Into variants/ until it is picked, where the csproj cannot copy it.
    export(obj, NAME, VARIANTS)
    write_col(os.path.join(VARIANTS, NAME + ".col"))

    tris = triangles(obj)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]

    # Moved after export: the asset stays centred on its own origin.
    obj.location.x += BESIDE
    post()

    tint(GLOW)
    stage()
    # The camera stands on +y looking at -y, so -x is on the right of the frame: the
    # piece is there, and the cube stands beyond it where it can be read against the
    # kerb without hiding the mouth.
    reference_cube((BESIDE - 1.52, -2.10, 0.50))

    # Eye height, 3.5m back, 42mm, aimed between the post and the piece and low enough
    # that a standing player is looking down into the hole.
    camera((-1.00, 3.50, 1.70), (-1.00, -0.24, 0.66), lens=42)
    render(os.path.join(PREVIEWS, NAME + ".png"), 900, 700)

    icon()

    flag = "  OVER %d" % LIMIT if tris > LIMIT else ""
    print("DESIGN_OK %s tris=%d colliders=%d size=%.2fx%.2fx%.2f%s"
          % (NAME, tris, colliders, max(xs) - min(xs), max(ys) - min(ys),
             max(zs) - min(zs), flag))
    if tris > LIMIT:
        raise SystemExit("the CELLAR upgrade is over %d triangles" % LIMIT)


def icon():
    """
    The crafting post's icon rig: orthographic, transparent, its own two suns and its own
    exposure, 128px. Into previews/ until the piece is picked - an icon for a design still
    on offer is a render, not an asset.

    Steeper than the other upgrades' icons, at 21 degrees rather than 12. Those are tall
    pieces and are read against their own height; this one is a hole, and a hole seen from
    eye level is a line. From above, the mouth, the doorway and the thrown-back hatch are
    all in the frame at once.

    Not steeper still: at 34, and at 26, the doorway closed up into the kerb and the icon
    was a grey slab with a brown flap on it. The dark rectangle is the one thing on this piece that
    says what it is, so the elevation is chosen to keep it open.
    """
    clear_scene()
    bpy.ops.wm.obj_import(filepath=os.path.join(VARIANTS, NAME + ".obj"),
                          forward_axis="Z", up_axis="Y")
    imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    for obj in imported:
        obj.rotation_euler.z += math.radians(24.0)
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

    elevation = 33.0
    rad = math.radians(elevation)
    # What the camera actually sees: width across, and depth and height foreshortened
    # into one another. Sizing off x and z alone framed the piece by its hatch and cut
    # the far corners of the kerb off.
    across = hi[0] - lo[0]
    down = (hi[1] - lo[1]) * math.sin(rad) + (hi[2] - lo[2]) * math.cos(rad)
    radius = max(across, down) * 0.5

    tint()

    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(0.0, 3.0 * math.cos(rad), 3.0 * math.sin(rad)))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = radius * 2.04
    cam.rotation_euler = (math.radians(90.0 - elevation), 0.0, math.radians(180.0))
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(-1.6, 2.0, 1.6))
    key = bpy.context.active_object
    key.data.energy = 2.8
    key.rotation_euler = (math.radians(56.0), 0.0, math.radians(-148.0))

    # Half the fill and two thirds of the ambient the other upgrades' icons use. Those
    # are solid objects and want their far side readable; this one is a doorway, and at
    # 128 pixels a doorway that is merely shaded is a grey smudge. Turning the bounce
    # down is what makes it black, and black at that size is the only thing that carries.
    bpy.ops.object.light_add(type="SUN", location=(1.8, 1.8, -0.6))
    fill = bpy.context.active_object
    fill.data.energy = 0.45
    fill.rotation_euler = (math.radians(106.0), 0.0, math.radians(218.0))

    world = bpy.data.worlds.new("icon")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.55, 0.62, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.20

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = 128
    scene.render.resolution_y = 128
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = os.path.join(PREVIEWS, NAME + "_icon.png")
    bpy.ops.render.render(write_still=True)

    # Scene state, not render state: left on, the next preview in the same run gets a
    # white void for a sky and reads as a blown exposure.
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    print("ICON_OK %s radius=%.2f" % (NAME, radius))


main()
