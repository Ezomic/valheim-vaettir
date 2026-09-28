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
    /// Eikthyr the jib does nothing at all. By the Frozen King it does everything, and that
    /// is deliberate rather than a fade-out: finishing the game is what buys it.
    ///
    /// Three separate facts have to line up for one item, and they come from three places:
    ///
    ///   the item's biome     BiomeIndex, read off the game's own vegetation, spawn,
    ///                        conversion and recipe tables - never a list typed here
    ///   the biome's boss     BossBiomes, a line in the .cfg, because nothing in the game
    ///                        says defeated_dragon is a Mountain thing. One row, the Deep
    ///                        North's, names the boss prefab instead of its key, and the
    ///                        key is read off that prefab in each world. See
    ///                        <see cref="ReadPrefabRows"/> for why
    ///   the boss's death     a global key, which is world state the server pushes out
    ///
    /// The last of those is the one with the trap in it, and it cost a live bug on 2026-08-25
    /// in a sibling mod. See <see cref="Settling"/>.
    /// </summary>
    internal static class HodGate
    {
        /// <summary>
        /// A class rather than the struct this used to be, because a prefab row's key is filled
        /// in after parsing, once per world, and a struct in a List is a copy.
        /// </summary>
        private sealed class Tier
        {
            /// <summary>
            /// Lowercased, the way ZoneSystem keeps keys. Null on a prefab row until it has been
            /// read off the prefab in the world being played, and a null key is never set, so
            /// the biome it guards stays shut.
            /// </summary>
            public string BossKey;

            public string Biome;

            /// <summary>
            /// The boss prefabs a row marked with <see cref="PrefabMark"/> names, in the order
            /// they are tried. Null on an ordinary row, whose key is typed.
            /// </summary>
            public string[] Prefabs;

            /// <summary>The row as it was typed, so the log can quote it.</summary>
            public string Text;
        }

        /// <summary>
        /// What the `hod item` readout prints as the boss of a biome whose row reads its key off
        /// a prefab and has not found one in this world.
        /// </summary>
        public const string Unresolved = "unresolved";

        /// <summary>
        /// Starts a BossBiomes row whose left side names boss prefabs rather than a key. A
        /// character no vanilla key starts with, so a row cannot be one thing and read as the
        /// other.
        /// </summary>
        private const char PrefabMark = '@';

        private static List<Tier> _tiers;
        private static string _parsedFrom;

        /// <summary>
        /// The ZNetScene the prefab rows' keys were read from, compared by reference and never
        /// through Unity's ==, because the question is "is this the same world" and a scene
        /// that has since been destroyed is still not the new one.
        /// </summary>
        private static ZNetScene _readFrom;

        /// <summary>
        /// The resolution lines already written for the world in <see cref="_saidIn"/>, so each
        /// is said once per world. Kept apart from _readFrom because a config edit rereads the
        /// rows in the same world, and the same answer twice is noise.
        /// </summary>
        private static readonly HashSet<string> Said = new HashSet<string>();

        private static ZNetScene _saidIn;

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
            // game from "nobody has killed that boss yet". A row deleted from the config, or a
            // typo in its biome half, would otherwise be exactly that, silently.
            //
            // The Deep North used to be the vanilla case of this, with the argument that it
            // had no boss and no items of its own. Valheim 1.0 falsified both halves, and
            // core ce9aaac then filed the Elaking and Jotun drops and the Vanguard family
            // there. For as long as no row named it, every one of them walked out of a chest
            // with no boss killed. It has a row now, reading its key off the Frozen
            // King, and that row fails SHUT while it cannot find one (see ReadPrefabRows).
            // The two rules do not contradict each other: a row is a claim that the biome is
            // gated, and keeping that claim is the whole feature, while no row is no claim.
            //
            // Reaching this line still means an item HAS been classified into a biome no row
            // names, which is the evidence the config is missing a row the default carries, so
            // it is said once per biome. Still open, because a silent shut door is worse than a
            // named open one and because the config, not the code, is where the answer
            // belongs - but the operator is told which row to add.
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
        ///
        /// A prefab row answers here only once it has resolved. Tiers() reads it the first
        /// time it is asked in a world with a ZNetScene, and the index's boss pass walks that
        /// same ZNetScene, so by the time it asks about the Frozen King's key the row already
        /// knows it and the King's own drops land in the Deep North.
        /// </summary>
        public static string BiomeFor(string bossKey)
        {
            if (string.IsNullOrEmpty(bossKey)) return null;

            var key = bossKey.ToLowerInvariant();
            foreach (var tier in Tiers())
                if (tier.BossKey == key) return tier.Biome;

            return null;
        }

        /// <summary>
        /// The key of the first BossBiomes row naming this biome, <see cref="Unresolved"/> when
        /// that row reads its key off a prefab and has not found one in this world, or null
        /// when no row names the biome.
        ///
        /// For the `hod item` readout (see HodConsole). Null is the answer that explains an item
        /// Allows lets through with no boss down: a biome no row names is left open, see Allows.
        /// Unresolved explains the opposite, an item held back with no key it could be waiting
        /// on.
        /// </summary>
        public static string BossOf(string biome)
        {
            foreach (var tier in Tiers())
                if (tier.Biome == biome) return tier.BossKey ?? Unresolved;

            return null;
        }

        /// <summary>
        /// The resolved key of every row naming this biome, for `hodkey`, which sets or removes
        /// them. named says whether any row names the biome at all, so "no row" and "a row that
        /// has not resolved" can be refused in different words.
        /// </summary>
        public static List<string> KeysOf(string biome, out bool named)
        {
            named = false;
            var keys = new List<string>();

            foreach (var tier in Tiers())
            {
                if (tier.Biome != biome) continue;

                named = true;
                if (tier.BossKey != null && !keys.Contains(tier.BossKey)) keys.Add(tier.BossKey);
            }

            return keys;
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
                // A prefab row with no key yet opens nothing. This is the fail-closed half of
                // ReadPrefabRows, and it is the whole of it: the row still names the biome, so
                // Allows treats it as gated, and a gate with no key is never met.
                if (tier.BossKey == null) continue;

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
            if (_tiers == null || raw != _parsedFrom)
            {
                _parsedFrom = raw;
                _tiers = Parse(raw);

                // Fresh rows have read nothing yet, whatever the old ones had.
                _readFrom = null;

                // A changed table is a changed answer, and the config file is edited while
                // the game is running.
                MarkStale();

                // The table just changed, so a biome previously reported as ungated may not
                // be any more - and if it still is, the operator has earned the reminder
                // after editing.
                ForgetWarnings();
            }

            ReadPrefabRows();
            return _tiers;
        }

        /// <summary>
        /// Fills in the key of every row that names a boss prefab, from the world being played.
        ///
        /// <b>Why a prefab and not a key.</b> The Deep North's boss is the Frozen King, and the
        /// key its death sets is asset data: Character.m_defeatSetGlobalKey on the boss prefab.
        /// It is not in any assembly, so there is no string anybody has read to type into the
        /// .cfg, and a guessed one that is wrong shuts the Deep North for the life of the world
        /// in a way nothing in game can tell apart from the King still being alive. Reading it
        /// off the prefab is what BiomeIndex's boss pass already does to place a boss's drops,
        /// and it keeps working if a patch renames the key.
        ///
        /// <b>Per world, never held.</b> The key a vanilla prefab carries is the same in every
        /// world, but whether the prefab is there to read is a fact about one world's
        /// ZNetScene, which is torn down and rebuilt on every world load including a logout to
        /// the menu. So the answer belongs to the scene it was read from: a different scene is
        /// read again, and no scene at all (the menu) drops every key back to unresolved. The
        /// ObjectDB stub at the start of a session does not come into it, because this reads
        /// ZNetScene, and ZNetScene.Awake publishes the instance and fills its whole prefab
        /// table in one synchronous call, so nothing ever sees it half built.
        ///
        /// <b>Fail closed.</b> Until a key is found the row's biome stays SHUT. That is the
        /// opposite of the rule for a biome with no row at all, and Robbin chose it on
        /// 2026-09-28: a row is a claim that the biome is gated, a Deep North material left in
        /// its chest is a trip the player makes by hand, and one walking out with no boss killed
        /// is the one thing this feature promises will not happen. The shut is not silent
        /// either, which was the objection to shutting an untiered biome: the warning below
        /// names every prefab it tried and what it found.
        ///
        /// Once a prefab has been tried in a scene it is not tried again in that scene. Its
        /// table is complete from Awake, so a second look could only find the same thing. A
        /// prefab a mod registers later than that is the one case this misses, and it is
        /// missed shut, with the warning, until the next world load.
        /// </summary>
        private static void ReadPrefabRows()
        {
            // Normalised through Unity's == so a destroyed scene counts as no scene, and then
            // compared by reference, because the question is identity rather than liveness.
            var live = ZNetScene.instance;
            var scene = live == null ? null : live;

            if (ReferenceEquals(scene, _readFrom)) return;
            _readFrom = scene;

            var changed = false;
            foreach (var tier in _tiers)
            {
                if (tier.Prefabs == null) continue;

                tier.BossKey = scene == null ? null : Read(scene, tier);
                changed = true;
            }

            if (!changed) return;

            // A key arriving or going is a changed answer exactly as a boss dying is, and the
            // drain in HodRuntime says what the chests serve now.
            MarkStale();
            Dirty = true;
        }

        /// <summary>
        /// The key the first named prefab carries, lowercased, or null. Says what it found once
        /// per world, at Info when it resolves and as a warning when it cannot.
        ///
        /// Every name is looked at, not only up to the first hit, because the log line is how
        /// the key gets learned: the Frozen King is three prefabs, one per phase, and which of
        /// them carries the key is as unread as the key itself.
        /// </summary>
        private static string Read(ZNetScene scene, Tier tier)
        {
            string key = null;
            string from = null;
            var seen = new List<string>();

            foreach (var name in tier.Prefabs)
            {
                string found;
                var carried = DefeatKey(scene, name, out found);
                seen.Add(name + " (" + found + ")");

                if (key != null || carried == null) continue;

                key = carried;
                from = name;
            }

            var place = Pretty(tier.Biome);
            if (key != null)
            {
                Say(scene, place + " materials open at " + key + ", read off " + from
                    + ". The hod jib's BossBiomes row '" + tier.Text + "' looked at "
                    + string.Join(", ", seen.ToArray()) + ".", false);
            }
            else
            {
                Say(scene, place + " materials stay shut: the hod jib's BossBiomes row '"
                    + tier.Text + "' reads its key off a boss prefab and found none. It tried "
                    + string.Join(", ", seen.ToArray()) + ". Until one of them is in this "
                    + "world with a defeat key, no " + place + " material comes out of a "
                    + "chest, whoever has been killed.", true);
            }

            return key;
        }

        /// <summary>
        /// The defeat key one prefab carries, or null, with what was found either way in words
        /// for the log.
        ///
        /// GetComponent of Character finds a Humanoid as well, which derives from it. The key
        /// goes through ZoneSystem.GetKeyValue, the same call GlobalKeyAdd makes on the way in,
        /// so it is lowercased and cut at its first space exactly as the world will store it.
        /// </summary>
        private static string DefeatKey(ZNetScene scene, string name, out string found)
        {
            var prefab = scene.GetPrefab(name);
            if (prefab == null)
            {
                found = "not in this world";
                return null;
            }

            var character = prefab.GetComponent<Character>();
            if (character == null)
            {
                found = "no Character";
                return null;
            }

            if (string.IsNullOrEmpty(character.m_defeatSetGlobalKey))
            {
                found = "no defeat key";
                return null;
            }

            found = ZoneSystem.GetKeyValue(character.m_defeatSetGlobalKey.ToLower(), out _, out _);
            return found;
        }

        private static void Say(ZNetScene scene, string line, bool warn)
        {
            if (!ReferenceEquals(scene, _saidIn))
            {
                Said.Clear();
                _saidIn = scene;
            }

            if (!Said.Add(line) || GrovePlugin.Log == null) return;

            if (warn) GrovePlugin.Log.LogWarning(line);
            else GrovePlugin.Log.LogInfo(line);
        }

        /// <summary>A biome as a sentence would name it, for the two log lines above.</summary>
        private static string Pretty(string biome)
        {
            switch (biome)
            {
                case "blackforest": return "Black Forest";
                case "deepnorth": return "Deep North";
                default:
                    return biome.Length == 0 ? biome : char.ToUpperInvariant(biome[0]) + biome.Substring(1);
            }
        }

        /// <summary>
        /// "defeated_eikthyr:meadows, defeated_bonemass:swamp, @FrozenKing:deepnorth".
        ///
        /// A left side starting with <see cref="PrefabMark"/> names boss prefabs rather than a
        /// key, several separated by |, tried in order; see ReadPrefabRows. Prefab names are
        /// kept as typed, because ZNetScene hashes the exact name and FrozenKing is not
        /// frozenking.
        ///
        /// A malformed entry is logged and dropped rather than throwing. This is a string a
        /// person types into a .cfg, and the failure that matters is a typo in one of nine
        /// entries taking the other eight down with it.
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

                var left = parts[0].Trim();
                if (left.Length > 0 && left[0] == PrefabMark)
                {
                    var names = new List<string>();
                    foreach (var name in left.Substring(1).Split('|'))
                        if (name.Trim().Length > 0) names.Add(name.Trim());

                    if (names.Count == 0)
                    {
                        GrovePlugin.LogOnce("Ignoring hod gate row '" + text + "': '" + PrefabMark
                            + "' has to be followed by a boss prefab name.");
                        continue;
                    }

                    tiers.Add(new Tier { Prefabs = names.ToArray(), Biome = biome, Text = text });
                    continue;
                }

                tiers.Add(new Tier
                {
                    // Lowercased because that is how ZoneSystem stores and looks keys up -
                    // GlobalKeyAdd calls keyStr.ToLower() on the way in and GetGlobalKey calls
                    // name.ToLower() on the way out.
                    BossKey = left.ToLowerInvariant(),
                    Biome = biome,
                    Text = text
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
                    sb.Append(biome).Append(" (").Append(tier.BossKey ?? Unresolved).Append(")");
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

            // The prefab rows' keys too, and the keys themselves and not only the scene they
            // came from. Clearing just _readFrom would leave a key from the world just left in
            // place for as long as there is no scene to compare against, since no scene and a
            // forgotten scene are both null. ReadPrefabRows was already going to notice a new
            // scene by itself; this makes "a world was left" mean the same thing here as it
            // does to everything else in Forget.
            if (_tiers != null)
                foreach (var tier in _tiers)
                    if (tier.Prefabs != null) tier.BossKey = null;

            _readFrom = null;
        }
    }
}
