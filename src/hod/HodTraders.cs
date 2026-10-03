using System;
using System.Collections.Generic;
using Ezomic.Shared;
using Grove;
using UnityEngine;

namespace Hod
{
    /// <summary>
    /// What a trader sells, filed under the biome the trader belongs to.
    ///
    /// The shared biome index places an item by where it grows, what drops it, and what is
    /// made from it. A trader's stock is none of those: Haldor's, the Bog Witch's and
    /// Hildir's wares are not found in the world, so the index either leaves them unplaced
    /// (and AllowUnclassified decides, which is a setting about six odd items and not about a
    /// shop) or files them wherever a recipe happens to put them. The Cured squirrel
    /// hamstring was the visible case, LHM-63, and the cause there was a pin rather than a
    /// derivation: see CHANGELOG.
    ///
    /// So a trader's stock is gated by the boss of the trader's own biome, read off the
    /// trader prefabs at load the way ItemGroups reads smelter conversions: nothing here
    /// lists an item. Only the three traders and their biomes are written down, as the
    /// TraderBiomes setting.
    ///
    /// This sits in Vaettir and not in the shared BiomeIndex on purpose. That file is linked
    /// into Yoke and Hirsla as well and is not this repo's to change in a bug fix; the gate
    /// folds the answer in on its way out instead (HodGate.BiomeOf). The cost is that a
    /// recipe made from a traded item is still placed by the index from its other
    /// ingredients, which is what it did before.
    /// </summary>
    internal static class HodTraders
    {
        private static Dictionary<string, string> _biomeOf = new Dictionary<string, string>();
        private static ZNetScene _scene;
        private static bool _built;
        private static string _builtFrom;
        private static string _pinnedFrom;
        private static HashSet<string> _pinned = new HashSet<string>();

        public static void Invalidate()
        {
            _built = false;
            _scene = null;
            _builtFrom = null;
            _biomeOf = new Dictionary<string, string>();
        }

        /// <summary>
        /// The biome of the earliest trader selling this prefab, or BiomeIndex.None.
        /// Earliest, because buying a thing from Haldor must never make it later than where
        /// it can be found, which is the rule every other item follows.
        /// </summary>
        public static string BiomeOf(string prefabName)
        {
            Ensure();

            string biome;
            return _biomeOf.TryGetValue(prefabName, out biome) ? biome : BiomeIndex.None;
        }

        public static int Count
        {
            get
            {
                Ensure();
                return _biomeOf.Count;
            }
        }

        /// <summary>
        /// "hod traders" for the console: how many items the traders of each biome sell, then
        /// the names, so the first run in a world shows what is there to override.
        /// </summary>
        public static string Describe()
        {
            Ensure();

            var counts = new Dictionary<string, int>();
            var names = new Dictionary<string, List<string>>();
            foreach (var pair in _biomeOf)
            {
                int n;
                counts.TryGetValue(pair.Value, out n);
                counts[pair.Value] = n + 1;

                List<string> list;
                if (!names.TryGetValue(pair.Value, out list)) names[pair.Value] = list = new List<string>();
                list.Add(pair.Key);
            }

            var sb = new System.Text.StringBuilder("hod traders scene=" + (_built ? "ready" : "missing"));
            foreach (var biome in BiomeIndex.All)
            {
                if (biome == BiomeIndex.None) continue;

                int n;
                counts.TryGetValue(biome, out n);
                sb.Append(' ').Append(biome).Append('=').Append(n);
            }

            foreach (var biome in BiomeIndex.All)
            {
                List<string> list;
                if (!names.TryGetValue(biome, out list)) continue;

                list.Sort(StringComparer.Ordinal);
                sb.Append("\n  ").Append(biome).Append(": ").Append(string.Join(", ", list.ToArray()));
            }

            return sb.ToString();
        }

