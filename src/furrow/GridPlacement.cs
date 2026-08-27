using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// The grid he actually asked for: the cultivator's ghost snaps so hand-placed
    /// plants land in rows and columns.
    ///
    /// The first reading of "from level 10 farming there is a grid" was a grid-shaped
    /// MULTI-sow - one press planting a square - and he struck it the moment he met
    /// it: "when planting carrot it plants 3. i just want a grid so i can place them
    /// myself in proper grid rows and columns." So sowing is one seed per press, as
    /// vanilla, and the skill unlock is alignment: from GridLevel up, the ghost pulls
    /// onto a lattice anchored on a nearby plant of the same crop, spaced by the
    /// plant's own grow radius. The first plant of a bed goes wherever you like and
    /// becomes the anchor; every later one clicks into its rows. No nearby kin, no
    /// snap - so the feature never fights you on a fresh patch.
    ///
    /// World-aligned axes rather than anchor-rotated: rows running north-south are
    /// predictable from any approach angle, which is what a grid is for.
    ///
    /// Two rules here are the fix for "rows drift out of line when planting", which
    /// only ever showed on the live server. With world axes the lattice's PHASE is
    /// the anchor's position mod the step, so every distinct free-placed plant seeds
    /// a different phase - and the first version re-picked "nearest plant" every
    /// frame, shifting the whole grid by the phase difference each time the anchor
    /// flipped to an off-lattice plant. Worse, it matched the sapling prefab only:
    /// on a server crops have time to GROW, and a grown carrot is a different prefab
    /// carrying Pickable, not Plant - so replanting a half-harvested field found no
    /// kin at all, free-placed, and every new row seeded its own phase. Hence:
    ///   - the anchor is HELD once found, and only re-derived when it falls out of
    ///     range, the crop changes, or the tool goes away - one bed, one phase;
    ///   - kin is the crop, not the prefab: the sapling AND everything in its
    ///     m_grownPrefabs, which stand exactly where the sapling did because
    ///     Plant.Grow spawns them in place.
    /// (FarmGrid and PlantEasily solve the same drift with on-grid pair detection
    /// and snap hysteresis; the held anchor buys the same stability inside this
    /// design's world-aligned axes without either.)
    /// </summary>
    [HarmonyPatch]
    internal static class GridPlacement
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        // The held lattice phase. A position, deliberately not the plant itself -
        // the phase stays valid after the anchor plant is harvested or destroyed,
        // and a Vector3 cannot become a dead UnityEngine.Object mid-frame.
        private static Vector3? _anchor;
        private static string _anchorCrop;

        // Kin lookup per crop, so the grown-prefab names are not re-read per frame.
        private static string _kinCrop;
        private static readonly HashSet<string> _kin = new HashSet<string>();

        private const float SearchRadius = 4f;   // past that you are starting a new bed
        private const float HoldRadius = 8f;     // hold the phase across a whole bed:
                                                 // re-picking at the search edge is what
                                                 // let the phase churn in the first place

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Snap(Player __instance)
        {
            if (!FurrowConfig.GridEnabled.Value) return;
            if (__instance != Player.m_localPlayer) return;

            var ghost = GhostRef(__instance);
            if (ghost == null || !ghost.activeSelf)
            {
                // Tool away is the end of the planting sweep. Dropping the anchor
                // here is safe because every plant this grid placed shares its
                // phase, so the re-pick on the next sweep lands on the same lattice.
                _anchor = null;
                return;
            }

            Plant ghostPlant;
            if (!ghost.TryGetComponent(out ghostPlant)) return;

            if (__instance.GetSkillFactor(Skills.SkillType.Farming) * 100f
                < FurrowConfig.GridLevel.Value) return;

            var step = Mathf.Max(0.1f,
                ghostPlant.m_growRadius * 2f
                * Mathf.Max(0.1f, FurrowConfig.Spacing.Value));

            var crop = Utils.GetPrefabName(ghost.name);
            var at = ghost.transform.position;

            if (_anchorCrop != crop
                || (_anchor.HasValue && FlatSqr(_anchor.Value, at) > HoldRadius * HoldRadius))
                _anchor = null;

            if (!_anchor.HasValue)
            {
                _anchor = NearestKin(ghostPlant, crop, at);
                _anchorCrop = crop;
                if (!_anchor.HasValue) return;
            }
            var anchor = _anchor.Value;

            var snapped = new Vector3(
                anchor.x + Mathf.Round((at.x - anchor.x) / step) * step,
                at.y,
                anchor.z + Mathf.Round((at.z - anchor.z) / step) * step);

            // Follow the terrain at the snapped spot, or a snap across a dip leaves
            // the ghost floating and vanilla refuses the placement for it.
            float ground;
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetGroundHeight(snapped, out ground))
                snapped.y = ground;

            ghost.transform.position = snapped;
        }

        /// <summary>
        /// The nearest standing plant of the same crop, because a carrot grid and a
        /// turnip grid interleaved is somebody's garden design, not a mistake to
        /// correct. "Same crop" spans the growth stages - the sapling prefab and its
        /// m_grownPrefabs - and the grown stage carries Pickable rather than Plant,
        /// so both components are consulted. Distances are XZ only: on a slope the
        /// height difference must not decide which plant seeds the lattice.
        /// </summary>
        private static Vector3? NearestKin(Plant ghostPlant, string crop, Vector3 at)
        {
            if (_kinCrop != crop)
            {
                _kin.Clear();
                _kin.Add(crop);
                if (ghostPlant.m_grownPrefabs != null)
                    foreach (var grown in ghostPlant.m_grownPrefabs)
                        if (grown != null) _kin.Add(grown.name);
                _kinCrop = crop;
            }

            Vector3? best = null;
            var bestSqr = SearchRadius * SearchRadius;

            foreach (var hit in Physics.OverlapSphere(at, SearchRadius))
            {
                if (hit == null) continue;

                Vector3 pos;
                var plant = hit.GetComponentInParent<Plant>();
                if (plant != null && plant.gameObject != ghostPlant.gameObject
                    && _kin.Contains(Utils.GetPrefabName(plant.gameObject.name)))
                {
                    pos = plant.transform.position;
                }
                else
                {
                    var pickable = hit.GetComponentInParent<Pickable>();
                    if (pickable == null
                        || !_kin.Contains(Utils.GetPrefabName(pickable.gameObject.name)))
                        continue;
                    pos = pickable.transform.position;
                }

                var d = FlatSqr(pos, at);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = pos;
                }
            }

            return best;
        }

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
