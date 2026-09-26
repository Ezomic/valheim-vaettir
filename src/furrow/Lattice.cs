using System.Collections.Generic;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// Which grid a plant belongs on, worked out from the plants already in the ground.
    ///
    /// This is the half of the grid that was wrong in LHM-29. Robbin, planting on live: "when planting crops it sometimes looks like it's using a
    /// different grid for a certain patch than another", and "when planting oak saplings it
    /// doesnt stick to the grid of the first one and shifts grids". Both came out of the same
    /// place: the lattice had nothing to fall back on but the cursor.
    ///
    /// The old rule was "hold the phase you found; when you lose it, take the nearest plant
    /// of the SAME CROP within four metres; when there is none, start a new grid under the
    /// cursor". Three things broke it, and each is answered here rather than patched over:
    ///
    ///  - Four metres was a constant, and a tree's grid is not. The step is the plant's own
    ///    grow radius twice over, which for a sapling is metres, so the next lattice point
    ///    beside an oak is often further than four metres from it. Aiming there found no oak,
    ///    and the fallback started a fresh grid under the cursor. So reach is counted in
    ///    CELLS here (<see cref="Reach"/>), and the neighbour of any plant is always in it.
    ///  - The phase was lost far more often than it looked. It was dropped whenever the
    ///    ghost was hidden, and vanilla hides the ghost on every frame the placement ray
    ///    misses or runs past reach - which is every time you look up at the next spot or
    ///    walk to it. The "re-pick lands on the same lattice" argument only held when the
    ///    re-pick found a plant, and for trees it often did not.
    ///  - "Same crop" split one field into several grids. A turnip beside a carrot bed found
    ///    no turnip and started its own. Evidence is now any standing plant with the same
    ///    SPACING, whatever it is. Carrots, turnips, onions, barley and flax all ripped at
    ///    a 0.5m grow radius (2026-08-16, before 1.0), so a field of them is one grid.
    ///
    /// And the cursor is no longer the fallback. On open ground the lattice is one grid
    /// shared by the whole world - its origin at the world's own zero, turned by GridAngle -
    /// so a bed started today lines up with one started last week a field away, and with one
    /// another player started, without either of them having to find the other. That is
    /// also what makes the oak row hold: every oak planted on open ground is on the same
    /// lattice whether or not the one beside it was found. GridShared switches this back to
    /// "the first plant goes where you aim", for anyone who wants that more.
    ///
    /// <b>Plants in the ground outrank the shared grid</b>, because they are the only record
    /// of a bed there is. A bed laid before this fix, a bed laid against a pin, and a plant
    /// put down free with Shift all sit off the shared grid, and a new plant beside them
    /// continues THEIR rows. That is the "stick to the grid of the first one" rule in its
    /// general form: the first plant decides, and everything after it follows.
    ///
    /// <b>Where two grids meet, the one more plants agree on wins</b> - counted among the
    /// plants within reach of where you are aiming, so it is the bed you are standing at
    /// rather than the biggest bed on the map. A single stray off-grid plant therefore
    /// cannot drag a bed onto its phase, which is the other half of the "rows drift" bug the
    /// held anchor was first written for. Ties keep the grid already in use, so the rows do
    /// not flicker between two beds while the cursor sits on the line between them.
    ///
    /// <b>Plants with different spacing never steer each other.</b> A magecap ripped at
    /// 1.6m and a carrot at 1m; a grid cannot be both, and forcing one onto the other either wastes
    /// ground or plants something with no room to grow. Both still start from the shared
    /// grid's origin on open ground, so their rows meet wherever the spacings coincide.
    ///
    /// <b>A tree that has already grown is not evidence.</b> Plant.Grow replaces the sapling
    /// with the tree prefab, and a grown oak is the same prefab as every wild oak in the
    /// Meadows - nothing on it says it was planted. Counting trees would let the forest steer
    /// an orchard. What keeps a grown orchard extendable instead is the shared grid: an oak
    /// planted on open ground was on it, so a new one beside it lands in line anyway.
    ///
    /// <b>Nor is anything else the world grows by itself</b>, for the same reason, and this
    /// one was caught in review before it shipped. Thicket's seedlings grow into the wild
    /// prefabs themselves - Pickable_Mushroom, Pickable_Thistle, the berry bushes - and a
    /// mushroom seedling is spaced exactly like a carrot. So the first version counted every
    /// wild mushroom and thistle within reach as a carrot bed of one, and a lone mushroom at
    /// the edge of a field beat the shared grid 1 to 0: the new bed took that mushroom's rows,
    /// and a second bed started beside a different mushroom took ITS rows. Two patches of one
    /// field on two grids, which is the bug this file exists to fix. See <see cref="Wild"/>.
    ///
    /// <b>Plants in the ground decide the ANGLE as well as the offset.</b> The first version
    /// voted at the player's GridAngle only, and a bed laid at 0 degrees agrees with nothing
    /// on a lattice turned 22.5 - every plant in it counted as a one-plant grid of its own,
    /// and the next plant started turned rows around one of them. The angle is easy to turn
    /// without meaning to, because the wheel turns it whenever a plant is selected. That was
    /// found in review and is NOT what Robbin reported: his two grids had the same angle and
    /// different rows, which is the offset half above. Middle click as a turn key was
    /// suspected for it and he ruled it out - it stays his turn key. So the vote now reads
    /// the angle off pairs of plants that stand in line (<see cref="Angles"/>) and tries each;
    /// the player's angle wins only where nothing stands in line, which is a lone plant. It
    /// still governs open ground and the pin, which is where turning the grid means something.
    /// </summary>
    internal static class Lattice
    {
        /// <summary>
        /// The least distance evidence is gathered from, in metres. Crops step a metre, so
        /// three cells alone would look barely past the next row; this is the old search
        /// radius, kept as a floor so small grids see no less than they used to.
        /// </summary>
        private const float MinReach = 4f;

        /// <summary>
        /// How many cells evidence is gathered from. Three covers the next plant in a row
        /// (one), the one after a gap left for a path (two), and the diagonal of either.
        /// </summary>
        private const float ReachCells = 3f;

        /// <summary>
        /// How far off a lattice point a plant may stand and still count as on it. Plants the
        /// grid put down sit exactly on it; this only absorbs float error at world scale,
        /// where a coordinate of eight thousand carries about a millimetre. A free-placed
        /// plant lands inside it by chance roughly once in twenty-five at a one-metre step,
        /// which is harmless - it is then genuinely in line.
        /// </summary>
        private const float MaxTolerance = 0.1f;

        public static float Reach(float step)
        {
            return Mathf.Max(MinReach, step * ReachCells);
        }

        // ------------------------------------------------------------------ spacing

        /// <summary>
        /// The distance between rows for a plant of this grow radius.
        ///
        /// GridCell, when set, is one spacing for everything and overrides the plant. Left at
        /// 0 it is the radius twice over - the tightest spacing where two neighbours both
        /// keep the clear ground the game checks for - times the Sowing multiplier.
        /// </summary>
        public static float StepFor(float growRadius)
        {
            var cell = FurrowConfig.GridCell.Value;
            if (cell > 0f) return Mathf.Max(0.1f, cell);

            return Mathf.Max(0.1f, growRadius * 2f * Mathf.Max(0.1f, FurrowConfig.Spacing.Value));
        }

        public static float StepFor(Plant plant)
        {
            return StepFor(plant.m_growRadius);
        }

        /// <summary>
        /// Two spacings are one if they agree to a centimetre. Both come off the same config
        /// and the same kind of prefab field, so this is equality with float noise allowed.
        /// </summary>
        public static bool SameStep(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.01f;
        }

        /// <summary>
        /// Name a plant's spacing in the log, once per plant per session.
        ///
        /// The grow radius is asset data - it lives on the prefab and cannot be read offline -
        /// and the whole grid is built on it. An oak's was never measured before this fix went
        /// in, so the first time each plant is lined up the log says what the game reported.
        /// </summary>
        private static readonly HashSet<string> _described = new HashSet<string>();

        public static void Describe(string name, Plant plant, float step)
        {
            if (plant == null || !_described.Add(name)) return;

            Grove.GrovePlugin.Log.LogInfo(FurrowConfig.GridCell.Value > 0f
                ? "Furrow grid: " + name + " on GridCell's " + step.ToString("0.##")
                  + "m (its own grow radius is " + plant.m_growRadius.ToString("0.##") + "m)."
                : "Furrow grid: " + name + " has a grow radius of "
                  + plant.m_growRadius.ToString("0.##") + "m, so its rows are "
                  + step.ToString("0.##") + "m apart.");
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// The lattice point nearest <paramref name="at"/>, on the lattice through
        /// <paramref name="anchor"/> with this step and angle, dropped onto the ground there.
        ///
        /// Rounded in the lattice's own frame, so a turned grid stays square. The ground
        /// height is re-read at the snapped spot, or a snap across a dip leaves the ghost
        /// floating and vanilla refuses the placement for it.
        /// </summary>
        public static Vector3 Snap(Vector3 anchor, float step, float angle, Vector3 at)
        {
            var into = Quaternion.Euler(0f, -angle, 0f);
            var back = Quaternion.Euler(0f, angle, 0f);

            var local = into * (at - anchor);
            local.x = Mathf.Round(local.x / step) * step;
            local.z = Mathf.Round(local.z / step) * step;

            var snapped = anchor + back * new Vector3(local.x, 0f, local.z);
            snapped.y = at.y;

            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(snapped, out ground))
                snapped.y = ground;

            return snapped;
        }

        /// <summary>
        /// Whether a point stands on the lattice through <paramref name="anchor"/>. XZ only:
        /// the lattice is flat and a bed on a slope is still one bed.
        /// </summary>
        public static bool On(Vector3 point, Vector3 anchor, float step, Quaternion into)
        {
            var local = into * (point - anchor);
            var tolerance = Mathf.Min(MaxTolerance, step * 0.1f);

            return Mathf.Abs(local.x - Mathf.Round(local.x / step) * step) <= tolerance
                && Mathf.Abs(local.z - Mathf.Round(local.z / step) * step) <= tolerance;
        }

        public static float FlatSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        /// <summary>
        /// How far apart two grid angles are, in degrees, counting a square lattice's own
        /// symmetry: it repeats every 90, so 89.9 and 0.1 are a fifth of a degree apart and
        /// 0 and 90 are the same grid.
        /// </summary>
        public static float AngleGap(float a, float b)
        {
            var gap = Mathf.Repeat(a - b, 90f);
            return Mathf.Min(gap, 90f - gap);
        }

        /// <summary>
        /// Two angles closer than this are one angle. A pair of plants the grid put down
        /// gives its rows' direction to within a few hundredths of a degree - float error
        /// over a metre - so half a degree only ever merges a measured angle with the one it
        /// was measured from, and lets the measurement give way to the player's exact 22.5.
        /// </summary>
        public const float SameAngle = 0.5f;

        // ------------------------------------------------------------------ angles

        /// <summary>
        /// Every lattice offset a pair of plants three cells or less apart can stand at, as
        /// its length in cells and the angle it makes with the rows. (1,0) is two plants side
        /// by side in a row, (1,1) the diagonal, (2,1) a knight's move, and so on to (3,3).
        /// Both signs of the angle, because (2,1) and (1,2) lean opposite ways.
        /// </summary>
        private static readonly List<Vector2> _offsets = BuildOffsets();

        private static List<Vector2> BuildOffsets()
        {
            var list = new List<Vector2>();
            for (var a = 1; a <= 3; a++)
                for (var b = 0; b <= a; b++)
                {
                    var length = Mathf.Sqrt(a * a + b * b);
                    var lean = Mathf.Atan2(b, a) * Mathf.Rad2Deg;
                    list.Add(new Vector2(length, lean));
                    if (b > 0 && b < a) list.Add(new Vector2(length, -lean));
                }
            return list;
        }

        /// <summary>
        /// Measured angles closer than this are one bed's angle measured twice, and only the
        /// better-supported one is tried. On() allows a tenth of a metre, which a plant a cell
        /// from the anchor still meets on a lattice turned five or six degrees, so near-copies
        /// of a real angle agree with most of the real bed for free and need only one stray
        /// to outvote it. Found by simulation before this ever ran in game, not by a player.
        /// </summary>
        private const float DistinctAngle = 6f;

        private static readonly List<float> _measured = new List<float>();
        private static readonly List<int> _pairs = new List<int>();
        private static readonly List<float> _candidateAngle = new List<float>();
        private static readonly List<int> _candidatePairs = new List<int>();
        private static readonly List<int> _kept = new List<int>();

        /// <summary>
        /// The angles worth trying for these plants, in the order to try them: the player's
        /// own, <paramref name="held"/> (the angle of the grid in use) when there is one, and
        /// every angle pairs of the plants agree on - ordered by how many pairs stand in line
        /// at each, so the angle the bed's rows actually run at is tried before one only the
        /// player's config names. That order is the vote's tie-break between angles: a real
        /// bed of three and three plants that happen to fall on your own angle by chance tie
        /// on count, and the one that is rows wins. With no pairs at all, which is one plant
        /// or none, the player's angle comes first and then the held one.
        ///
        /// Measured rather than chosen from GridTurnStep's family, because a bed is whatever
        /// angle it was planted at: another player's step, an angle typed into the config, a
        /// bed from before any of this. Two plants the grid put down stand an exact lattice
        /// offset apart, so their distance is a whole number of cells along one of the offsets
        /// above and their bearing, less that offset's lean, is the rows' direction. A pair
        /// that only happens to be the right distance apart - two plants set down by hand -
        /// adds an angle that then has to win the vote on plant count like any other, and a
        /// lattice made of one coincidence does not.
        ///
        /// A single plant has no pairs and gives no angle. That is the honest answer: one
        /// plant is on a lattice at every angle there is, so the player's angle stands.
        /// </summary>
        public static void Angles(List<Vector3> plants, float step, float preferred, float? held,
                                  List<float> angles)
        {
            _measured.Clear();
            _pairs.Clear();

            var tolerance = Mathf.Min(MaxTolerance, step * 0.1f);
            var longest = step * 3f * 1.4143f + tolerance;

            for (var i = 0; i < plants.Count; i++)
            {
                for (var j = i + 1; j < plants.Count; j++)
                {
                    var dx = plants[j].x - plants[i].x;
                    var dz = plants[j].z - plants[i].z;
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance < step * 0.5f || distance > longest) continue;

                    // The bearing in the lattice's own convention: Snap turns a lattice by
                    // Quaternion.Euler(0, angle, 0), which carries the row direction (1,0) to
                    // (cos a, -sin a) in world x and z, so a row's bearing is atan2(-dz, dx).
                    var bearing = Mathf.Atan2(-dz, dx) * Mathf.Rad2Deg;

                    foreach (var offset in _offsets)
                    {
                        if (Mathf.Abs(distance - offset.x * step) > tolerance) continue;
                        Count(Mathf.Repeat(bearing - offset.y, 90f));
                    }
                }
            }

            // The player's angle and the held one are always tried, with whatever pairs stand
            // in line at them.
            _candidateAngle.Clear();
            _candidatePairs.Clear();

            _candidateAngle.Add(Mathf.Repeat(preferred, 90f));
            _candidatePairs.Add(PairsAt(preferred));

            if (held.HasValue && AngleGap(held.Value, preferred) >= SameAngle)
            {
                _candidateAngle.Add(Mathf.Repeat(held.Value, 90f));
                _candidatePairs.Add(PairsAt(held.Value));
            }

            // Then the measured ones, most agreed-on first, no more than six, and none within
            // DistinctAngle of a better one. A bed gives one angle many times over; the rest
            // are near-copies of it or coincidences between hand-placed plants, and past six
            // the vote is paying for lattices that cannot win.
            _kept.Clear();
            for (var taken = 0; taken < 6; taken++)
            {
                var best = -1;
                for (var k = 0; k < _measured.Count; k++)
                {
                    if (_pairs[k] <= 0 || _kept.Contains(k)) continue;

                    var copy = false;
                    foreach (var j in _kept)
                        if (AngleGap(_measured[j], _measured[k]) < DistinctAngle) { copy = true; break; }
                    if (copy) continue;

                    if (best < 0 || _pairs[k] > _pairs[best]) best = k;
                }
                if (best < 0) break;

                _kept.Add(best);

                var known = false;
                foreach (var already in _candidateAngle)
                    if (AngleGap(already, _measured[best]) < SameAngle) { known = true; break; }
                if (known) continue;

                _candidateAngle.Add(_measured[best]);
                _candidatePairs.Add(_pairs[best]);
            }

            // Most pairs first; a stable pick, so equal counts keep the order above - yours,
            // then the held one, then the measured ones by support.
            angles.Clear();
            var used = new bool[_candidateAngle.Count];
            for (var n = 0; n < _candidateAngle.Count; n++)
            {
                var pick = -1;
                for (var k = 0; k < _candidateAngle.Count; k++)
                    if (!used[k] && (pick < 0 || _candidatePairs[k] > _candidatePairs[pick])) pick = k;

                used[pick] = true;
                angles.Add(_candidateAngle[pick]);
            }
        }

        /// <summary>How many measured pairs stand in line at this angle.</summary>
        private static int PairsAt(float angle)
        {
            var total = 0;
            for (var k = 0; k < _measured.Count; k++)
                if (AngleGap(_measured[k], angle) < SameAngle) total += _pairs[k];
            return total;
        }

        private static void Count(float angle)
        {
            for (var k = 0; k < _measured.Count; k++)
            {
                if (AngleGap(_measured[k], angle) >= SameAngle) continue;
                _pairs[k]++;
                return;
            }

            _measured.Add(angle);
            _pairs.Add(1);
        }

        // ------------------------------------------------------------------ evidence

        private static readonly HashSet<int> _seen = new HashSet<int>();

        /// <summary>
        /// Every standing plant within reach of <paramref name="at"/> whose rows are
        /// <paramref name="step"/> apart - the evidence a grid is read from.
        ///
        /// Two sources, because a crop is two different objects over its life. The sapling
        /// stage carries Plant, and every Plant is in SlowUpdate's registry - read directly,
        /// so no physics layer has to be guessed and a plant placed a frame ago is already
        /// there. The grown stage is a separate prefab carrying Pickable, standing exactly
        /// where the sapling stood because Plant.Grow spawns it in place; Pickables have no
        /// registry, so those come from a physics sweep with no layer mask. On a server crops
        /// have time to grow, and a half-harvested field is mostly this second kind.
        ///
        /// A registered Plant with no valid ZNetView is skipped. That is the placement ghost:
        /// it is instantiated with network init suppressed, so it joins SlowUpdate's list
        /// from Plant.Awake but never gets a ZDO. Without the check the ghost would count as
        /// evidence for wherever it is standing, which is everywhere.
        ///
        /// Anything the world spawns by itself is skipped at both stages - see Wild.
        /// </summary>
        public static void Gather(Vector3 at, float step, List<Vector3> found)
        {
            found.Clear();
            _seen.Clear();

            var reach = Reach(step);
            var reachSqr = reach * reach;

            // Read before the saplings, because it is what builds the wild list both loops
            // are filtered on.
            var grown = GrownCrops();

            foreach (var slow in SlowUpdate.GetAllInstaces())
            {
                var plant = slow as Plant;
                if (plant == null) continue;

                ZNetView view;
                if (!plant.TryGetComponent(out view) || !view.IsValid()) continue;

                var pos = plant.transform.position;
                if (FlatSqr(pos, at) > reachSqr) continue;
                if (!SameStep(StepFor(plant), step)) continue;
                if (_wild.Contains(Utils.GetPrefabName(plant.gameObject.name))) continue;
                if (!_seen.Add(plant.gameObject.GetInstanceID())) continue;

                found.Add(pos);
            }

            if (grown.Count == 0) return;

            // A capsule standing on the aim point rather than a sphere around it, so a bed
            // running up a slope is found as far along the slope as across the flat.
            foreach (var hit in Physics.OverlapCapsule(at + Vector3.down * reach,
                                                       at + Vector3.up * reach, reach))
            {
                if (hit == null) continue;

                var pickable = hit.GetComponentInParent<Pickable>();
                if (pickable == null) continue;

                float radius;
                if (!grown.TryGetValue(Utils.GetPrefabName(pickable.gameObject.name), out radius))
                    continue;

                var pos = pickable.transform.position;
                if (FlatSqr(pos, at) > reachSqr) continue;
                if (!SameStep(StepFor(radius), step)) continue;
                if (!_seen.Add(pickable.gameObject.GetInstanceID())) continue;

                found.Add(pos);
            }
        }

        // ------------------------------------------------------------------ the vote

        private struct Scored
        {
            public Vector3 Anchor;
            public float Angle;
            public Quaternion Into;
        }

        private static readonly List<Scored> _scored = new List<Scored>();
        private static readonly List<Vector3> _byDistance = new List<Vector3>();
        private static readonly List<float> _angles = new List<float>();

        // The vote's running answer, as fields rather than a closure so a vote allocates
        // nothing. Only Strongest and Try touch them.
        private static bool _any;
        private static Vector3 _bestAnchor;
        private static float _bestAngle;
        private static int _bestSupport;

        /// <summary>
        /// The lattice most of the evidence agrees on - its anchor and its angle - and how
        /// many plants agree.
        ///
        /// Candidates are tried in order of preference and a later one wins only by having
        /// STRICTLY more plants on it: the grid already in use first, at its own angle; then,
        /// angle by angle in the order <see cref="Angles"/> gives - the angle most pairs of
        /// plants stand in line at first, the player's own first when nothing stands in line -
        /// the shared world grid when it is on offer and the lattice through each plant
        /// nearest-first. That order is the whole tie-break: ties keep what you are already
        /// on, then the angle the rows really run at, then the world grid, then the plant
        /// closest to the cursor. It is what lets the vote run every few frames without the
        /// rows flicking between two beds of equal size.
        ///
        /// It is also what decides a turn. One plant standing alone agrees with every angle
        /// equally, so the player's angle wins it and the rows turn about that plant. Two or
        /// more in line agree only at their own angle, so they win against a turn and the bed
        /// keeps its rows. The pin is how you override a bed, as it always was.
        ///
        /// A candidate whose lattice an earlier one already covers - same angle, on its
        /// points - is skipped, so each distinct grid is counted once and under its
        /// most-preferred name.
        ///
        /// Returns false when nothing stands within reach, which leaves the choice of what an
        /// empty patch of ground means to the caller.
        /// </summary>
        public static bool Strongest(List<Vector3> kin, Vector3 at, float step, float preferred,
                                     Vector3? current, float currentAngle, bool offerWorld,
                                     out Vector3 anchor, out float angle, out int support)
        {
            anchor = Vector3.zero;
            angle = preferred;
            support = 0;
            if (kin.Count == 0) return false;

            _byDistance.Clear();
            _byDistance.AddRange(kin);
            _byDistance.Sort((a, b) => FlatSqr(a, at).CompareTo(FlatSqr(b, at)));

            Angles(kin, step, preferred, current.HasValue ? currentAngle : (float?)null, _angles);

            _scored.Clear();
            _any = false;
            _bestAnchor = Vector3.zero;
            _bestAngle = preferred;
            _bestSupport = 0;

            if (current.HasValue) Try(kin, step, current.Value, currentAngle);

            foreach (var a in _angles)
            {
                if (offerWorld) Try(kin, step, Vector3.zero, a);
                foreach (var plant in _byDistance) Try(kin, step, plant, a);
            }

            anchor = _bestAnchor;
            angle = _bestAngle;
            support = _bestSupport;
            return support > 0;
        }

        private static void Try(List<Vector3> kin, float step, Vector3 candidate, float angle)
        {
            foreach (var done in _scored)
                if (AngleGap(done.Angle, angle) < SameAngle && On(candidate, done.Anchor, step, done.Into))
                    return;

            var into = Quaternion.Euler(0f, -angle, 0f);
            _scored.Add(new Scored { Anchor = candidate, Angle = angle, Into = into });

            var agree = 0;
            foreach (var plant in kin)
                if (On(plant, candidate, step, into)) agree++;

            if (_any && agree <= _bestSupport) return;

            _any = true;
            _bestAnchor = candidate;
            _bestAngle = angle;
            _bestSupport = agree;
        }

        /// <summary>
        /// Split plants into the lattices they stand on, biggest first, for `furrow check`.
        ///
        /// Greedy by size: the anchor and angle that cover the most plants still unclaimed
        /// take them, and the rest go round again. So a bed of five with one stray reads
        /// "5 + 1" and a bed that followed the stray reads "4 + 2", which is the difference a
        /// test has to be able to see. Angles come from the plants' own pairs the same way
        /// the vote finds them, so a bed at another angle than yours still reads as one grid.
        /// </summary>
        public static void Separate(List<Vector3> plants, float step, float preferred,
                                    List<int> sizes, List<float> angles)
        {
            sizes.Clear();
            angles.Clear();

            var left = new List<Vector3>(plants);
            var tried = new List<float>();
            Angles(plants, step, preferred, null, tried);

            while (left.Count > 0)
            {
                var bestCount = 0;
                var bestAnchor = left[0];
                var bestAngle = tried[0];

                foreach (var a in tried)
                {
                    var into = Quaternion.Euler(0f, -a, 0f);
                    foreach (var anchor in left)
                    {
                        var count = 0;
                        foreach (var p in left)
                            if (On(p, anchor, step, into)) count++;

                        if (count <= bestCount) continue;
                        bestCount = count;
                        bestAnchor = anchor;
                        bestAngle = a;
                    }
                }

                var intoBest = Quaternion.Euler(0f, -bestAngle, 0f);
                left.RemoveAll(p => On(p, bestAnchor, step, intoBest));

                sizes.Add(bestCount);
                angles.Add(bestAngle);
            }
        }

        // ------------------------------------------------------------------ grown crops

        private static readonly Dictionary<string, float> _grown = new Dictionary<string, float>();
        private static readonly HashSet<string> _wild = new HashSet<string>();
        private static int _grownForScene;
        private static int _grownForPrefabs = -1;
        private static int _grownForVegetation = -1;

        /// <summary>
        /// Grown-crop prefab names, each with the grow radius of the sapling it came from.
        ///
        /// Read off the world rather than listed, the same way AreaPick finds crops: every
        /// prefab carrying Plant, and whatever is in its m_grownPrefabs that is picked rather
        /// than felled. So a crop another mod adds is evidence the day it is added.
        ///
        /// Three kinds are left out, all for the header's reason - a grown plant only says
        /// "a bed was planted here" when nothing else could have put it there:
        ///
        ///  - anything carrying TreeBase: a grown oak is the same prefab as a wild one;
        ///  - anything the world spawns by itself (<see cref="Wild"/>): the Thicket seedlings
        ///    grow into Pickable_Mushroom, Pickable_Thistle and the berry bushes, and vanilla's
        ///    seed crops and Mistlands mushrooms may well grow into prefabs that also spawn
        ///    wild - which is asset data this cannot see offline, so it reads the world's own
        ///    list rather than guessing one;
        ///  - anything grown from a plant that does not need tilled ground. Every vanilla crop
        ///    ripped with m_needCultivatedGround on, and Thicket turns it off on purpose, so
        ///    this is a second fence around the same wild plants that does not depend on the
        ///    vegetation list being complete. A modded crop grown on grass loses its grown
        ///    stage as evidence and keeps its sapling, which is the cheap side to be wrong on:
        ///    the shared grid still lines it up.
        ///
        /// Rebuilt when the world changes, because ZNetScene is remade on every world load and
        /// a table answered from a stale flag would describe the previous world - and when the
        /// prefab or vegetation list grows, because Thicket registers its seedlings a few
        /// frames after the scene exists and ZoneSystem fills its vegetation in Start. What it
        /// left out is logged once per build, since every part of it is asset data.
        /// </summary>
        private static Dictionary<string, float> GrownCrops()
        {
            var scene = ZNetScene.instance;
            if (scene == null) { _grown.Clear(); _wild.Clear(); return _grown; }

            var zones = ZoneSystem.instance;
            var vegetation = zones != null && zones.m_vegetation != null ? zones.m_vegetation.Count : -1;

            if (_grownForScene == scene.GetInstanceID()
                && _grownForPrefabs == scene.m_prefabs.Count
                && _grownForVegetation == vegetation)
                return _grown;

            _grown.Clear();
            _grownForScene = scene.GetInstanceID();
            _grownForPrefabs = scene.m_prefabs.Count;
            _grownForVegetation = vegetation;

            Wild(zones);

            var skipped = new List<string>();

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                Plant plant;
                if (!prefab.TryGetComponent(out plant) || plant.m_grownPrefabs == null) continue;

                foreach (var grown in plant.m_grownPrefabs)
                {
                    if (grown == null || _grown.ContainsKey(grown.name)) continue;
                    if (grown.GetComponent<Pickable>() == null) continue;
                    if (grown.GetComponentInChildren<TreeBase>(true) != null) continue;

                    if (_wild.Contains(grown.name) || !plant.m_needCultivatedGround)
                    {
                        if (!skipped.Contains(grown.name)) skipped.Add(grown.name);
                        continue;
                    }

                    _grown[grown.name] = plant.m_growRadius;
                }
            }

            if (vegetation < 0)
                Grove.GrovePlugin.Log.LogInfo("Furrow grid: the world's vegetation list is not "
                    + "there yet, so only plants that need tilled ground are told from wild ones "
                    + "for now.");

            if (skipped.Count > 0)
                Grove.GrovePlugin.Log.LogInfo("Furrow grid: these grow wild as well as from a "
                    + "planted seed, so they do not steer the grid when grown: "
                    + string.Join(", ", skipped.ToArray()) + ".");

            return _grown;
        }

        /// <summary>
        /// Every prefab the world places by itself, by name: ZoneSystem.m_vegetation, the list
        /// world generation scatters across every zone. That is what "grows wild" means to
        /// the game, so it is what it means here - read off the running world, and so right
        /// for another mod's wild plants and for whatever 1.0 changed without anyone listing
        /// them. Disabled entries count too; a world generated before they were switched off
        /// still has them standing.
        /// </summary>
        private static void Wild(ZoneSystem zones)
        {
            _wild.Clear();
            if (zones == null || zones.m_vegetation == null) return;

            foreach (var entry in zones.m_vegetation)
                if (entry != null && entry.m_prefab != null) _wild.Add(entry.m_prefab.name);
        }
    }
}
