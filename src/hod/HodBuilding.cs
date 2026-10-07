using HarmonyLib;
using Grove;

namespace Hod
{
    /// <summary>
    /// The hammer, served by the same chests as the bench (LHM-76).
    ///
    /// Nothing here counts or spends anything itself. It opens the scope the crafting patches
    /// already answer to (HodCrafting's depth, plus <see cref="HodScope.Building"/>, which makes
    /// the scope resolve from the player instead of from a station) around the hammer's own
    /// questions, and adds the one thing a placement needs that a craft does not.
    ///
    /// The questions, and the vanilla method that asks each:
    ///
    ///   Player.HaveRequirements(Piece, mode)    the gate on a click, and the menu's greying
    ///                                           (Hud.UpdatePieceBuildStatus). CanBuild and
    ///                                           CanAlmostBuild only; IsKnown is about which
    ///                                           pieces are LISTED and stays vanilla, so a
    ///                                           material never carried still hides its piece.
    ///   Hud.SetupPieceInfo                      the requirement lines of the build panel, whose
    ///                                           red flash and (through HodRequirement) the
    ///                                           have/need +chest figure read CountItems.
    ///   Player.UpdatePlacement                  the click. Scoped for the spend, which is
    ///                                           ConsumeResources, already bracketed for the
    ///                                           craft: inventory first, then the chests this
    ///                                           client owns, in the order the bench uses.
    ///   Player.TryPlacePiece                    the refusal described below.
    ///
    /// Repair and removal spend no material in vanilla (Player.Repair calls
    /// WearNTear.Repair and charges stamina and durability), and there is no upgrade-in-place
    /// for pieces, so nothing else needs a bracket.
    ///
    /// <b>Why the click can be refused.</b> A craft has a two second timer in which a fetch from
    /// another player's chest can land. A placement asks, builds and spends in ONE frame
    /// (Player.UpdatePlacement). The count is optimistic by design, so a piece can look
    /// affordable on stock in a chest this client does not own, and nothing may write to such a
    /// chest synchronously (ClaimOwnership is not a lock; see HodWithdraw). Left alone, the
    /// spend would pay only the part it could reach and the piece would still be placed, cheap.
    /// So TryPlacePiece is prefixed: when pack plus the chests this client owns cannot cover the
    /// piece, the click asks the owners for the rest and is refused, and the next click finds it
    /// in the pack. Where every chest is owned (singleplayer, a listen host, a chest you are
    /// standing at) the first click builds and this never fires.
    ///
    /// Free building (the noplacementcost cheat, the world's free-build keys) skips the gate and
    /// the spend in vanilla, so it skips this.
    /// </summary>
    internal static class HodBuilding
    {
        // ------------------------------------------------------------------ brackets

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements),
            typeof(Piece), typeof(Player.RequirementMode))]
        private static void GateStart(Player.RequirementMode mode, out bool __state)
        {
            __state = mode != Player.RequirementMode.IsKnown && HodConfig.Enabled.Value;
            if (__state) Open();
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements),
            typeof(Piece), typeof(Player.RequirementMode))]
        private static void GateEnd(bool __state)
        {
            if (__state) Close();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
        private static void PanelStart(out bool __state)
        {
            __state = HodConfig.Enabled.Value;
            if (__state) Open();
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
        private static void PanelEnd(bool __state)
        {
            if (__state) Close();
        }

        /// <summary>
        /// Scope only: no counting depth is opened here, because the questions inside a
        /// placement that count (the gate) and spend (ConsumeResources) bracket themselves.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        private static void PlacementStart() { HodScope.Building++; }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        private static void PlacementEnd() { if (HodScope.Building > 0) HodScope.Building--; }

        private static void Open()
        {
            HodScope.Building++;
            HodCrafting.OpenScope();
        }

        private static void Close()
        {
            HodCrafting.CloseScope();
            if (HodScope.Building > 0) HodScope.Building--;
        }

        // ------------------------------------------------------------------ the click

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool PayBeforePlacing(Player __instance, Piece piece)
        {
            // Only the placement click. UpdatePlacement is the one caller that holds the scope.
            if (HodScope.Building <= 0 || piece == null) return true;
            if (!HodConfig.Enabled.Value || __instance != Player.m_localPlayer) return true;
            if (__instance.NoCostCheat()) return true;

            var zone = ZoneSystem.instance;
            if (zone != null && zone.GetGlobalKey(piece.FreeBuildKey())) return true;

            // Vanilla names its own reason for a spot it will not build on, and that is the
            // better message. The status is last frame's, which is what the ghost showed.
            if (__instance.GetPlacementStatus() != Player.PlacementStatus.Valid) return true;

            // Nothing in reach: the gate was vanilla's and the spend will be too.
            if (!HodScope.IsOpen) return true;

            if (CanPayFor(__instance, piece)) return true;

            var asked = false;

            foreach (var requirement in piece.m_resources)
            {
                if (!Counts(requirement)) continue;

                var name = requirement.m_resItem.m_itemData.m_shared.m_name;
                var need = requirement.GetAmount(0);
                var have = HodCrafting.Spendable(__instance, name, -1);
                if (have >= need) continue;

                asked |= HodCrafting.Ask(__instance, name, -1, need - have);
            }

            var message = asked ? HodConfig.BuildFetchMessage.Value : HodConfig.ShortMessage.Value;
            if (!string.IsNullOrEmpty(message))
                __instance.Message(MessageHud.MessageType.Center, message);

            if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo("Refused to place " + piece.name + ": "
                    + (asked ? "fetching the rest from chests this client does not own."
                             : "the chests in reach could not supply it."));

            return false;
        }

        /// <summary>
        /// The lines vanilla's own build gate counts: a resource with an item and an amount. No
        /// upgrader filter, because HaveRequirements(Piece, ...) has none.
        /// </summary>
        private static bool Counts(Piece.Requirement requirement)
        {
            return requirement != null && requirement.m_resItem != null && requirement.m_amount > 0;
        }

        private static bool CanPayFor(Player player, Piece piece)
        {
            foreach (var requirement in piece.m_resources)
            {
                if (!Counts(requirement)) continue;

                var name = requirement.m_resItem.m_itemData.m_shared.m_name;
                if (HodCrafting.Spendable(player, name, -1) < requirement.GetAmount(0)) return false;
            }

            return true;
        }
    }
}
