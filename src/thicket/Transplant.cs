using Grove;
using HarmonyLib;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// Digging a wild plant up, which is the only place an uprooted plant comes from.
    ///
    /// This is the half that turned Thicket from planting into transplanting. The first
    /// design let you grow a new bush from a handful of its berries, and the objection to
    /// it is the objection to every generous version of a thing: the world's bush count
    /// only ever went up. Moving one instead conserves it - the piece costs an uprooted
    /// plant, the uprooted plant only exists because a wild one stopped existing, and
    /// choosing WHERE a bush stands is the whole of what the feature grants.
    ///
    /// The way in rides vanilla's own interaction: every one of these plants already
    /// carries a Pickable, which is Hoverable and Interactable, so the crosshair, the
    /// hover text and the E press all exist. Holding the cultivator is what flips their
    /// meaning from "pick the berries" to "dig the plant" - the tool in your hand is the
    /// statement of intent, and empty hands still pick berries exactly as vanilla does.
    /// </summary>
    [HarmonyPatch]
    internal static class Transplant
    {
        private static readonly AccessTools.FieldRef<Pickable, bool> PickedRef =
            AccessTools.FieldRefAccess<Pickable, bool>("m_picked");

        // GetRightItem is protected, and the field behind it is too.
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> RightItemRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_rightItem");

        /// <summary>
        /// Whether the character is holding the cultivator. By its shared name rather
        /// than the prefab, so a skinned or cloned cultivator from another mod counts.
        /// </summary>
        private static bool HoldingCultivator(Humanoid who)
        {
            if (who == null) return false;

            var item = RightItemRef(who);
            return item != null && item.m_shared != null
                && item.m_shared.m_name == "$item_cultivator";
        }

        /// <summary>The roster plant this pickable is an instance of, or null.</summary>
        private static WildPlant Of(Pickable pickable)
        {
            if (!ThicketConfig.Enabled.Value || pickable == null) return null;

            return WildPlants.OfGrown(Utils.GetPrefabName(pickable.gameObject.name));
        }

        /// <summary>
        /// The offer, written into the plant's own hover text. $KEY_Use renders as the
        /// actually bound key and follows a rebind for free.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pickable), "GetHoverText")]
        private static void Hover(Pickable __instance, ref string __result)
        {
            var plant = Of(__instance);
            if (plant == null || !HoldingCultivator(Player.m_localPlayer)) return;

            // A picked bush answers hover with an empty string, which would leave the
            // dig offer floating over nothing - give it the plant's name to hang from.
            if (string.IsNullOrEmpty(__result))
                __result = plant.Title;

            __result += "\n[<color=yellow><b>$KEY_Use</b></color>] Dig up";
        }

        /// <summary>
        /// The dig itself, in place of the pick. A prefix that returns false so vanilla
        /// never runs: picking and digging are the same button on the same object, and
        /// the cultivator in hand is what chose between them.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Pickable), "Interact")]
        private static bool Dig(Pickable __instance, Humanoid character, bool repeat,
                                ref bool __result)
        {
            if (repeat) return true;

            var plant = Of(__instance);
            if (plant == null || !HoldingCultivator(character)) return true;

            __result = false;

            var player = character as Player;
            if (player == null) return true;

            if (Carry.Carrying)
            {
                player.Message(MessageHud.MessageType.Center, "Hands full");
                return false;
            }

            // The same ladder the replanting piece is gated by, read the same way. A
            // plant you cannot yet replant is a plant you may not uproot - otherwise the
            // gate teaches you to destroy the thing it exists to protect.
            if (plant.Level > 0
                && player.GetSkillFactor(Skills.SkillType.Farming) * 100f < plant.Level)
            {
                player.Message(MessageHud.MessageType.Center,
                    plant.Title + " needs Farming " + plant.Level + " to dig up");
                return false;
            }

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return true;

            // Writing to an object you do not own is silently discarded - this is what
            // vanilla's own Take All does before it touches a container.
            nview.ClaimOwnership();

            var where = __instance.transform.position;

            // An unpicked plant surrenders its pickings too, exactly what picking first
            // and then digging would have given. Dropping them costs nothing and means
            // no order of operations is ever the wrong one.
            if (!PickedRef(__instance) && __instance.m_itemPrefab != null)
            {
                for (var i = 0; i < Mathf.Max(1, __instance.m_amount); i++)
                    Object.Instantiate(__instance.m_itemPrefab,
                        where + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f,
                        Quaternion.identity);
            }

            ZNetScene.instance.Destroy(__instance.gameObject);

            // Into the arms, not the inventory - Carry is the whole second half.
            Carry.Begin(player, plant);
            player.RaiseSkill(Skills.SkillType.Farming, 1f);

            __result = true;
            return false;
        }

        /// <summary>
        /// A transplanted plant arrives picked.
        ///
        /// Plant.Grow instantiates a FRESH copy of the vanilla prefab, and a fresh bush
        /// carries berries - so without this, dig, replant and a short wait would mint a
        /// picking out of nothing, every cycle. Starting picked puts the regrowth timer
        /// where the berries would be: you moved a living plant, and it fruits again on
        /// its own schedule. Grow returns the spawned object and runs on the owner, so
        /// the write lands.
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
