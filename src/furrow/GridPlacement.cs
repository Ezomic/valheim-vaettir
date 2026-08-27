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
    /// onto a lattice anchored on the NEAREST PLANT OF THE SAME KIND, spaced by the
    /// plant's own grow radius. The first plant of a bed goes wherever you like and
    /// becomes the anchor; every later one clicks into its rows. No nearby kin, no
    /// snap - so the feature never fights you on a fresh patch.
    ///
    /// World-aligned axes rather than anchor-rotated: rows running north-south are
    /// predictable from any approach angle, which is what a grid is for.
    /// </summary>
    [HarmonyPatch]
    internal static class GridPlacement
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Snap(Player __instance)
        {
            if (!FurrowConfig.GridEnabled.Value) return;
            if (__instance != Player.m_localPlayer) return;

            var ghost = GhostRef(__instance);
            if (ghost == null || !ghost.activeSelf) return;

            Plant ghostPlant;
            if (!ghost.TryGetComponent(out ghostPlant)) return;

            if (__instance.GetSkillFactor(Skills.SkillType.Farming) * 100f
                < FurrowConfig.GridLevel.Value) return;

            var step = Mathf.Max(0.1f,
                ghostPlant.m_growRadius * 2f
                * Mathf.Max(0.1f, FurrowConfig.Spacing.Value));

            var found = Nearest(ghost);
            if (found == null) return;
            var anchor = found.Value;

            var at = ghost.transform.position;
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
        /// The nearest standing plant of the same prefab, because a carrot grid and a
        /// turnip grid interleaved is somebody's garden design, not a mistake to
        /// correct. The ghost's own name carries "(Clone)".
        /// </summary>
        private static Vector3? Nearest(GameObject ghost)
        {
            var name = Utils.GetPrefabName(ghost.name);
            var at = ghost.transform.position;

            Vector3? best = null;
            var bestSqr = 16f;   // 4m: past that you are starting a new bed

            foreach (var hit in Physics.OverlapSphere(at, 4f))
            {
                if (hit == null) continue;

                var plant = hit.GetComponentInParent<Plant>();
                if (plant == null || plant.gameObject == ghost) continue;
                if (Utils.GetPrefabName(plant.gameObject.name) != name) continue;

                var d = (plant.transform.position - at).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = plant.transform.position;
                }
            }

            return best;
        }
    }
}
