"""
GROW, third round: a wall rack of hanging baskets.

    blender --background --python tools/craft/grow3_rack_designs.py

Two rounds are behind this one and both were refused, and between them they fence the
design in tightly enough that the fence IS the brief.

Round one offered a lean-to of bays, a hopper bin and a tiered A-frame shelf. All three
were storage carcasses - heavy, closed at the back, standing on the floor, asking to be
filled. That is exactly the thing this piece must not be, because the player never puts
anything into it: the extra slots it buys appear in the POST's own window. It is the
reason the post got bigger, not a second container. So: no doors, no lids, no drawers,
no hinge and no latch. Nothing on it may invite an open.

Round two offered a barrow, a yoke-and-panniers, a windlass and a cellar hatch, and the
answer was "i dont want machines. it should be furniture." So no wheels, no cranks, no
drums, no rope on a spool, nothing at rest that would move if you pushed it.

He then named the idea himself: a wall rack of hanging baskets. A pegged board with
woven baskets and bundles hung off it, standing beside the post. "Barely an object at
all" is how it was put to him and that is the target - openwork, hung things, daylight
straight through the middle of the silhouette. Furniture a woodsman made, in an evening,
out of what was to hand.

THREE VARIANTS OF THAT ONE IDEA, and the whole of the difference is the outline:

    leant   a flat slatted board tipped towards the post on a broad sill, its pegs in
            two rails - three at the head and two at the waist - with everything hanging
            clear in front of the face. A leaning rectangle: a panel, not a frame.

    rail    wide and low - one long rail between two short uprights, baskets strung
            along it in a row at three different drops and a bundle hung off the far end
            of the rail past the upright. The airiest of the three by a long way: above
            the rail there is nothing at all, and between the baskets you see the ground.

    horse   a shallow A - a drying horse, feet splayed wide, two rungs across the front
            of it and a ridge pole in the forks where the legs cross. A triangle with a
            triangle of daylight inside it.

WHAT THEY SHARE, because they have to belong to the post standing next to them:

  * Its four material groups and no more - sawn `wood` for the frames, `bark` for the
    woven work, `iron` at the joints, `rope` at every cord. Four is what the post wears
    (it swaps `core` for `rope` here, having no heartwood to show), and a fifth group
    would put a fifth vanilla prefab's palette into a piece that stands touching it.

  * Its jitter. WOOD_TILT is imported from the post's own script rather than restated,
    so the same carpenter with the same not-quite-square eye built both.

  * No light. This one houses no spirit, so there is no heartwood, no lantern and
    nothing that glows - which is also what keeps it subordinate to a post that does.

WHAT THE SECOND PASS'S RENDERS SAID, because every one of these came out of looking at
a picture rather than out of the plan:

  * THE BASKETS WERE PAILS. Four tall bands with alternate ones standing 24mm proud of
    their neighbours is not a coil, it is a hoop: a 24mm step on a 150mm vessel has the
    proportions of a cooper's band, and over a wide flat rim it reads as a bucket in
    every render and in all three icons. Worse than merely missing the note - a rack of
    wooden tubs is a rack of vessels you put things into, which walks straight back
    towards the container reading two rounds of refusal were about. A basket is now
    SIX TO NINE shallow coils, each one 7.5mm proud of the top of the coil beneath it
    and stepping the same way all the way up, so the profile is a ratchet rather than a
    bulge-and-pinch, and the rim is a slim lip 4% wider than the mouth instead of a
    band. The ridge is smaller and there are twice as many: that is the difference
    between coiling and staving, and it is worth the ~700 triangles a variant.

  * A TIMBER OR A STRAP WENT THROUGH A BASKET ON ALL THREE. Not a near miss in one
    place - the leant board's lower strap, the rail's end upright and the horse's front
    leg and lower rung, each of them arithmetic I had done by eye. Eye is not good
    enough for this, so it is no longer done by eye: every frame member registers an
    `obstacle` box and every hung thing `claim`s one, and the build DIES NAMING BOTH if
    they intersect. The three layouts below are what came back after that check stopped
    failing, and the numbers in them are load-bearing rather than taste.

  * THE CORDS ENDED UP INSIDE THE BOWLS. They were tied at 62% of the rim radius and
    20mm BELOW the rim plane, and then run on 30mm past that at both ends - so each one
    finished about 5cm down inside the basket, and the renders show pale rod ends lying
    in the bottoms of them. A cord now ties AT the rim, on the rim plane, and its
    overrun is asymmetric: 30mm into the peg, where it must disappear, and 10mm past the
    rim, where it disappears into the lip. Nothing of a cord is visible inside a bowl.

  * LEANT AND HORSE WERE THE SAME PICTURE. Both led with one big raking timber and side
    by side they read as one design twice. The lean is 8 degrees now instead of 13, the
    sill is wider than the board is tall is wide, and the slat heads are cut flush into
    the head ledger instead of standing past it in a fringe - so it is a panel leaning
    on a plinth, against a triangle standing on its own feet.

A note on the two peg rails, because the single stepped column they replaced is what
made the clipping unfixable. Pegs jut into exactly the plane the baskets hang in - a
basket is as deep as it is wide, so no amount of peg length puts one in front of the
others - which means a basket hung from a peg at one height WILL swallow any peg below
it that shares its column of x. Staggering left and right down the board only moves
which peg it eats. Two rails fix it by construction: the upper row hangs long and every
one of its baskets bottoms out above the lower row, and the lower row hangs below
everything. That is also how a real rack is built, for the same reason.

Lighting is CLAUDE.md's: sun 1.4, fill 0.35, world 0.28, Standard view transform. The
camera distance is computed from the span of post-plus-piece rather than typed, because
the three pieces are 1.1m, 1.4m and 1.5m wide and one hand-tuned distance would clip
two of them.
"""

import os
import sys

# Three levels: tools/craft -> tools -> the repo.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

sys.path.insert(0, TOOLS)
sys.path.insert(0, os.path.join(TOOLS, "stow"))

import bpy
import math
import random

from mathutils import Euler, Matrix, Vector

from vhbuild import (bevel_all, box, clear_scene, collide, export, finish, material,
                     shell, taper, tint, COLLIDERS, TINTS, write_col)

from post_heartwood import WOOD_TILT

ASSETS = os.path.join(ROOT, "assets")
VARIANTS = os.path.join(ASSETS, "variants")
PREVIEWS = os.path.join(ASSETS, "previews")

# The stowing post that shipped: the neighbour in every render, and the thing every one
# of these has to look smaller than.
POST = os.path.join(ASSETS, "stow_post_canopy.obj")
POST_HALF_W = 0.605      # measured off the .obj: x -0.597 .. 0.610
POST_HEIGHT = 1.740

