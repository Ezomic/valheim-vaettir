using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// A sapling belongs in the woods, not in anybody's home.
    ///
    /// This exists because of what the seed does now. A passive sapling planted in someone
    /// else's base was rude; one that summons waves of greydwarfs at it is a weapon, and on
    /// a public server it is the obvious grief - plant a dozen around a stranger's longhouse
    /// and walk away.
    ///
    /// Vanilla already answers half of it. The sapling is an ordinary Piece placed through
    /// Player.PlacePiece, so a ward refuses it outright: UpdatePlacementGhost sets
    /// PlacementStatus.PrivateZone and TryPlacePiece returns false with $msg_privatezone.
    /// Anyone who has warded their base was never exposed to this.
    ///
    /// The gap is unwarded bases, which is most of them, and the game answers that too:
    /// EffectArea.Type.PlayerBase is what a workbench or a fire radiates, and it is the same
    /// test vanilla's own spawn system uses to keep creatures from appearing in your house.
    /// Riding it means "is this someone's home" is the game's question rather than a guess
    /// of ours, and it comes with the counter-play built in - the answer to a sapling
    /// planted next to you is to put a workbench down, not to fight it.
    ///
    /// Any base rather than only someone else's. Ownership would have to be inferred from
    /// Piece.m_creator on whatever happens to be nearby, which is more code to reach a worse
    /// answer: it would still block you at a friend's base in co-op, and stopping you
    /// besieging your own home is correct anyway. The piece is a wilderness ritual and the
    /// instructions have always said to plant it somewhere greydwarfs will come.
    ///
    /// Honest limit: this is client-side placement, so a modded client could ignore it. What
    /// makes it stick on a server is Core's version gate - everyone is running the same
    /// build or they do not connect.
    /// </summary>
    internal static class Wilderness
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostRef =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly AccessTools.FieldRef<Player, PieceTable> BuildPiecesRef =
            AccessTools.FieldRefAccess<Player, PieceTable>("m_buildPieces");

        private static readonly AccessTools.FieldRef<Player, Player.PlacementStatus> StatusRef =
            AccessTools.FieldRefAccess<Player, Player.PlacementStatus>("m_placementStatus");

        /// <summary>
        /// Player.SetPlacementGhostValid is private, and setting the status alone does not
        /// recolour the ghost: vanilla calls this as the last line of UpdatePlacementGhost,
        /// which is before any postfix of ours runs. Bound once rather than reflected per
        /// frame - this is reached every frame the cultivator is out.
        /// </summary>
        private static readonly System.Action<Player, bool> SetGhostValid =
            AccessTools.MethodDelegate<System.Action<Player, bool>>(
                AccessTools.Method(typeof(Player), "SetPlacementGhostValid"));

        private static float _lastSaid;

        /// <summary>
        /// Whether this spot counts as inside somebody's base.
        ///
        /// The margin is added on top of whatever radius the game's own areas already have,
        /// so a server that wants a wider skirt around a base can have one without this
        /// having to know anything about workbenches.
        /// </summary>
        internal static bool InsideBase(Vector3 at)
        {
            if (!GroveConfig.NotInBases.Value) return false;

            // Unity overloads ==, so this is a real null check on a Component. Never ?. or
            // ??, which bypass that overload and pass a destroyed object through.
            return EffectArea.IsPointInsideArea(at, EffectArea.Type.PlayerBase,
                                                Mathf.Max(0f, GroveConfig.BaseMargin.Value))
                   != null;
        }

        /// <summary>Whether the piece being placed is the ancient sapling. The placement
        /// ghost is a clone, so Unity has appended "(Clone)" to its name.</summary>
        private static bool IsSapling(Piece piece)
        {
            if (piece == null) return false;

            var name = piece.gameObject.name;
            if (name == SaplingPrefab.Name) return true;

            var clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
            return clone > 0 && name.Substring(0, clone) == SaplingPrefab.Name;
        }

        private static Vector3 GhostAt(Player player, out bool have)
        {
            var ghost = GhostRef(player);
            have = ghost != null;
            return have ? ghost.transform.position : Vector3.zero;
        }

        // ------------------------------------------------------------------ the ghost

        /// <summary>
        /// Red before you click, rather than a refusal after.
        ///
        /// Invalid rather than one of the named statuses. PrivateZone and NoBuildZone are
        /// taken by real conditions this same piece can be in, and borrowing one would put a
        /// wrong reason on screen - "no build zone" over a perfectly buildable meadow is
        /// worse than no reason at all. The reason is said by the prefix below, at the
        /// moment somebody actually asks for it.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Ghost(Player __instance)
        {
            var table = BuildPiecesRef(__instance);
            var piece = table != null ? table.GetSelectedPiece() : null;
            if (!IsSapling(piece)) return;

            bool have;
            var at = GhostAt(__instance, out have);
            if (!have || !InsideBase(at)) return;

            StatusRef(__instance) = Player.PlacementStatus.Invalid;
            if (SetGhostValid != null) SetGhostValid(__instance, false);
        }

        // ------------------------------------------------------------------ the refusal

        /// <summary>
        /// Says why, once per press.
        ///
        /// Vanilla would refuse this on its own now that the status is Invalid, and would
        /// say "$msg_invalidplacement" - which is true and useless. Somebody standing in
        /// their own front garden wondering why a seed will not go in the ground needs to be
        /// told it is the workbench, because that is also how they defend against one.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool Refuse(Player __instance, Piece piece, ref bool __result)
        {
            if (!IsSapling(piece)) return true;

            bool have;
            var at = GhostAt(__instance, out have);
            if (!have || !InsideBase(at)) return true;

            // Throttled: the place button repeats while held, and vanilla itself acts on a
            // press every 0.2s, so an unguarded message is a wall of the same line.
            if (Time.time - _lastSaid > 1f)
            {
                _lastSaid = Time.time;
                __instance.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize(GroveConfig.BaseRefusal.Value));
            }

            __result = false;
            return false;
        }
    }
}
