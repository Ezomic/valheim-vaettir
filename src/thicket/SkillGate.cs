using Grove;
using HarmonyLib;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// The Farming ladder: a wild plant you have not earned cannot be planted.
    ///
    /// Valheim has no notion of a piece requiring a skill. Piece.m_resources answers "have
    /// you got the materials", m_craftingStation answers "are you near the bench", and
    /// nothing anywhere answers "are you good enough" - so the gate has to be added, and the
    /// place to add it is Player.HaveRequirements, which is the one question every part of
    /// the build system already routes through. The build menu greys the entry, the ghost
    /// turns red, and the place press is refused, all off one answer.
    ///
    /// Three patches rather than one, because vanilla's own answer arrives in three places
    /// and only the first of them is load-bearing:
    ///
    ///   HaveRequirements   the gate itself. Greyed in the menu, and the place is refused.
    ///   UpdatePlacementGhost   the ghost turns red, so it is visible before you click.
    ///   the press   a line of text saying which level, because everything above this
    ///               is indistinguishable from not having the berries.
    ///
    /// That last one matters more than it looks. Vanilla greys a piece for missing materials
    /// and greys it for this in exactly the same way, so without the message the honest
    /// reading of a locked raspberry bush is "I need more raspberries" - and you go and pick
    /// more, and it stays grey.
    /// </summary>
    internal static class SkillGate
    {
        private static readonly AccessTools.FieldRef<Player, PieceTable> BuildPiecesRef =
            AccessTools.FieldRefAccess<Player, PieceTable>("m_buildPieces");

        private static readonly AccessTools.FieldRef<Player, Player.PlacementStatus> StatusRef =
            AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");

        /// <summary>
        /// Player.SetPlacementGhostValid is private, and setting the status field alone is
        /// not enough to recolour the ghost: vanilla calls this as the very last line of
        /// UpdatePlacementGhost, which is before any postfix of ours runs, so the ghost has
        /// already been told it is valid by the time we disagree. Bound once as a delegate
        /// rather than reflected per frame - this is called every frame the cultivator is out.
        /// </summary>
        private static readonly System.Action<Player, bool> SetGhostValid =
            AccessTools.MethodDelegate<System.Action<Player, bool>>(
                AccessTools.Method(typeof(Player), "SetPlacementGhostValid"));

        /// <summary>So the refusal is said once per press rather than once per frame held.</summary>
        private static float _lastSaid;

        /// <summary>
        /// The level a player has in whatever skill this build table trains.
        ///
        /// The table's own skill rather than Farming by name, matching what vanilla credits
        /// on a successful place. If some other mod ever puts these pieces on a table that
        /// trains something else, the gate reads the same skill the place would have raised,
        /// which is the only self-consistent answer available.
        /// </summary>
        private static float Level(Player player)
        {
            var table = BuildPiecesRef(player);
            var skill = table != null && table.m_skill != Skills.SkillType.None
                ? table.m_skill
                : Skills.SkillType.Farming;

            return player.GetSkillFactor(skill) * 100f;
        }

        /// <summary>
        /// Whether this piece is a wild plant the player has not reached yet.
        /// Null plant, no gate, and level 0 in the row switches the gate off for that plant.
        /// </summary>
        private static WildPlant Locked(Player player, Piece piece)
        {
            if (!ThicketConfig.Enabled.Value) return null;

            var plant = WildPlants.Of(piece);
            if (plant == null || plant.Level <= 0) return null;

            return Level(player) < plant.Level ? plant : null;
        }

        // ------------------------------------------------------------------ the gate

        /// <summary>
        /// IsKnown is deliberately left alone.
        ///
        /// That mode is what decides whether a piece is in the menu at all, and answering
        /// false would hide every locked plant until the level arrived. Hiding them makes
        /// the ladder invisible: there is nothing to work towards and no way to find out
        /// that a blue mushroom can ever be planted. Shown-and-locked, with the level in the
        /// description, is the whole design.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements),
                      typeof(Piece), typeof(Player.RequirementMode))]
        private static void Gate(Player __instance, Piece piece, Player.RequirementMode mode,
                                 ref bool __result)
        {
            if (!__result || mode == Player.RequirementMode.IsKnown) return;
            if (Locked(__instance, piece) == null) return;

            __result = false;
        }

        /// <summary>
        /// A red ghost, so the refusal is visible while you are aiming rather than only after
        /// you click.
        ///
        /// Invalid rather than one of the specific statuses. WrongBiome and NeedCultivated are
        /// already taken by real conditions this same piece can be in, and borrowing one of
        /// them would put a wrong reason on screen - a raspberry bush standing in the meadows
        /// reading "wrong biome" is worse than no reason at all.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Ghost(Player __instance)
        {
            var table = BuildPiecesRef(__instance);
            var piece = table != null ? table.GetSelectedPiece() : null;
            if (piece == null) return;

            if (Locked(__instance, piece) == null) return;

            // The status is what actually refuses the placement: TryPlacePiece calls
            // UpdatePlacementGhost itself and then switches on this field, so Invalid earns
            // the vanilla "$msg_invalidplacement" and returns false. The colour is the
            // second half of the same statement.
            StatusRef(__instance) = Player.PlacementStatus.Invalid;
            if (SetGhostValid != null) SetGhostValid(__instance, false);
        }

        // ------------------------------------------------------------------ saying so

        /// <summary>
        /// Called from the plugin's Update, not from a patch.
        ///
        /// The natural place for this would be inside Gate, and it cannot go there: the Hud
        /// asks HaveRequirements about every piece in the open build menu on every frame, so
        /// a message written from inside it would be sixty lines a second about eight
        /// different plants. Here it is one press, one line.
        /// </summary>
        public static void Tick(Player player)
        {
            if (!ThicketConfig.Enabled.Value || !ThicketConfig.SayTheLevel.Value) return;
            if (player == null || !player.InPlaceMode()) return;

            if (!ZInput.GetButtonDown("Attack") && !ZInput.GetButtonDown("JoyPlace")) return;

            // Guarded because the two buttons can both report a press on the same frame, and
            // because vanilla itself only acts on a press within 0.2s - so a held mouse can
            // re-enter here before the first message has been read.
            if (Time.time - _lastSaid < 0.5f) return;

            var table = BuildPiecesRef(player);
            var piece = table != null ? table.GetSelectedPiece() : null;
            if (piece == null) return;

            var plant = Locked(player, piece);
            if (plant == null) return;

            _lastSaid = Time.time;

            player.Message(MessageHud.MessageType.Center,
                plant.Title + " needs Farming " + plant.Level
                + " ( you have " + Mathf.FloorToInt(Level(player)) + " )");
        }
    }
}