LIMIT = 10000            # the hard cap a buildable piece may not cross
TARGET = 3600            # what we are aiming at, printed either way. It was 2500 before
                         # the coils; eight shallow bands where there were four costs
                         # about 700 a variant and it buys the one thing the piece is

# Clear floor between the post's side and the piece's nearest edge. A piece is dropped
# beside the post the way a chopping block is dropped beside a workbench - close enough
# to be read as belonging to it, far enough that a player can walk between the two.
CLEAR = 0.28
NEAR = -1.35             # preferred centre distance, overridden when the piece is wide

WIDTH, HEIGHT = 1000, 780
LENS = 42.0
SENSOR_HALF = 18.0       # 36mm sensor, horizontal fit
EYE = 1.70

# How far a basket's rim stands proud of its mouth. Slim: this used to be 1.105 with a
# band 20% of the basket's height behind it, and a wide flat rim over a bulging body is
# most of what made every one of them photograph as a pail.
RIM = 1.045


# ------------------------------------------------------------------- room to hang in
#
# A frame member registers an `obstacle`; a hung thing `claim`s a box and the build dies
# if the two meet. This exists because the second pass shipped a strap through one
# basket, an upright through another and a leg AND a rung through a third, all of them
# things I had checked by reading the numbers - and a render only shows the ones that
# happen to face the camera. Frame members are not checked against each other: they
# overlap on purpose, which is the rule about a 5cm gap reading as a detached stick.

FRAME = []
HUNG = []

SLACK = 0.0005           # a shared face is a joint, not a collision


def _extent(centre, size):
    return tuple((centre[i] - size[i] * 0.5, centre[i] + size[i] * 0.5) for i in range(3))


def _meets(a, b):
    return all(a[i][0] < b[i][1] - SLACK and b[i][0] < a[i][1] - SLACK for i in range(3))


def clear_room():
    del FRAME[:]
    del HUNG[:]


def obstacle(label, centre, size):
    """Register a piece of frame. Conservative boxes only - err large."""
    FRAME.append((label, _extent(centre, size)))


def obstacle_run(label, a, b, thick, slices=60, over=0.0):
    """A raking member, sliced along its length.

    One box round a leaning leg is the whole triangle it leans across, which would flag
    every basket inside the A. Slices follow the rake instead - but a slice is only as
    tight as its WIDEST end, and on a leg raking 0.6 in x per metre of z, every
    centimetre a slice sticks up above the thing it is being tested against costs six
    millimetres of false width. Two things follow, and both of them were learned by
    watching this refuse a basket that fits with 18mm to spare:

      * Sixty slices, not six. Sixty puts the quantisation error at 12mm of x.
      * No padding on the axis the member mostly runs along. A beam's thickness is a
        cross-section perpendicular to its own length, so on a near-vertical leg it
        belongs in x and y and not in z, and adding it in z was pulling the next slice
        up the rake into contact - which is 37mm of leg that is not there.
    """
    a, b = Vector(a), Vector(b)
    step = (b - a).normalized()
    a, b = a - step * over, b + step * over
    along = [abs(c) > 0.7 for c in step]          # the axis or axes this member follows

    for i in range(slices):
        p = a.lerp(b, i / float(slices))
        q = a.lerp(b, (i + 1) / float(slices))
        centre = (p + q) * 0.5
        size = tuple(abs(q[d] - p[d]) + (0.006 if along[d] else thick)
                     for d in range(3))
        FRAME.append(("%s[%d]" % (label, i), _extent(centre, size)))


def claim(label, centre, size, ignore=()):
    me = _extent(centre, size)
    for other, box_ in FRAME + HUNG:
        if other in ignore or other.split("[")[0] in ignore:
            continue
        if _meets(me, box_):
            raise SystemExit(
                "%s intersects %s - a hung thing may not share space with the frame "
                "or with another hung thing" % (label, other))
    HUNG.append((label, me))


# --------------------------------------------------------------------------- timber

