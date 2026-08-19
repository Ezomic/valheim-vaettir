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

        /// <summary>
        /// Which biomes will take a sapling at all, parsed once and re-parsed if the setting
        /// is edited in game.
        /// </summary>
        private static Heightmap.Biome _allowed;
        private static string _allowedRaw;

        private static Heightmap.Biome Allowed()
        {
            var raw = GroveConfig.SaplingBiomes.Value ?? "";
            if (_allowedRaw == raw) return _allowed;

            _allowedRaw = raw;
            _allowed = (Heightmap.Biome)0;

            foreach (var entry in raw.Split(','))
            {
                var name = entry.Trim();
                if (name.Length == 0) continue;

                try
                {
                    _allowed |= (Heightmap.Biome)System.Enum.Parse(
                        typeof(Heightmap.Biome), name, true);
                }
                catch (System.Exception)
                {
                    // Named singly rather than parsing the whole list in one call, so one
                    // typo costs its own biome instead of all of them.
                    GrovePlugin.LogOnce(name + " is not one of Valheim's biome names.");
                }
            }

            return _allowed;
        }

        /// <summary>
        /// Whether this spot is far enough inside a biome that will have it.
        ///
        /// Sampled on a ring as well as at the point, so the seed has to be a real margin
        /// inside its own wood rather than balanced on the boundary. Standing one step into
        /// the forest and planting a thing that summons the forest is exactly the case worth
        /// refusing: the fight would happen half in the meadow, and the border between two
        /// biomes is where a raid is least interesting.
        ///
        /// Eight points and the centre. Fewer misses a notch of meadow poking in, and the
        /// whole test runs once a frame while the cultivator is out, which is cheap enough
        /// at nine heightmap lookups but not at ninety.
        /// </summary>
        internal static bool OutsideBiome(Vector3 at)
        {
            var allowed = Allowed();
            if (allowed == 0) return false;

            if ((Heightmap.FindBiome(at) & allowed) == 0) return true;

            var margin = Mathf.Max(0f, GroveConfig.BiomeMargin.Value);
            if (margin <= 0f) return false;

            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * 2f / 8f;
                var edge = at + new Vector3(Mathf.Cos(angle) * margin, 0f,
                                            Mathf.Sin(angle) * margin);

                if ((Heightmap.FindBiome(edge) & allowed) == 0) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this spot refuses a sapling, and why. One place, so the ghost and the
        /// message can never disagree about the reason.
        /// </summary>
        internal static bool Refused(Vector3 at, out string why)
        {
            if (InsideBase(at))
            {
                why = GroveConfig.BaseRefusal.Value;
                return true;
            }

            if (OutsideBiome(at))
            {
                why = GroveConfig.BiomeRefusal.Value;
                return true;
            }

            why = null;
            return false;
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
            string why;
            if (!have || !Refused(at, out why)) return;

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
            string why;
            if (!have || !Refused(at, out why)) return true;

            // Throttled: the place button repeats while held, and vanilla itself acts on a
            // press every 0.2s, so an unguarded message is a wall of the same line.
            if (Time.time - _lastSaid > 1f)
            {
                _lastSaid = Time.time;
                __instance.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize(why));
            }

            __result = false;
            return false;
        }
    }
}
