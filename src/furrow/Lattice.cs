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
        /// </summary>
        public static void Gather(Vector3 at, float step, List<Vector3> found)
        {
            found.Clear();
            _seen.Clear();

            var reach = Reach(step);
            var reachSqr = reach * reach;

            foreach (var slow in SlowUpdate.GetAllInstaces())
            {
                var plant = slow as Plant;
                if (plant == null) continue;

                ZNetView view;
                if (!plant.TryGetComponent(out view) || !view.IsValid()) continue;

                var pos = plant.transform.position;
                if (FlatSqr(pos, at) > reachSqr) continue;
                if (!SameStep(StepFor(plant), step)) continue;
                if (!_seen.Add(plant.gameObject.GetInstanceID())) continue;

                found.Add(pos);
            }

            var grown = GrownCrops();
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

        private static readonly List<Vector3> _candidates = new List<Vector3>();
        private static readonly List<Vector3> _scored = new List<Vector3>();
        private static readonly List<Vector3> _byDistance = new List<Vector3>();

        /// <summary>
        /// The lattice most of the evidence agrees on, and how many plants agree.
        ///
        /// Candidates are tried in order of preference and a later one wins only by having
        /// STRICTLY more plants on it: the grid already in use first, then the shared world
        /// grid when it is on offer, then the lattice through each plant nearest-first. That
        /// order is the whole tie-break - ties keep what you are already on, then the world
        /// grid, then the plant closest to the cursor - and it is what lets the vote run every
        /// few frames without the rows flicking between two beds of equal size.
        ///
        /// A candidate whose lattice an earlier one already covers is skipped, so each
        /// distinct grid is counted once and under its most-preferred name.
        ///
        /// Returns false when nothing stands within reach, which leaves the choice of what an
        /// empty patch of ground means to the caller.
        /// </summary>
        public static bool Strongest(List<Vector3> kin, Vector3 at, float step, float angle,
                                     Vector3? current, bool offerWorld,
                                     out Vector3 anchor, out int support)
        {
            anchor = Vector3.zero;
            support = 0;
            if (kin.Count == 0) return false;

            var into = Quaternion.Euler(0f, -angle, 0f);

            _byDistance.Clear();
            _byDistance.AddRange(kin);
            _byDistance.Sort((a, b) => FlatSqr(a, at).CompareTo(FlatSqr(b, at)));

            _candidates.Clear();
            if (current.HasValue) _candidates.Add(current.Value);
            if (offerWorld) _candidates.Add(Vector3.zero);
            _candidates.AddRange(_byDistance);

            _scored.Clear();
            var any = false;

            foreach (var candidate in _candidates)
            {
                var covered = false;
                foreach (var done in _scored)
                    if (On(candidate, done, step, into)) { covered = true; break; }
                if (covered) continue;

                _scored.Add(candidate);

                var agree = 0;
                foreach (var plant in kin)
                    if (On(plant, candidate, step, into)) agree++;

                if (any && agree <= support) continue;

                any = true;
                anchor = candidate;
                support = agree;
            }

            return support > 0;
        }

        // ------------------------------------------------------------------ grown crops

        private static readonly Dictionary<string, float> _grown = new Dictionary<string, float>();
        private static int _grownForScene;

        /// <summary>
        /// Grown-crop prefab names, each with the grow radius of the sapling it came from.
        ///
        /// Read off the world rather than listed, the same way AreaPick finds crops: every
        /// prefab carrying Plant, and whatever is in its m_grownPrefabs that is picked rather
        /// than felled. So a crop another mod adds is evidence the day it is added.
        ///
        /// Anything grown that carries TreeBase is left out on purpose - see the header: a
        /// grown oak cannot be told from a wild one. Rebuilt when the world changes, because
        /// ZNetScene is remade on every world load and a table answered from a stale flag
        /// would describe the previous world.
        /// </summary>
        private static Dictionary<string, float> GrownCrops()
        {
            var scene = ZNetScene.instance;
            if (scene == null) { _grown.Clear(); return _grown; }

            if (_grownForScene == scene.GetInstanceID()) return _grown;

            _grown.Clear();
            _grownForScene = scene.GetInstanceID();

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

                    _grown[grown.name] = plant.m_growRadius;
                }
            }

            return _grown;
        }
    }
}