def beam(a, b, width, mat, depth=None, over=0.0, tilt=WOOD_TILT):
    """
    A square timber from a to b, run on by `over` at both ends.

    Square rather than round because everything on the post is sawn; a round frame here
    would be the one thing in the pair that was not made by the same hands. The turn is
    the shortest rotation from +z onto the timber, so a leg leaning in one plane only is
    not also twisted about its own length.
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


def rod(a, b, radius, mat, sides=7, over=0.0, taper_to=None, tilt=WOOD_TILT):
    """
    A round pole from a to b. Rails, pegs, rungs, cords and withies are all this.

    Odd sides by default - an even-sided cylinder presents a flat face square to the
    camera and reads as a box with its corners knocked off.
    """
    a = Vector(a)
    b = Vector(b)
    axis = b - a
    r1 = radius if taper_to is None else taper_to

    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius, radius2=r1,
                                    depth=axis.length + over * 2.0,
                                    location=(a + b) * 0.5)
    obj = bpy.context.active_object
    turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis.normalized())
    lean = Euler((math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)),
                  math.radians(random.uniform(-tilt, tilt)))).to_quaternion()
    obj.rotation_euler = (lean @ turn).to_euler()
    obj.data.materials.append(material(mat))
    return obj


def cord(a, b, radius=0.011, mat="rope", into=0.030, past=0.010):
    """
    A length of cord, run 30mm INTO the peg it is tied to and 10mm past the rim.

    The overrun at the peg end is not a detail: a cord that merely reaches its peg leaves
    a millimetre of daylight at the knot and the basket reads as floating, which is the
    same failure as the 5cm gap that makes a strut read as a detached stick except that
    here it undoes the one thing the piece is doing.

    The two ends are NOT the same, and making them the same is what went wrong. Run 30mm
    past a rim as well, the cord finished five centimetres down inside the bowl - and
    because the bowls are open, you could see it lying in there in every render, in one
    case coming back out through the side wall. Ten millimetres buries the end in the
    lip and no further.
    """
    a = Vector(a)
    b = Vector(b)
    step = (b - a).normalized()
    return rod(a - step * into, b + step * past, radius, mat, sides=5, over=0.0,
               tilt=1.0)


def strap(size, at, rot_z=0.0, tilt=1.0):
    """A flat iron band over a joint. The post carries three of these across its front."""
    return box(size, at, "iron", rot_z=rot_z, tilt=tilt)


# --------------------------------------------------------------------------- basketry

def _coil(r0, r1, height, at, mat, sides=9, tilt=2.2):
    """One coil of a basket: a frustum with both caps gone, spun to a random angle.

    The spin matters more than it sounds. Nine-sided bands stacked with their facets in
    register read as a turned cone with grooves cut in it; rotated against each other
    they read as something coiled by hand, which is the entire difference between a pot
    and a basket at this distance.
    """
    obj = shell(r0, r1, height, at, mat, sides=sides)
    obj.rotation_euler = (math.radians(random.uniform(-tilt, tilt)),
                          math.radians(random.uniform(-tilt, tilt)),
                          math.radians(random.uniform(0.0, 360.0)))
    return obj


LIP = 0.0075             # how far each coil overhangs the one below it


def creel(at, radius, height, lean_x=0.0, lean_y=0.0, spin=0.0, mat="bark"):
    """
    A woven basket hanging open-mouthed, its rim centred on `at`.

    Built upright about the rim and then tipped bodily about that point, because the rim
    is where the cords land: tilting the parts individually puts the cords on a rim that
    has moved and the basket hangs off its own strings. Returns a function mapping a
    point in the basket's own frame into the world, so the caller can tie to the rim
    wherever it ends up.

    Open at the top, always. A lid on any of this and the piece becomes a container,
    which is the one thing two rounds of refusals said it must not be.

    THE COILS ARE THE WHOLE PIECE and the first two passes got them wrong in opposite
    directions - 9mm of relief read as nothing, 24mm read as a cooper's hoop, and both
    times the answer came back as a wooden pail. The relief is not the variable that
    matters: the NUMBER is. Four bands on a vessel is staving however deep the step;
    eight is coiling even at a third of the depth, because what the eye is counting is
    rounds of withy and four of them is not a basket-maker's afternoon. So: one coil per
    28mm of height, six at the least and nine at the most, each one standing 7.5mm proud
    of the TOP of the one beneath it and every one of them stepping the same way. That
    last part is what a coil does and what a hoop does not - the profile is a ratchet
    running up the wall, not a bulge with a pinch above and below it.
    """
    at = Vector(at)
    before = set(bpy.context.scene.objects)

    sides = 9
    floor_r = radius * 0.66          # baskets narrow towards the bottom; a straight tube
                                     # is a bucket, and a bucket has a lid somewhere

    def profile(t):
        # A gentle belly, not a straight cone: a basket is coiled outwards and then
        # drawn back in to bind, and the bulge is the making of it. Shallower than the
        # first pass's, because a deep belly under a wide rim is the exact silhouette of
        # a pail and the coils have to do the talking now.
        return (floor_r + (radius - floor_r) * t
                + radius * 0.055 * math.sin(math.pi * t))

    # The floor, set up inside the lowest coil rather than closing it off underneath.
    taper(floor_r * 1.05, floor_r * 0.98, 0.028,
          (at.x, at.y, at.z - height + 0.020), mat, sides=sides, tilt=1.2)

    coils = min(9, max(6, int(round(height / 0.028))))
    step = height / float(coils)
    for i in range(coils):
        t0 = i / float(coils)
        t1 = (i + 1) / float(coils)
        # Each coil laps the one below by a sixth of its own height. Butted, they show a
        # hairline of daylight all the way round wherever the two random spins disagree.
        z0 = at.z - height + t0 * height - (0.16 * step if i else 0.0)
        z1 = at.z - height + t1 * height
        # Wide at the bottom, narrow at the top, so its lower edge overhangs the coil
        # beneath and catches the sun as a line. Every coil the same way up: alternating
        # them is a hoop, and a hoop is a barrel.
        _coil(profile(t0) + LIP, profile(t1), z1 - z0,
              (at.x, at.y, (z0 + z1) * 0.5), mat, sides=sides)

    # The bound rim: a slim lip barely wider than the mouth, and short. A short frustum
    # and not a torus on purpose - a torus of any useful smoothness costs four times the
    # triangles for a shape that is three pixels thick.
    _coil(radius * RIM, radius * (RIM - 0.025), 0.030,
          (at.x, at.y, at.z - 0.009), mat, sides=sides)

    turn = (Matrix.Rotation(math.radians(spin), 3, "Z")
            @ Matrix.Rotation(math.radians(lean_y), 3, "Y")
            @ Matrix.Rotation(math.radians(lean_x), 3, "X"))
    place = Matrix.Translation(at) @ turn.to_4x4() @ Matrix.Translation(-at)
    for obj in bpy.context.scene.objects:
        if obj not in before:
            obj.matrix_world = place @ obj.matrix_world

    def point(lx, ly, lz):
        return at + turn @ Vector((lx, ly, lz))

    return point


def hang(label, peg, at, radius, height, lean_x=0.0, lean_y=0.0, spin=0.0, ignore=()):
    """A basket on two cords from one peg. The pair of cords is what says `hanging`.

    One cord reads as a stalk and the basket looks grown rather than tied; two splayed
    to opposite sides of the rim make a triangle, and a triangle of cord over a bowl is
    recognisable at any distance and any angle.

    The claim goes in BEFORE anything is built, so a layout that would put a timber
    through a bowl never gets as far as a render to be missed in.
    """
    at = Vector(at)
    # Lean tips the body sideways under a fixed rim, so the footprint is wider than the
    # mouth by about height * sin(lean); 9% covers every lean used here with margin.
    spread = radius * RIM + height * 0.09
    top = at.z + 0.028
    bottom = at.z - height - 0.022
    claim(label, (at.x, at.y, (top + bottom) * 0.5),
          (spread * 2.0, spread * 2.0, top - bottom), ignore=ignore)

    point = creel(at, radius, height, lean_x=lean_x, lean_y=lean_y, spin=spin)
    peg = Vector(peg)
    for side in (-1.0, 1.0):
        # Tied AT the rim: full rim radius, on the rim plane. Every other choice puts
        # rope inside a bowl that is open at the top and therefore looked into.
        cord(peg, point(side * radius, side * radius * 0.12, -0.009))
    return point


def bundle(label, peg, at, length, count=5, spread=0.050, thick=0.016, mat="bark",
           lean=8.0, ignore=()):
    """
    A hanging bundle - withies, or a handful of green rods, tied at the neck.

    Secondary detail and deliberately small. Its job is to break the rhythm of round
    mouths in a row: four baskets hung along a rail is a diagram of four baskets, and
    one bundle among them is a rack that somebody uses.
    """
    at = Vector(at)
    reach = spread + thick * 1.4
    top = at.z + 0.030
    bottom = at.z - length - 0.045
    claim(label, (at.x, at.y, (top + bottom) * 0.5),
          (reach * 2.0, reach * 1.4 + thick * 2.0, top - bottom), ignore=ignore)

    for i in range(count):
        yaw = 360.0 / count * i + random.uniform(-18.0, 18.0)
        rad = math.radians(yaw)
        tip = Vector((at.x + math.cos(rad) * spread,
                      at.y + math.sin(rad) * spread * 0.7,
                      at.z - length + random.uniform(-0.035, 0.035)))
        rod(at + Vector((0.0, 0.0, 0.02)), tip, thick, mat, sides=5,
            taper_to=thick * 0.55, tilt=lean)

    # The tie, at the neck and standing wide of the rods on both sides. Flush with them
    # it is a stripe; proud of them it is a knot.
    _coil(thick * 2.3, thick * 2.1, 0.055, (at.x, at.y, at.z - 0.075), "rope", sides=7)
    cord(Vector(peg), at + Vector((0.0, 0.0, 0.02)))


# A coil of cordage hung over a peg was tried here and cut after the first render. A
# torus is a torus: pale, perfectly circular, edge-on to the camera, it photographed as
# an iron hoop hung on the frame - which is hardware, and hardware is the shortest road
# back to the machine the second round was refused for. There is no cheap way to make a
# ring read as soft, so the ring is gone and the bundles carry the cordage instead.


# --------------------------------------------------------------------------- leant
#
# A flat slatted board tipped towards the post, standing on a broad sill, with its pegs
# in TWO rails and everything hanging clear in front of the face.
#
# This is the third version of this piece and the first two both died of the same thing:
# they led with a big raking timber and so did the horse, and two pictures of one design
# is the exact failure the instruction about silhouettes exists to catch. Version one
# tipped back onto a pair of struts and was an easel. Version two tipped sideways at 13
# degrees and was still a rake first and a board second. At 8 degrees over a sill wider
# than the board, with the slat heads cut flush into the head ledger instead of standing
# past it in a fringe, it is a panel leaning on a plinth - and a panel and a triangle are
# different shapes at any distance.
#
# The sill runs on PAST the board on the uphill side, which is what does the struts' old
# job: the counterweight is under the piece rather than sticking out behind it, so
# nothing crosses the outline.

LEANT_LEAN = 8.0          # degrees off plumb, top tipped towards the post
LEANT_TOP = 1.280

# FOUR narrow slats with three narrow slots, and the proportion between those two
# numbers is the whole thing. Three slats 135mm wide with 90mm of daylight between them
# photographed as three separate posts with baskets hung in front of them - a scaffold,
# not a board - because the eye counts anything it can tell apart and 90mm at five
# metres is plainly apart. Four slats 115mm wide with 60mm slots group into one striped
# surface instead, which is what a hurdle or a pallet does, and it is still 28% open.
LEANT_SLATS = (-0.2625, -0.0875, 0.0875, 0.2625)
LEANT_PLANK = 0.115
LEANT_RAKE = math.tan(math.radians(LEANT_LEAN))

# THREE peg rails, not two, and not one stepped column. The column cannot work at all -
# see the note at the top of the file - and two rails could not carry five hung things
# either: the board is 0.64 across, a basket is a third of a metre of frontage and a
# bundle is another eighth, so two rails is room for four things and only if none of
# them is large. Three rails carry four baskets and a bundle with a hand's width between
# every pair, and each row's drop is chosen so its bodies bottom out ABOVE the pegs of
# the row beneath. That last rule is the only thing keeping a peg out of a basket.
LEANT_ROWS = (1.170, 0.775, 0.380)
LEANT_LEDGERS = (LEANT_TOP - 0.032, LEANT_ROWS[1], LEANT_ROWS[2])


def _leant_x(z, off):
    """Where the centre of one slat is at a given height."""
    return off + LEANT_RAKE * z


def _leant_peg(off, z, length):
    """One peg; returns the point things hang from and the label of its obstacle."""
    root = Vector((_leant_x(z, off), 0.0, z))
    tip = root + Vector((0.012 if off >= 0 else -0.012, length, length * 0.22))
    rod(root + Vector((0.0, -0.045, 0.0)), tip, 0.022, "wood", sides=7,
        taper_to=0.017, tilt=2.0)
    lo = Vector((min(root.x, tip.x) - 0.024, -0.050, root.z - 0.024))
    hi = Vector((max(root.x, tip.x) + 0.024, tip.y + 0.024, tip.z + 0.024))
    label = "peg@%+.2f,%.2f" % (root.x, z)
    obstacle(label, (lo + hi) * 0.5, hi - lo)
    return tip - Vector((0.0, 0.020, 0.008)), label


def leant():
    # The sill, offset away from the lean and WIDE - it is 0.98 long under a board 0.64
    # across, and that proportion is most of why this reads as a panel standing on
    # something rather than as a timber leaning on the ground.
    box((0.98, 0.46, 0.115), (-0.130, 0.0, 0.058), "wood", tilt=WOOD_TILT)
    obstacle("sill", (-0.130, 0.0, 0.058), (0.98, 0.46, 0.115))

    # The slats, flat on: 11.5cm across the face and 5.4cm through. Flat rather than
    # square because this is a BOARD - square stiles read as a ladder, and the ladder
    # belongs to the horse. No `over` at the head: the heads are cut flush into the top
    # ledger so the board ends on one line rather than in a row of prongs.
    for off in LEANT_SLATS:
        beam((_leant_x(0.030, off), 0.012, 0.030),
             (_leant_x(LEANT_TOP, off), 0.0, LEANT_TOP), LEANT_PLANK, "wood",
             depth=0.054)

    # THREE ledgers, and they run BEHIND the slats on -y. In front they would be the
    # first thing the eye lands on and the board would read as a gate; behind, they are
    # seen only through the slots. Three and not two because two left the middle of the
    # board with nothing crossing it, and vertical members with nothing crossing them
    # are posts. The top one is flush with the slat heads and the other two sit exactly
    # on the peg rails, which is where a board like this would actually be pegged.
    for z in LEANT_LEDGERS:
        # 30mm past the outer slats and no more. At 75mm the ledger stood out beyond the
        # iron in front of it and the head of the board grew a second, wider, paler band
        # behind the first - two horizontals where the design has one.
        beam((_leant_x(z, LEANT_SLATS[0]) - 0.030, -0.046, z),
             (_leant_x(z, LEANT_SLATS[-1]) + 0.030, -0.046, z),
             0.060, "wood", over=0.012)

    # Iron across all three ledger joints, in the post's own idiom - it wears three flat
    # straps over its front and this is the same blacksmith's morning. These are what a
    # viewer actually sees tying the slats into one board, the ledgers being behind
    # them, so they are what turns four uprights into a surface. Shallow on y: the front
    # face of this strap is the front face of the whole board, and it is the thing every
    # basket has to clear.
    #
    # The topmost strap is placed to CAP the slat heads rather than to sit under them.
    # Cut flush into the ledger, the heads still stood about 12mm above the iron, and at
    # four times magnification that is a row of little prongs along the top edge - the
    # crenellated fringe this board has now been rebuilt twice to lose. A band whose top
    # edge is a few millimetres above the timber ends the board on one line.
    for z, deep in ((LEANT_TOP - 0.018, 0.044),
                    (LEANT_ROWS[1], 0.040), (LEANT_ROWS[2], 0.040)):
        strap((0.66, 0.036, deep), (LEANT_RAKE * z, 0.036, z))

    board_front = 0.056
    obstacle("board", (0.090, (board_front - 0.100) * 0.5, LEANT_TOP * 0.5),
             (0.86, board_front + 0.100, LEANT_TOP + 0.02))

    # The pegs. They are angled up so nothing slides off, and they stand out of the face
    # towards the camera, which is the whole reason this is not the round-one lean-to: a
    # lean-to has bays, and a bay is a box on its side. A peg holds one thing and hides
    # nothing behind it.
    top_left, _ = _leant_peg(LEANT_SLATS[0], LEANT_ROWS[0], 0.245)
    top_right, top_right_peg = _leant_peg(LEANT_SLATS[2], LEANT_ROWS[0], 0.230)
    mid_left, _ = _leant_peg(LEANT_SLATS[0], LEANT_ROWS[1], 0.225)
    mid_right, _ = _leant_peg(LEANT_SLATS[3], LEANT_ROWS[1], 0.235)
    foot, _ = _leant_peg(LEANT_SLATS[2], LEANT_ROWS[2], 0.220)

    # Four baskets and one bundle, no two the same size, and the run of them walks down
    # and across the board so no two mouths are level and no two neighbours are the same
    # width. Each one hangs plumb under its own peg: a basket set even 10cm to one side
    # of the thing holding it slants both its cords, and two slanted cords on a hanging
    # bowl is the one detail that says "this was placed" rather than "this hangs here".
    hang("leant.top-left", top_left, (top_left.x, top_left.y + 0.040, 1.070),
         0.145, 0.180, lean_y=-4.0, spin=18.0)
    hang("leant.mid-left", mid_left, (mid_left.x, mid_left.y + 0.040, 0.640),
         0.105, 0.150, lean_y=5.0, lean_x=-3.0, spin=-34.0)
    hang("leant.mid-right", mid_right, (mid_right.x, mid_right.y + 0.040, 0.675),
         0.128, 0.175, lean_y=4.0, spin=-52.0)
    # The lowest one is the smallest and hangs shortest, because the sill is under it:
    # a 16cm basket on a 12cm drop put its floor inside the plinth, which the obstacle
    # check refused and which would have read in game as a basket sunk into the wood.
    hang("leant.foot", foot, (foot.x, foot.y + 0.040, 0.310),
         0.100, 0.140, lean_y=-5.0, spin=41.0)

    # The bundle goes on the second head peg, beside the top basket and above everything
    # else, where it breaks the run of round mouths in half. It hangs close under its
    # own peg, so that peg is the one obstacle it is allowed to touch.
    bundle("leant.bundle", top_right, (top_right.x, top_right.y + 0.040, 1.115), 0.205,
           count=5, spread=0.044, ignore=(top_right_peg,))

    # Two colliders: the sill, and a slab standing in for the board. Not a box round the
    # baskets - they hang out in front where a player walks past, and an invisible wall
    # a foot proud of the frame is the kind of thing that gets reported as "the piece
    # pushes me".
    collide((-0.130, 0.0, 0.058), (0.98, 0.46, 0.115))
    collide((LEANT_RAKE * 0.68, -0.010, 0.680), (0.68, 0.22, 1.24))


# --------------------------------------------------------------------------- rail
#
# One long rail between two short uprights, and nothing above it at all. This is the
# airiest of the three and the least like furniture-with-a-back: from eye height you
# look straight over the top of it at whatever is behind, and between the baskets you
# see the ground. Low and wide against the post's tall and narrow.
#
# Three baskets and not four. Four was tried and the arithmetic does not close: the
# clear span between the uprights is 1.11m, four baskets of any useful size are 1.05m of
# it, and what came back was a row with 3cm of daylight between the bowls and the last
# one driven straight through the upright - which is what the second pass shipped. Three
# leaves 50mm gaps and a hand's width at each end, and the bundle moves OUTBOARD onto
# the rail's overhang past the right upright, where there is nothing behind it at all.

RAIL_TOP = 0.905
RAIL_X = 0.600
RAIL_OVER = 0.145        # how far the rail runs past each upright. Long enough that a
                         # bundle can hang off the right-hand one with a clear 27mm
                         # between its withies and the upright - at 0.115 they touched,
                         # and the obstacle check refused the build for it.
RAIL_WIDTH = 0.086


def rail():
    for side in (-1, 1):
        # A sill under each upright rather than a leg into the dirt. A post rammed in
        # the ground is fencing; a sill is joinery, and joinery is the word the whole
        # brief keeps circling.
        box((0.165, 0.44, 0.085), (side * RAIL_X, -0.010, 0.043), "wood",
            tilt=WOOD_TILT)
        beam((side * RAIL_X, 0.0, 0.055), (side * (RAIL_X - 0.012), 0.010, RAIL_TOP),
             RAIL_WIDTH, "wood", over=0.02)

        # Raking brace on -y, out of sight behind the upright, doing the job that says
        # this does not fold over sideways.
        beam((side * (RAIL_X - 0.010), 0.008, 0.560),
             (side * (RAIL_X - 0.030), -0.200, 0.075), 0.055, "wood", over=0.015)

        # Iron at the rail seat, where a rail bearing three loaded baskets would actually
        # split the head of an upright.
        strap((0.115, 0.150, 0.055), (side * (RAIL_X - 0.012), 0.010, RAIL_TOP - 0.048))

        obstacle("upright%+d" % side,
                 (side * (RAIL_X - 0.006), 0.005, (RAIL_TOP + 0.035) * 0.5),
                 (RAIL_WIDTH + 0.020, 0.20, RAIL_TOP + 0.06))
        obstacle("sill%+d" % side, (side * RAIL_X, -0.010, 0.043), (0.185, 0.46, 0.10))

    # The rail, proud of both uprights by a hand's width. An end that stops flush is a
    # cut; one that runs on is a rail somebody could still hang another basket from -
    # and here somebody has, because the bundle hangs off the right-hand overhang.
    rod((-(RAIL_X + RAIL_OVER), 0.012, RAIL_TOP), ((RAIL_X + RAIL_OVER), 0.012, RAIL_TOP),
        0.034, "wood", sides=7, taper_to=0.031)
    obstacle("rail", (0.0, 0.012, RAIL_TOP),
             (2.0 * (RAIL_X + RAIL_OVER), 0.080, 0.080))

    # A low stretcher tying the two sills. It sits at ankle height so it does not close
    # the middle of the frame - everything between it and the rail is daylight.
    beam((-(RAIL_X - 0.02), -0.010, 0.245), ((RAIL_X - 0.02), -0.010, 0.245),
         0.058, "wood", over=0.015)
    strap((0.34, 0.075, 0.032), (0.0, 0.032, 0.250))
    obstacle("stretcher", (0.0, 0.0, 0.248), (2.0 * RAIL_X, 0.140, 0.075))

    # Three baskets along the rail at three drops and three sizes. The drops are
    # staggered hard - 0.70 down to 0.53 - because three mouths at one height is a shop
    # display, and this is a working rack.
    for label, x, radius, height, rim, lean_y, spin in (
            ("rail.big", -0.330, 0.152, 0.225, 0.700, 6.0, 22.0),
            ("rail.small", 0.020, 0.104, 0.160, 0.535, -8.0, -35.0),
            ("rail.mid", 0.360, 0.134, 0.200, 0.735, 4.0, 14.0)):
        hang(label, (x, 0.012, RAIL_TOP - 0.030), (x + 0.006, 0.020, rim), radius,
             height, lean_y=lean_y, spin=spin, ignore=("rail",))

    # One bundle, hung OUTBOARD on the right overhang past the upright. Inboard there is
    # no room for it - the span is full - and out here it is the one thing on the piece
    # with open sky behind it on three sides, which is exactly what a secondary detail
    # wants and exactly what the second pass's bundle, tucked against an upright, did
    # not get.
    bundle("rail.bundle", (0.730, 0.012, RAIL_TOP - 0.030), (0.730, 0.020, 0.760),
           0.330, count=5, spread=0.034, ignore=("rail",))

    # Two colliders at the uprights and nothing across the middle. A single box round
    # the whole frame would be a metre and a half of invisible wall through the gap the
    # piece exists to show daylight through.
    for side in (-1, 1):
        collide((side * RAIL_X, 0.0, RAIL_TOP / 2.0), (0.22, 0.44, RAIL_TOP))


# --------------------------------------------------------------------------- horse
#
# A drying horse: a shallow A on splayed feet, its legs crossed at the top with a ridge
# pole in the forks. The A is deliberately shallow - a steep one converges so fast that
# a rung at any useful height is 30cm wide and nothing can hang from it. Two rungs on
# the front face carry the baskets, and the triangle of daylight inside the legs is the
# reason this outline is not the leant board with its struts showing.
#
# How wide the A is at a given height is not negotiable, and the second pass pretended
# otherwise: it hung the upper baskets at 84% of a rung's half-span, which at that
# height IS the leg, and the same baskets then swallowed the rung below them whole. What
# fixes it is not a percentage - it is the obstacle check at the top of this file, plus
# accepting that the upper rung carries ONE basket and it is a small one.

HORSE_APEX = 1.240
HORSE_FOOT = 0.690
HORSE_CROSS = 0.055       # how far each leg runs past the other at the fork
HORSE_DEPTH = 0.195
HORSE_LEG = 0.072

HORSE_UPPER = 0.860
HORSE_LOWER = 0.430


def _horse_x(z, side):
    """The centre of one leg at a given height, measured along its own rake."""
    return side * (HORSE_FOOT - (HORSE_FOOT + HORSE_CROSS) * (z / HORSE_APEX))


def horse():
    for depth in (HORSE_DEPTH, -HORSE_DEPTH):
        for side in (-1, 1):
            beam((_horse_x(0.0, side), depth, 0.02),
                 (_horse_x(HORSE_APEX, side), depth * 0.86, HORSE_APEX),
                 HORSE_LEG, "wood", over=0.025)
            # Sliced, not boxed. One box round a leg leaning across 1.4m of x is the
            # whole triangle, and every basket inside the A would fail against it.
            obstacle_run("leg%+d%+.0f" % (side, depth * 10),
                         (_horse_x(0.0, side), depth, 0.02),
                         (_horse_x(HORSE_APEX, side), depth * 0.86, HORSE_APEX),
                         HORSE_LEG + 0.020, over=0.030)

    # A foot board under each pair of feet, running in y to tie the front leg to the
    # back one. That is the joint that makes two A-frames into a horse rather than into
    # two hurdles leaning at each other, and it lies on the ground where it cannot close
    # any daylight. Across the grain, so the horse stands on something rather than on
    # four sawn ends.
    for side in (-1, 1):
        box((0.115, 0.52, 0.062), (side * (HORSE_FOOT - 0.01), 0.0, 0.031), "wood",
            tilt=WOOD_TILT)
        obstacle("foot%+d" % side, (side * (HORSE_FOOT - 0.01), 0.0, 0.031),
                 (0.135, 0.54, 0.082))

    # The ridge, sitting in the two forks. It ran 15cm past each fork in the first pass
    # and the apex came back a knot of timber and iron with a basket buried in it - at
    # eye height the top of an A is already four members crossing, and every centimetre
    # of overhang adds another line through the same twenty pixels. Ten centimetres is
    # enough to read as a pole resting in a fork rather than as a joint. Round where the
    # rest is sawn, because it is the one part that was a pole rather than a board.
    ridge_z = HORSE_APEX - 0.050
    ridge_y = HORSE_DEPTH + 0.100
    rod((0.0, -ridge_y, ridge_z), (0.0, ridge_y, ridge_z), 0.030, "wood", sides=7,
        taper_to=0.027)
    obstacle("ridge", (0.0, 0.0, ridge_z), (0.075, 2.0 * ridge_y, 0.075))

    # Iron at the crossing, where the whole weight of the thing is carried on one lap
    # joint. The post wears its straps the same way. Slimmer than the post's, for the
    # same reason the ridge is shorter.
    strap((0.195, 0.046, 0.036), (0.0, HORSE_DEPTH + 0.026, HORSE_APEX - 0.062))
    strap((0.195, 0.046, 0.036), (0.0, -(HORSE_DEPTH + 0.026), HORSE_APEX - 0.062))

    # Two rungs across the FRONT pair only. Rungs on both faces would double the timber
    # and halve the daylight for nothing - the back of this piece is against the post's
    # side, and nobody hangs anything there.
    rungs = {}
    for name, z in (("upper", HORSE_UPPER), ("lower", HORSE_LOWER)):
        span = abs(_horse_x(z, 1))
        rod((-span - 0.03, HORSE_DEPTH, z), (span + 0.03, HORSE_DEPTH, z),
            0.027, "wood", sides=7)
        obstacle("rung.%s" % name, (0.0, HORSE_DEPTH, z),
                 (2.0 * span + 0.06, 0.070, 0.070))
        rungs[name] = (z, span)

    # One side stretcher low down, front leg to back leg. Only one, on -x, because two
    # is a frame and a frame starts to read as a carcass again.
    beam((-(HORSE_FOOT - 0.075), HORSE_DEPTH, 0.225),
         (-(HORSE_FOOT - 0.075), -HORSE_DEPTH, 0.225), 0.050, "wood", over=0.02)
    obstacle("stretcher", (-(HORSE_FOOT - 0.075), 0.0, 0.225),
             (0.070, 2.0 * HORSE_DEPTH + 0.04, 0.070))

    # The upper rung is 35cm across between the legs and it carries ONE basket, a small
    # one, sitting a little left of centre. That is the honest capacity of an A at that
    # height; the second pass claimed two and put both of them through a leg.
    up, _ = rungs["upper"]
    hang("horse.upper", (-0.060, HORSE_DEPTH, up - 0.020),
         (-0.060, HORSE_DEPTH + 0.095, 0.700), 0.108, 0.175, lean_y=6.0, spin=-19.0,
         ignore=("rung.upper",))

    # Two on the lower rung, out at the flanks where the A is finally wide. Out at the
    # flanks and not in the middle for the same reason the ridge is short: the hole in
    # the middle of an A is the whole reason this outline is not a box, and baskets
    # parked in it make a box with a pointed top.
    low, _ = rungs["lower"]
    hang("horse.low-left", (-0.255, HORSE_DEPTH, low - 0.018),
         (-0.255, HORSE_DEPTH + 0.090, 0.330), 0.140, 0.200, lean_y=5.0, spin=-44.0,
         ignore=("rung.lower",))
    hang("horse.low-right", (0.245, HORSE_DEPTH, low - 0.018),
         (0.245, HORSE_DEPTH + 0.085, 0.350), 0.118, 0.165, lean_y=-6.0, spin=33.0,
         ignore=("rung.lower",))

    # The bundle hangs off the NOSE of the ridge, in front of the apex and 10cm proud of
    # the legs, so it is seen against the sky rather than against timber. On the upper
    # rung beside the basket there is no room for it - that rung is 35cm wide and the
    # basket has 22cm of it - and on the lower rung it was hidden behind a basket in the
    # first render, which is the whole of what a secondary detail must not be.
    bundle("horse.bundle", (0.0, HORSE_DEPTH + 0.080, ridge_z),
           (0.0, HORSE_DEPTH + 0.125, 1.140), 0.235, count=5, spread=0.042,
           ignore=("ridge",))

    # A collider per leg pair, leaving the triangle between them walkable. The point of
    # the shape is that you can see through it; being unable to walk through it as well
    # would be the kind of quiet contradiction a player feels and cannot name.
    for side in (-1, 1):
        collide((side * 0.44, 0.0, HORSE_APEX / 2.0), (0.44, 0.52, HORSE_APEX))


# --------------------------------------------------------------------------- staging

def stage():
    """Ground, sun 1.4, fill 0.35, world 0.28 - the numbers that keep timber timber.

    vhbuild's own stage_scene runs a 3.2 sun, which puts every material in the piece on
    one value and makes a silhouette impossible to judge. That is the whole job here.
    """
    bpy.ops.mesh.primitive_plane_add(size=60.0, location=(0, 0, 0))
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


def camera(at, aim, lens=LENS):
    bpy.ops.object.camera_add(location=at)
    cam = bpy.context.active_object
    cam.data.lens = lens
    cam.data.sensor_fit = "HORIZONTAL"
    cam.data.clip_end = 400
    target = bpy.data.objects.new("aim", None)
    bpy.context.collection.objects.link(target)
    target.location = aim
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam
    return cam


def import_post(x=0.0):
    """The shipped stowing post, imported with the same axes it was exported with."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=POST, forward_axis="Z", up_axis="Y")
    for obj in bpy.context.selected_objects:
        obj.location.x += x


