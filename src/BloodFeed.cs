using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Greydwarf deaths feed the nearest sapling.
    ///
    /// The hook is `Character.OnDeath`, which is `protected virtual` and - checked
    /// against the decompiled source rather than assumed - is *not* overridden by
    /// `Humanoid`, which overrides only `OnDamaged`. Greydwarfs are Humanoids, so
    /// their deaths dispatch to the base body and a postfix there sees them. If a
    /// future update adds a `Humanoid.OnDeath`, this silently stops firing, and the
    /// symptom would be a sapling that never grows.
    /// </summary>
    internal static class BloodFeed
    {
        private static Dictionary<string, float> _weights;
        private static string _weightsRaw;

        /// <summary>
        /// The nearest sapling gets it, not every sapling in range.
        ///
        /// Feeding all of them would mean two saplings planted side by side grow twice
        /// as fast as one for the same work, and the obvious next move would be to
        /// plant a dozen in a heap. One kill, one seed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "OnDeath")]
        private static void Feed(Character __instance)
        {
            if (__instance == null || Sapling.All.Count == 0) return;

            var weight = WeightOf(Utils.GetPrefabName(__instance.gameObject));
            if (weight <= 0f) return;

            var where = __instance.transform.position;
            var range = GroveConfig.FeedRange.Value;

            Sapling best = null;
            var bestDistance = float.MaxValue;

            foreach (var sapling in Sapling.All)
            {
                if (sapling == null) continue;

                var distance = Vector3.Distance(sapling.transform.position, where);
                if (distance > range || distance >= bestDistance) continue;

                best = sapling;
                bestDistance = distance;
            }

            if (best == null) return;

            best.Feed(weight);

            if (GroveConfig.Messages.Value && best.Progress < 1f) Count(best);
        }

        /// <summary>Reused so a kill does not allocate a list every time.</summary>
        private static readonly List<Player> Nearby = new List<Player>();

        /// <summary>
        /// Everyone defending it sees the count, not just whoever landed the last blow.
        ///
        /// This used to message Player.m_localPlayer, which is wrong in every co-op game
        /// and looks like the mod being broken. Character.OnDeath runs on the client that
        /// *owns* the creature and nowhere else, so with two players clearing greydwarfs
        /// around one sapling the counter appeared for whichever of them happened to own
        /// each corpse - so both of them saw roughly half the kills register and neither
        /// could tell whether the other's kills were counting at all. They were; only the
        /// message was missing.
        ///
        /// Player.Message handles the networking itself: on a Player this client does not
        /// own it invokes an RPC rather than drawing anything locally, so calling it on
        /// each player in range puts the line on each of their screens and needs no RPC of
        /// ours.
        ///
        /// Range is the sapling's own FeedRange, from the sapling rather than from the
        /// corpse. What the message is about is the sapling, so the people who should see
        /// it are the ones standing near it - not the ones near the thing that died, who
        /// may be a hedge away chasing something else.
        /// </summary>
        private static void Count(Sapling sapling)
        {
            var line = Localization.instance.Localize(string.Format(
                "{0}  ( {1} / {2} )", sapling.GetHoverName(),
                Mathf.FloorToInt(sapling.Blood), Mathf.FloorToInt(sapling.Needed)));

            Nearby.Clear();
            Player.GetPlayersInRange(sapling.transform.position,
                                     GroveConfig.FeedRange.Value, Nearby);

            foreach (var player in Nearby)
            {
                if (player == null) continue;
                player.Message(MessageHud.MessageType.TopLeft, line);
            }

            Nearby.Clear();
        }

        /// <summary>
        /// What one death is worth. Unlisted creatures are worth nothing.
        ///
        /// A list rather than a faction check: ForestMonsters would also catch trolls,
        /// boars and the Elder himself, and "kill anything in the forest" is a
        /// different and much duller quest than "clear out the greydwarfs".
        /// </summary>
        private static float WeightOf(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return 0f;

            var raw = GroveConfig.FeedWeights.Value ?? "";
            if (_weights == null || raw != _weightsRaw)
            {
                _weightsRaw = raw;
                _weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in raw.Split(','))
                {
                    var parts = entry.Split(':');
                    if (parts.Length != 2) continue;

                    var name = parts[0].Trim();
                    float weight;
                    if (name.Length == 0
                        || !float.TryParse(parts[1].Trim(),
                                           System.Globalization.NumberStyles.Float,
                                           System.Globalization.CultureInfo.InvariantCulture,
                                           out weight))
                        continue;

                    _weights[name] = weight;
                }
            }

            float found;
            return _weights.TryGetValue(prefab, out found) ? found : 0f;
        }
    }
}
