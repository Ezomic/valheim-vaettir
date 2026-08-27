using Grove;
using HarmonyLib;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// Digging a wild plant up, from the cultivator's own menu.
    ///
    /// The second interaction design, and the reason is worth keeping: the first rode
    /// Pickable.Interact - hold the cultivator, press E on the bush - and could never
    /// fire, because a drawn cultivator puts the player in PLACE MODE, where the game
    /// suppresses the whole hover/interact pipeline. Nothing hovered, E placed. He
    /// found it, and he named the fix: "maybe add an option to the cultivator menu."
    ///
    /// So Transplant is now a menu entry beside the crops, wearing m_repairPiece the
    /// way the hammer's repair does - the one kind of selected piece that CLICKS ON
    /// THE WORLD instead of placing into it. Player.UpdatePlacement routes the press
    /// to Player.Repair, stamina and build-mode already handled; a prefix takes over
    /// when the selected piece is ours: click a wild roster plant and it goes into
    /// your arms (Carry.cs); click open ground while carrying and it goes back down.
    /// Vanilla's repair never runs for this piece - GetHoveringPiece only finds
    /// Pieces, and a bush is not one, so there is nothing to fight over.
    /// </summary>
    [HarmonyPatch]
    internal static class Transplant
    {
        private static readonly AccessTools.FieldRef<Pickable, bool> PickedRef =
            AccessTools.FieldRefAccess<Pickable, bool>("m_picked");

        /// <summary>The roster plant this pickable is an instance of, or null.</summary>
        private static WildPlant Of(Pickable pickable)
        {
            if (!ThicketConfig.Enabled.Value || pickable == null) return null;

            return WildPlants.OfGrown(Utils.GetPrefabName(pickable.gameObject.name));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Repair")]
        private static bool Click(Player __instance, Piece repairPiece)
        {
            if (repairPiece == null
                || repairPiece.gameObject.name != WildPrefab.ToolName) return true;

            if (Carry.Carrying)
            {
                Carry.TryPlantFromTool(__instance);
                return false;
            }

            var pickable = UnderCrosshair(__instance);
            var plant = Of(pickable);
            if (plant == null)
            {
                __instance.Message(MessageHud.MessageType.Center,
                    "Nothing here to dig up");
                return false;
            }

            Dig(__instance, pickable, plant);
            return false;
        }

        /// <summary>
        /// Our own ray, because repair mode's GetHoveringPiece only finds Pieces and a
        /// bush is not one. Same camera, same reach a repair click has.
        /// </summary>
        private static Pickable UnderCrosshair(Player player)
        {
            var camera = GameCamera.instance;
            if (camera == null) return null;

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            foreach (var hit in Physics.RaycastAll(ray, 8f))
            {
                if (hit.collider == null) continue;

                var pickable = hit.collider.GetComponentInParent<Pickable>();
                if (pickable == null) continue;
                if (Vector3.Distance(hit.point, player.transform.position) > 6f) continue;

                return pickable;
            }
            return null;
        }

        private static void Dig(Player player, Pickable pickable, WildPlant plant)
        {
            // The same ladder replanting is gated by. A plant you cannot yet replant is
            // a plant you may not uproot - otherwise the gate teaches you to destroy
            // the thing it exists to protect.
            if (plant.Level > 0
                && player.GetSkillFactor(Skills.SkillType.Farming) * 100f < plant.Level)
            {
                player.Message(MessageHud.MessageType.Center,
                    plant.Title + " needs Farming " + plant.Level + " to dig up");
                return;
            }

            var nview = pickable.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            // Writing to an object you do not own is silently discarded - this is what
            // vanilla's own Take All does before it touches a container.
            nview.ClaimOwnership();

            var where = pickable.transform.position;

            // An unpicked plant surrenders its pickings too, exactly what picking first
            // and then digging would have given. No order of operations is the wrong one.
            if (!PickedRef(pickable) && pickable.m_itemPrefab != null)
            {
                for (var i = 0; i < Mathf.Max(1, pickable.m_amount); i++)
                    Object.Instantiate(pickable.m_itemPrefab,
                        where + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f,
                        Quaternion.identity);
            }

            ZNetScene.instance.Destroy(pickable.gameObject);

            // Into the arms, not the inventory - Carry is the whole second half.
            Carry.Begin(player, plant);
            player.RaiseSkill(Skills.SkillType.Farming, 1f);
        }

        /// <summary>
        /// A transplanted plant arrives picked.
        ///
        /// Plant.Grow instantiates a FRESH copy of the vanilla prefab, and a fresh bush
        /// carries berries - so without this, dig, replant and a short wait would mint
        /// a picking out of nothing, every cycle. Starting picked puts the regrowth
        /// timer where the berries would be. Grow returns the spawned object and runs
        /// on the owner, so the write lands.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Plant), "Grow")]
        private static void Grown(Plant __instance, GameObject __result)
        {
            if (__result == null) return;
            if (WildPlants.OfPieceName(Utils.GetPrefabName(__instance.gameObject.name)) == null)
                return;

            Pickable pickable;
            if (__result.TryGetComponent(out pickable))
                pickable.SetPicked(true);
        }
    }
}