def render(path, width=WIDTH, height=HEIGHT):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = False     # scene state; this render wants a sky
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def check_tints():
    """Say out loud what tint() could not colour.

    tint() SKIPS any material whose name is not in TINTS, and a skipped material keeps
    Blender's default near-white BSDF. A piece that invents a group therefore renders it
    bone-white with nothing anywhere saying so, and white reads as a colour-space bug
    rather than as a missing dictionary key. This turns that into a failed run.
    """
    unknown = sorted({m.name.split(".")[0].lower() for m in bpy.data.materials
                      if m.name.split(".")[0].lower() not in TINTS
                      and m.name.split(".")[0].lower() not in ("ground", "ref")})
    if unknown:
        raise SystemExit("untinted material groups %s - they render white"
                         % ", ".join(unknown))


# --------------------------------------------------------------------------- output

def build(key, make, yaw, icon_yaw):
    """Model one variant, export it, then photograph it standing beside the post."""
    name = "grow3_rack_" + key

    clear_scene()
    clear_room()
    make()

    # Bevel per object, ONE segment, then join. finish() would bevel with two, which
    # doubles what a chamfer costs to buy a few pixels at five metres.
    bevel_all(segments=1)
    obj = finish(name, bevel=False)

    colliders = len(COLLIDERS)
    export(obj, name, VARIANTS)
    write_col(os.path.join(VARIANTS, name + ".col"))

    tris = triangles(obj)
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    width = max(xs) - min(xs)
    depth = max(ys) - min(ys)
    height = max(zs) - min(zs)

    if height > POST_HEIGHT - 0.25:
        raise SystemExit("%s is %.2fm - it has to stay subordinate to the post's %.2fm"
                         % (name, height, POST_HEIGHT))

    # Placed AFTER export, so the asset stays centred on its own origin. The yaw is the
    # render's and not the model's: a piece is built at whatever angle the player faces,
    # and dead square to the camera a flat rack hides the whole depth of what hangs on
    # it. A few degrees is enough - the brief says it faces the camera, and it does.
    obj.rotation_euler.z = math.radians(yaw)

    # Preferred 1.35m to one side, pushed further only when the piece is wide enough to
    # crowd the post. The rail is 1.43m across and at 1.35m its end would touch the post.
    beside = min(NEAR, -(POST_HALF_W + CLEAR + width / 2.0))
    obj.location.x += beside

    import_post()
    tint()
    check_tints()
    stage()

    left = beside - width / 2.0 - 0.10
    right = POST_HALF_W

    # The cube stands past the far end of the piece, on the clear ground beyond it, and
    # set back a little so it does not crowd whatever hangs off that end - the rail's
    # bundle was half behind it in the first pass. It is placed by its centre and framed
    # by its NEAR face, half a metre closer to the camera and so projecting wider, which
    # is how a cube that fits on paper ends up clipped at the frame edge.
    cube_x = left - 0.78
    reference_cube((cube_x, -0.42, 0.50))
    left = cube_x - 0.50
    centre = (left + right) / 2.0

    # Post, piece and cube come to three and a half metres of frontage, so the camera
    # lands near 5m rather than the 3.5m the post's own renders use. That is not a
    # choice: 42mm shows about 3m at 3.5m, and the alternative - putting the cube behind
    # the row instead of beside it - makes the one object in the shot whose job is to
    # state a size smaller than it really is, which is worse than standing further off.
    half = max(right - centre, centre - left) + 0.26
    fov = 2.0 * math.atan(SENSOR_HALF / LENS)
    distance = half / math.tan(fov / 2.0)
    aim_z = 0.82
    camera((centre, distance, EYE), (centre, -0.05, aim_z))

    vertical_half = half * HEIGHT / float(WIDTH)
    if aim_z + vertical_half < POST_HEIGHT + 0.12:
        raise SystemExit("%s frame is too short for the post" % name)

    render(os.path.join(PREVIEWS, name + ".png"))

    flag = "  OVER %d" % LIMIT if tris > LIMIT else ("  over target" if tris > TARGET else "")
    print("DESIGN_OK %-18s tris=%5d colliders=%d  size=%.2fx%.2fx%.2f  beside=%.2f "
          "camera=%.2f%s" % (name, tris, colliders, width, depth, height, beside,
                             distance, flag))
    if tris > LIMIT:
        raise SystemExit("%s is over %d triangles" % (name, LIMIT))

    icon(name, icon_yaw)
    return tris, colliders, width, depth, height


