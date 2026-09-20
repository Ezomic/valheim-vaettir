using System.Collections.Generic;
using Ezomic.Shared;
using Grove;

namespace Hod
{
    /// <summary>
    /// Which biomes the world has opened, and therefore which materials a chest may hand
    /// over.
    ///
    /// This is the bench service's whole argument, and it is the reason the jib is worth
    /// building rather than being a thing that makes the game shorter. Reaching into every
    /// chest in sight from the first minute deletes the early game's one real lesson - that a
    /// trip has to be planned - and hands it over as a convenience before the player has done
    /// anything to want it. Reaching into them once you have killed the thing that owns the
    /// biome the material came from is the same convenience arriving as a reward. Before
    /// Eikthyr the jib does nothing at all. By Fader it does everything, and that is
    /// deliberate rather than a fade-out: finishing the game is what buys it.
    ///
    /// Three separate facts have to line up for one item, and they come from three places:
    ///
    ///   the item's biome     BiomeIndex, read off the game's own vegetation, spawn,
    ///                        conversion and recipe tables - never a list typed here
    ///   the biome's boss     BossBiomes, a line in the .cfg, because nothing in the game
    ///                        says defeated_dragon is a Mountain thing
    ///   the boss's death     a global key, which is world state the server pushes out
    ///
    /// The last of those is the one with the trap in it, and it cost a live bug on 2026-08-25
    /// in a sibling mod. See <see cref="Settling"/>.
    /// </summary>
    internal static class HodGate
    {
        private struct Tier
        {
            public string BossKey;
            public string Biome;
        }

        private static List<Tier> _tiers;
        private static string _parsedFrom;

        /// <summary>
        /// The biomes open as of the last question, which may be one world state behind.
        ///
        /// Kept rather than cleared when it goes stale, because holding yesterday's answer
        /// for a frame is strictly better than answering from a key list that is mid-rebuild
        /// - see <see cref="Settling"/>.
        /// </summary>
        private static HashSet<string> _open;

        private static bool _stale = true;

        /// <summary>
        /// True while <c>ZoneSystem.RPC_GlobalKeys</c> is replacing the world's key list.
        ///
        /// That method calls ClearGlobalKeys and then re-adds every key ONE AT A TIME, and it
        /// runs on every client every time anybody sets any key, because SetGlobalKey ends in
        /// SendGlobalKeys(Everybody). For the length of that loop the world genuinely has
        /// fewer keys than it has - at the start of it, none - and anything asking "is that
        /// boss dead" gets false.
        ///
        /// Vanilla never notices, because the refill is synchronous and no frame boundary
        /// falls inside it. A Harmony postfix on GlobalKeyAdd does notice, and this feature
        /// has one, so the flag is what stops a chest wall going dark for the duration of
        /// somebody else's unrelated key.
        ///
        /// Both halves of the fix are here on purpose. The flag holds the previous answer
        /// steady through the refill; the dirty flag drained in HodRuntime.Tick recomputes it
        /// once the list is certainly complete. Yoke shipped only the second half and it was
        /// not enough: mid-refill it answered "not earned", wrote stack sizes off the back of
        /// it, and no further key ever arrived to trigger a correction.
        /// </summary>
        public static bool Settling;

        /// <summary>Set by the GlobalKeyAdd postfix, drained once a frame by HodRuntime.</summary>
        public static bool Dirty;

        /// <summary>Forget the cached answer. Cheap; the recompute is deferred until asked.</summary>
        public static void MarkStale()
        {
            _stale = true;
        }

        /// <summary>
        /// Whether this item may come out of a chest.
        ///
        /// Takes the prefab name rather than the item's shared name, because the shared name
        /// is a localisation token - "$item_wood" - and BiomeIndex is keyed on prefabs. The
        /// two are not interchangeable and mixing them up would classify nothing at all,
        /// silently, which reads as the gate being shut.
        /// </summary>
        public static bool Allows(string prefabName)
        {
            if (!HodConfig.Enabled.Value) return false;

            // A half-built index answers "no biome" for every item in the game, and that
            // would then be decided by AllowUnclassified - a setting about the handful of
            // items no table can reach, being asked a question it was never written for. So
            // the incomplete case is answered explicitly and openly instead. It only lasts
            // from the first stub ObjectDB to the world's SpawnSystem arriving, which is
            // before a player exists to craft anything.
            if (!BiomeIndex.Complete) return true;

            var biome = BiomeIndex.BiomeOf(prefabName);

            // Fail open, on purpose and with a setting behind it. A false block reads as a
            // broken mod - the material is visibly in the chest and the bench refuses it -
            // while a false allow reads as an ordinary craft-from-container mod, which is
            // what somebody who built the jib already expects.
            if (biome == BiomeIndex.None) return HodConfig.AllowUnclassified.Value;

            // A biome no row in BossBiomes names can never be unlocked by anything, so gating
            // on it would shut it permanently - and permanently shut is indistinguishable in
            // game from "nobody has killed that boss yet". Deep North is the vanilla case: no
            // boss, no items of its own. The same failure with a typo in the config would
            // otherwise be silent.
            //
            // That reasoning rests on a premise - "no items of its own" - which was true when
            // it was written and is exactly what a content update falsifies. The moment Deep
            // North ships materials, every one of them classifies into an untiered biome and
            // walks straight out of a chest with no boss killed, in the feature whose only
            // promise is the opposite. Nothing about that is visible: the item is simply
            // craftable.
            //
            // So the premise checks itself now. Reaching this line means an item HAS been
            // classified into a biome no row names, which is the evidence the premise has
            // broken, and it is said once per biome. Still open, because a silent shut door
            // is worse than a named open one and because the config, not the code, is where
            // the answer belongs - but the operator is told which row to add.
            if (!Tiered(biome))
            {
                Warn(biome, prefabName);
                return true;
            }

            return Open().Contains(biome);
        }

