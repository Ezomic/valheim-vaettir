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

            Pickable stranger;
            var pickable = UnderCrosshair(__instance, out stranger);
            var plant = Of(pickable);
            if (plant == null)
            {
                // Naming what was actually under the cursor, when something was. "Nothing
                // here to dig up" while a bush fills the screen reads as the mod being
                // broken, and for a while it half was - see UnderCrosshair.
                __instance.Message(MessageHud.MessageType.Center,
                    stranger != null
                        ? Humanise(stranger) + " cannot be dug up"
                        : "Nothing here to dig up");
                return false;
            }

            Dig(__instance, pickable, plant);
            return false;
        }

        /// <summary>
        /// The roster plant the player is aiming at, or null - with anything else that
        /// was under the cursor handed back so the refusal can name it.
        ///
        /// Our own ray, because repair mode's GetHoveringPiece only finds Pieces and a
        /// bush is not one. Three things make it find what the player means:
        ///
        /// RaycastAll's hits arrive in ARBITRARY order, not nearest first. This used to
        /// take the first pickable in that array and hand it straight back, so a bush
        /// standing behind a mushroom - or a mushroom whose collider merely happened to
        /// be listed first - answered for the plant actually aimed at, and the dig was
        /// refused with a bush filling the screen. That is the intermittent "sometimes
        /// it says nothing to dig up": nothing about it depended on where you pointed.
        ///
        /// So the hits are sorted, and a plant ON THE ROSTER always beats one that is
        /// not, however close the stranger. Being nearer is not being what was meant.
        ///
        /// And a miss falls back to a cone: wild plants are small, ragged, and often
        /// have a collider narrower than they look, so a ray through the exact centre
        /// of the crosshair is a harder shot than the plant appears to be. The cone
        /// picks the plant closest to the aim LINE rather than the nearest one, so it
        /// resolves the way a player expects when two stand side by side.
        /// </summary>
        private static Pickable UnderCrosshair(Player player, out Pickable stranger)
        {
            stranger = null;

            var camera = GameCamera.instance;
            if (camera == null) return null;

            var reach = Mathf.Max(1f, ThicketConfig.DigReach.Value);
            var eye = camera.transform.position;
            var forward = camera.transform.forward;

            // Reach is measured from the player and the ray starts at the camera, which
            // on a third-person shoulder sits behind them - so the ray is given room and
            // the reach test below is what actually decides.
            var hits = Physics.RaycastAll(new Ray(eye, forward), reach + 6f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;

                var pickable = hit.collider.GetComponentInParent<Pickable>();
                if (pickable == null) continue;
                if (Vector3.Distance(pickable.transform.position,
                                     player.transform.position) > reach) continue;

                if (Of(pickable) != null) return pickable;
                if (stranger == null) stranger = pickable;
            }

            var cone = ThicketConfig.DigAssist.Value;
            if (cone <= 0f) return null;

            Pickable best = null;
            var widest = Mathf.Cos(Mathf.Clamp(cone, 0f, 89f) * Mathf.Deg2Rad);

            foreach (var collider in Physics.OverlapSphere(player.transform.position, reach))
            {
                if (collider == null) continue;

                var pickable = collider.GetComponentInParent<Pickable>();
                if (pickable == null || Of(pickable) == null) continue;

                // Aimed at the middle of the plant rather than its base, or a bush at
                // your feet is always behind you by a couple of degrees.
                var to = pickable.transform.position + Vector3.up * 0.3f - eye;
                if (to.sqrMagnitude < 0.0001f) continue;

                var alignment = Vector3.Dot(to.normalized, forward);
                if (alignment <= widest) continue;

                widest = alignment;
                best = pickable;
            }

            return best;
        }

        /// <summary>The prefab name of something the player can see, made readable.</summary>
        private static string Humanise(Pickable pickable)
        {
            var name = Utils.GetPrefabName(pickable.gameObject.name);

            // Vanilla's own display name when there is one - a Pickable carries the
            // token it shows on hover, and "$piece_mushroom" localises to what the
            // player already calls it.
            if (!string.IsNullOrEmpty(pickable.m_overrideName))
                return Localization.instance.Localize(pickable.m_overrideName);

            if (pickable.m_itemPrefab != null)
            {
                var drop = pickable.m_itemPrefab.GetComponent<ItemDrop>();
                if (drop != null && drop.m_itemData != null
                    && drop.m_itemData.m_shared != null)
                    return Localization.instance.Localize(
                        drop.m_itemData.m_shared.m_name);
            }

            return name;
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
            var wasPicked = PickedRef(pickable);

            // An unpicked plant surrenders its pickings too, exactly what picking first
            // and then digging would have given. No order of operations is the wrong one.
            if (!wasPicked && pickable.m_itemPrefab != null)
            {
                for (var i = 0; i < Mathf.Max(1, pickable.m_amount); i++)
                    Object.Instantiate(pickable.m_itemPrefab,
                        where + Vector3.up * 0.3f + Random.insideUnitSphere * 0.2f,
                        Quaternion.identity);
            }

            ZNetScene.instance.Destroy(pickable.gameObject);

            // Into the arms, not the inventory - Carry is the whole second half.
            Carry.Begin(player, plant, wasPicked);
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