def icon(name, yaw):
    """
    Orthographic, front-on, transparent, its own two suns and its own exposure, 128px.

    The yaw is per variant and it is not decoration. A flat 22 degrees for all three put
    the leant board and the horse both three-quarters on, and at 128 pixels a board seen
    three-quarters on is a stick and an A-frame seen three-quarters on is a lean-to -
    the two icons came out looking like the same object. Each one is turned as far as it
    can be while its own defining line still faces the viewer.

    The preview pass's lighting and its ground plane are both wrong for an icon, so none
    of it is reused. film_transparent goes back OFF at the end - it is scene state, and
    left on, the next preview in this same run comes out with a white void for a sky and
    reads as a blown exposure.
    """
    clear_scene()
    bpy.ops.wm.obj_import(filepath=os.path.join(VARIANTS, name + ".obj"),
                          forward_axis="Z", up_axis="Y")
    imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    for obj in imported:
        obj.rotation_euler.z += math.radians(yaw)
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
    check_tints()

    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(0.0, 2.93, 2.93 * math.tan(math.radians(10.0))))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = radius * 2.16
    cam.rotation_euler = (math.radians(80.0), 0.0, math.radians(180.0))
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

    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    print("ICON_OK   %-18s radius=%.2f" % (name, radius))


# --------------------------------------------------------------------------- lineup