        /// <summary>
        /// True when a BiomeOverrides entry names this prefab. An override is the correction
        /// path and beats everything, the trader rule included.
        /// </summary>
        public static bool Pinned(string prefabName)
        {
            var raw = HodConfig.BiomeOverrides == null ? "" : HodConfig.BiomeOverrides.Value ?? "";

            // Rebuilt only when the string changes: this is asked per item per frame by the
            // chest tally, and splitting the whole list every time was the cost.
            if (_pinnedFrom == null || _pinnedFrom != raw)
            {
                var set = new HashSet<string>();
                foreach (var entry in raw.Split(','))
                {
                    var parts = entry.Trim().Split(':');
                    if (parts.Length != 2) continue;

                    // The same test BiomeIndex.ApplyOverrides applies. An entry it skips
                    // (Wood:swampp) pins nothing there, so it must not stop the trader rule
                    // here either, or the item loses its trader biome to a typo.
                    var biome = parts[1].Trim().ToLowerInvariant();
                    if (biome == BiomeIndex.None || Array.IndexOf(BiomeIndex.All, biome) < 0) continue;

                    set.Add(parts[0].Trim());
                }

                _pinned = set;
                _pinnedFrom = raw;
            }

            return _pinned.Contains(prefabName);
        }

        private static void Ensure()
        {
            var scene = ZNetScene.instance;

            // No scene yet is not an answer to remember: the first pass of a session runs from
            // ObjectDB.Awake, before any ZNetScene exists, and caching an empty map there
            // would leave every trader item unplaced for the rest of the session.
            if (scene == null || scene.m_prefabs == null) return;
            var setting = HodConfig.TraderBiomes == null ? "" : HodConfig.TraderBiomes.Value ?? "";
            if (_built && _scene == scene && _builtFrom == setting) return;

            var found = new Dictionary<string, string>();
            var unmatched = new List<string>();
            var biomes = ParseSetting();

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var trader = prefab.GetComponentInChildren<Trader>(true);
                if (trader == null || trader.m_items == null) continue;

                string biome;
                if (!biomes.TryGetValue(prefab.name.ToLowerInvariant(), out biome))
                {
                    if (trader.m_items.Count > 0) unmatched.Add(prefab.name);
                    continue;
                }

                foreach (var sale in trader.m_items)
                {
                    if (sale == null || sale.m_prefab == null) continue;

                    var name = sale.m_prefab.gameObject.name;
                    string existing;
                    if (!found.TryGetValue(name, out existing) || Earlier(biome, existing))
                        found[name] = biome;
                }
            }

            _biomeOf = found;
            _scene = scene;
            _builtFrom = setting;
            _built = true;

            GrovePlugin.Log.LogInfo("Trader stock placed: " + found.Count + " item(s) filed by the "
                + "biome of the trader that sells them.");

            if (unmatched.Count > 0)
                GrovePlugin.Log.LogWarning("These trader prefabs sell items but are not named in "
                    + "TraderBiomes, so their stock is placed like any other item: "
                    + string.Join(", ", unmatched.ToArray()) + ".");
        }

        private static bool Earlier(string a, string b)
        {
            return Array.IndexOf(BiomeIndex.All, a) < Array.IndexOf(BiomeIndex.All, b);
        }

        private static Dictionary<string, string> ParseSetting()
        {
            var parsed = new Dictionary<string, string>();
            var raw = HodConfig.TraderBiomes == null ? "" : HodConfig.TraderBiomes.Value ?? "";

            foreach (var entry in raw.Split(','))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var parts = text.Split(':');
                var biome = parts.Length == 2 ? parts[1].Trim().ToLowerInvariant() : "";

                if (biome == BiomeIndex.None || Array.IndexOf(BiomeIndex.All, biome) < 0)
                {
                    GrovePlugin.LogOnce("Ignoring trader biome '" + text + "': expected "
                        + "prefab:biome with a real biome.");
                    continue;
                }

                parsed[parts[0].Trim().ToLowerInvariant()] = biome;
            }

            return parsed;
        }
    }
}