        /// <summary>
        /// The same question asked of a stack sitting in a chest.
        ///
        /// m_dropPrefab is the only route from an item in the world back to the prefab name
        /// the index is keyed on. It can be null on an item that was never a pickup, and a
        /// null there is not a classification failure but an unanswerable question, so it
        /// takes the same fail-open path as an unplaced item.
        /// </summary>
        public static bool AllowsStack(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;

            // Plain null check, never ?. - Unity overloads == so a destroyed Object compares
            // equal to null while the null-propagating operators sail straight past it.
            if (item.m_dropPrefab == null)
            {
                if (!HodConfig.Enabled.Value) return false;
                return HodConfig.AllowUnclassified.Value;
            }

            return Allows(item.m_dropPrefab.name);
        }

        /// <summary>
        /// The biome a boss key belongs to, or null.
        ///
        /// Handed to BiomeIndex as its BiomeForKey seam, which is what lets it place a boss's
        /// own drops - the boss prefab knows the key its death sets and this table knows the
        /// biome that key owns, so Moder's trophy lands in the Mountains without either side
        /// naming an item. It also clamps key-gated spawn rows, which is the fix for a whole
        /// tier of Ashlands drops that used to read as Meadows.
        /// </summary>
        public static string BiomeFor(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return null;

            var key = bossKey.ToLowerInvariant();
            foreach (var tier in Tiers())
                if (tier.BossKey == key) return tier.Biome;

            return null;
        }