PIECE_GAP = 0.50     # clear floor between two candidates
GROUP_GAP = 1.05     # and a wider one after the post, so it reads as the reference
CUBE_GAP = 0.95

LINE_W, LINE_H = 3000, 780


def load(path):
    """Import one exported .obj and return its meshes with world-space bounds.

    forward_axis Z / up_axis Y is the exact inverse of the export, so this is a picture
    of the files that would ship rather than of a scene that only exists in Blender. If
    a piece looks wrong here it is wrong in the game.
    """
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Z", up_axis="Y")
    objs = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    bpy.context.view_layer.update()
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            for a in range(3):
                lo[a] = min(lo[a], w[a])
                hi[a] = max(hi[a], w[a])
    return objs, lo, hi


def lineup(keys):
    """
    The post at one end, the three candidates in a row, the cube at the far end.

    The post is FIRST and not in the middle. A pick is made by comparing silhouettes
    against each other, and centring the post puts the outer two candidates four metres
    apart with it in between. Ranged along one wall they are neighbours, and having the
    post read first means each candidate is seen after the thing it must be subordinate
    to - which is the order the eye wants for that question.

    Every piece is centred on its own DEPTH before placing, so they stand on one line
    rather than wherever each script happened to leave its origin. A piece half a metre
    forward of the row reads as bigger when it is only nearer.
    """
    clear_scene()

    row = [("POST", POST)] + [None] + \
          [(k, os.path.join(VARIANTS, "grow3_rack_%s.obj" % k)) for k in keys]

    loaded = []
    for slot in row:
        if slot is None:
            loaded.append(None)
            continue
        label, path = slot
        if not os.path.exists(path):
            raise SystemExit("LINEUP missing %s" % path)
        loaded.append((label,) + load(path))

    # Walk towards -x, leaving clear floor between bounding boxes rather than spacing on
    # centres: the candidates run 1.1m to 1.5m wide and centre spacing would crowd one
    # end of the row and strand the other.
    placed = []
    cursor = 0.0
    gap = None
    for item in loaded:
        if item is None:
            gap = GROUP_GAP
            continue
        label, objs, lo, hi = item
        if placed:
            cursor -= (gap if gap is not None else PIECE_GAP)
        gap = None
        dx = cursor - hi.x
        dy = -(lo.y + hi.y) / 2.0
        placed.append((label, objs, dx, dy, lo, hi))
        cursor -= (hi.x - lo.x)

    left_edge = placed[0][2] + placed[0][5].x
    cube_x = cursor - CUBE_GAP - 0.5
    right_edge = cube_x - 0.5
    centre = (left_edge + right_edge) / 2.0

    tallest = 0.0
    for label, objs, dx, dy, lo, hi in placed:
        for obj in objs:
            obj.location.x += dx - centre
            obj.location.y += dy
        tallest = max(tallest, hi.z - lo.z)
        print("LINEUP %-6s x=%+6.2f  w=%.2f  d=%.2f  h=%.2f"
              % (label, dx - centre + (lo.x + hi.x) / 2.0, hi.x - lo.x, hi.y - lo.y,
                 hi.z - lo.z))

    tint()
    check_tints()
    stage()
    reference_cube((cube_x - centre, 0.0, 0.5))

    half = max(left_edge - centre, centre - right_edge) + 0.45
    fov = 2.0 * math.atan(SENSOR_HALF / LENS)
    distance = half / math.tan(fov / 2.0)
    aim_z = 0.80
    camera((0.0, distance, EYE), (0.0, 0.0, aim_z))

    vertical_half = half * LINE_H / float(LINE_W)
    print("LINEUP camera %.2f  half %.2f  vertical half %.2f  tallest %.2f"
          % (distance, half, vertical_half, tallest))
    if aim_z + vertical_half < tallest + 0.15:
        raise SystemExit("LINEUP frame is too short - raise LINE_H or widen the run")

    render(os.path.join(PREVIEWS, "grow3_rack_lineup.png"), LINE_W, LINE_H)
    print("DESIGN_OK grow3_rack_lineup")


# --------------------------------------------------------------------------- main

def main():
    os.makedirs(VARIANTS, exist_ok=True)
    os.makedirs(PREVIEWS, exist_ok=True)

    # A few degrees of yaw, never none and never much. Square on, a flat rack hides
    # every centimetre of the depth its baskets hang in; turned far, it stops facing
    # the camera the brief put on +y.
    build("leant", leant, -8.0, icon_yaw=13.0)
    build("rail", rail, -6.0, icon_yaw=22.0)
    build("horse", horse, -7.0, icon_yaw=7.0)

    lineup(("leant", "rail", "horse"))


main()