        /// <summary>What the chests are currently serving, for one line in the log.</summary>
        public static string Describe()
        {
            var open = Open();
            if (open.Count == 0) return "nothing - no boss in this world is down yet";

            // In progression order rather than hash order, because a list that reads meadows,
            // blackforest, swamp is a sentence about how far the world has got and a list
            // that reads swamp, meadows, ocean is a set dump.
            var sb = new System.Text.StringBuilder();
            foreach (var biome in BiomeIndex.All)
            {
                if (!open.Contains(biome)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(biome);
            }

            return sb.ToString();
        }

        // -------------------------------------------------------------------- internals

        private static bool Tiered(string biome)
        {
            foreach (var tier in Tiers())
                if (tier.Biome == biome) return true;

            return false;
        }

        /// <summary>
        /// Biomes already reported as ungated, so this is one line rather than one per item
        /// per frame. Allows runs from the crafting tally, which sweeps every chest in range.
        /// </summary>
        private static readonly HashSet<string> Ungated = new HashSet<string>();

        /// <summary>
        /// Say that a biome is carrying items nothing can gate.
        ///
        /// A warning rather than info: this is the feature's promise not being kept, and the
        /// person who needs to see it is the one who would otherwise find out when a player
        /// crafts black metal out of a chest before killing anything. It names the item
        /// because a biome name alone does not tell you whether the row is missing or the
        /// classification is wrong, and those have different fixes.
        /// </summary>
        private static void Warn(string biome, string prefabName)
        {
            if (!Ungated.Add(biome)) return;

            GrovePlugin.LogOnce("Items are classified into the biome '" + biome
                + "' and no BossBiomes row names it, so the hod jib cannot gate them and is "
                + "letting them through - '" + prefabName + "' is the first. Add a '" + biome
                + "' row to BossBiomes naming that biome's boss, or the whole biome's "
                + "materials are craftable from a chest with nothing killed.");
        }

        /// <summary>
        /// Forget the reported set, so a config edit that adds the missing row is not drowned
        /// out by a warning that has already been said. Called wherever the tiers are reread.
        /// </summary>
        public static void ForgetWarnings()
        {
            Ungated.Clear();
        }

        private static HashSet<string> Open()
        {
            if (!_stale && _open != null) return _open;

            // Mid-refill, hold the previous answer. See the Settling docstring.
            if (Settling && _open != null) return _open;

            var computed = Compute();
            _open = computed;

            // Only trusted once the refill has finished. Computing during one is a stopgap
            // for the very first join of a session, where there is no previous answer to hold
            // on to - and leaving it stale means the drain in HodRuntime corrects it a frame
            // later rather than the gate being wrong until the next boss dies.
            if (!Settling) _stale = false;

            return computed;
        }

        private static HashSet<string> Compute()
        {
            var open = new HashSet<string>();

            var zone = ZoneSystem.instance;
            if (zone == null) return open;

            foreach (var tier in Tiers())
            {
                // The STRING overload, always. GetGlobalKey(GlobalKeys) reads
                // m_globalKeysEnums, and that enum runs out at defeated_goblinking in this
                // build - there is no defeated_queen member and no defeated_fader member,
                // because those two bosses carry their key in prefab data instead. Asking the
                // enum therefore answers "not yet" for the Mistlands and the Ashlands for the
                // rest of time, and from inside the game that is indistinguishable from
                // nobody having killed them. The string form reads m_globalKeysValues and
                // finds any key at all, including a modded one.
                if (zone.GetGlobalKey(tier.BossKey)) open.Add(tier.Biome);
            }

            return open;
        }

        private static List<Tier> Tiers()
        {
            var raw = HodConfig.BossBiomes.Value ?? "";
            if (_tiers != null && raw == _parsedFrom) return _tiers;

            _parsedFrom = raw;
            _tiers = Parse(raw);

            // A changed table is a changed answer, and the config file is edited while the
            // game is running.
            MarkStale();

            // The table just changed, so a biome previously reported as ungated may not be
            // any more - and if it still is, the operator has earned the reminder after
            // editing.
            ForgetWarnings();

            return _tiers;
        }

        /// <summary>
        /// "defeated_eikthyr:meadows, defeated_bonemass:swamp".
        ///
        /// A malformed entry is logged and dropped rather than throwing. This is a string a
        /// person types into a .cfg, and the failure that matters is a typo in one of eight
        /// entries taking the other seven down with it.
        /// </summary>
        private static List<Tier> Parse(string raw)
        {
            var tiers = new List<Tier>();

            foreach (var entry in raw.Split(','))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var parts = text.Split(':');
                if (parts.Length != 2)
                {
                    GrovePlugin.LogOnce("Ignoring hod gate row '" + text
                        + "': expected boss:biome.");
                    continue;
                }

                var biome = parts[1].Trim().ToLowerInvariant();
                if (System.Array.IndexOf(BiomeIndex.All, biome) < 0)
                {
                    GrovePlugin.LogOnce("Ignoring hod gate row '" + text + "': '" + biome
                        + "' is not one of " + string.Join(", ", BiomeIndex.All) + ".");
                    continue;
                }

                tiers.Add(new Tier
                {
                    // Lowercased because that is how ZoneSystem stores and looks keys up -
                    // GlobalKeyAdd calls keyStr.ToLower() on the way in and GetGlobalKey calls
                    // name.ToLower() on the way out.
                    BossKey = parts[0].Trim().ToLowerInvariant(),
                    Biome = biome
                });
            }

            return tiers;
        }

        /// <summary>
        /// The biomes still shut, each named beside the key it is waiting on.
        ///
        /// This exists to make one specific typo visible, and it is a typo worth expecting:
        /// the two boss keys nobody guesses right are defeated_dragon and defeated_goblinking,
        /// whose bosses are called Moder and Yagluth. Parse validates the BIOME half of every
        /// row against BiomeIndex.All and drops a bad one, and a dropped row leaves that biome
        /// open - loud in the right direction. The KEY half cannot be validated against
        /// anything, because any global key is legitimate here including a modded one. So a
        /// row reading defeated_moder:mountain parses cleanly, tiers the Mountains on a key
        /// that will never exist, and shuts silver and wolf pelts out for the life of the
        /// world.
        ///
        /// Nothing distinguishes that from "Moder is still alive" except being able to read
        /// the key the gate is waiting on. Hence this line.
        /// </summary>
        private static string DescribeWaiting()
        {
            var open = Open();
            var sb = new System.Text.StringBuilder();

            foreach (var biome in BiomeIndex.All)
            {
                if (open.Contains(biome)) continue;

                foreach (var tier in Tiers())
                {
                    if (tier.Biome != biome) continue;

                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(biome).Append(" (").Append(tier.BossKey).Append(")");
                    break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Says out loud what the chests serve, but only when it has actually changed.
        ///
        /// Called from HodRuntime's Update drain, which fires once for a whole connect's
        /// worth of keys rather than once per key. Without the comparison, joining a finished
        /// world would print the same line eight times.
        /// </summary>
        public static void ReportIfChanged()
        {
            var waiting = DescribeWaiting();

            var now = "A hod jib's chests now serve: " + Describe() + "."
                      + (waiting.Length == 0 ? "" : " Waiting on: " + waiting + ".");

            if (now == _reported) return;

            _reported = now;
            if (GrovePlugin.Log != null) GrovePlugin.Log.LogInfo(now);
        }

        private static string _reported;

        /// <summary>Forget everything derived from the world. Called when a world is left.</summary>
        public static void Reset()
        {
            _open = null;
            _stale = true;
            _reported = null;
        }
    }
}
