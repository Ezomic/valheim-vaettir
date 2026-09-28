using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Greydwarf deaths feed the nearest sapling.
    ///
    /// The hook is `Character.OnDeath`, which is `virtual` and - checked
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
        /// Kills this machine has fed to a sapling this session, for `vaettir sapling`.
        /// </summary>
        internal static int FedHere;

        /// <summary>
        /// Whether this machine owns the dying creature, read before vanilla's death runs,
        /// because by the time Feed runs it can no longer be read at all.
        ///
        /// On the owner, Character.OnDeath ends in ZNetScene.Destroy, and that calls
        /// ZNetView.ResetZDO, which nulls the view's ZDO before any postfix gets a turn. So in
        /// a postfix the owner's view is invalid, IsOwner answers false (it asks IsValid
        /// first) and GetZDO is null. On every other machine OnDeath returns early at its own
        /// IsOwner check, before Destroy, so there the view is still valid and IsOwner is
        /// false, as it should be. A postfix guarded on "valid and owner" therefore turned
        /// away every machine, the owner included: from the guard's arrival on 2026-09-27
        /// until this prefix on 2026-09-28 no kill fed a sapling anywhere, in singleplayer
        /// or online. Nothing complained, because a sapling that is never fed looks exactly
        /// like one nobody has fought near.
        ///
        /// Nothing else is read off the ZDO. Feed needs the prefab name and where the body
        /// fell, and both come off the GameObject, which Object.Destroy leaves standing until
        /// the end of the frame.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "OnDeath")]
        private static void ReadOwner(Character __instance, out bool __state)
        {
            ZNetView nview;
            __state = __instance != null && __instance.TryGetComponent(out nview) && nview.IsOwner();
        }

        /// <summary>
        /// The nearest sapling gets it, not every sapling in range.
        ///
        /// Feeding all of them would mean two saplings planted side by side grow twice
        /// as fast as one for the same work, and the obvious next move would be to
        /// plant a dozen in a heap. One kill, one seed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "OnDeath")]
        private static void Feed(Character __instance, bool __state)
        {
            // The owner only, and ReadOwner is what knows which machine that is. In 1.0 a
            // creature with a death animation reaches OnDeath through CharacterAnimEvent.Die on
            // every client animating it, not just through CheckDeath on the owner, so with no
            // guard one kill fed the sapling once per player watching. Asking the view here
            // instead cannot work: see ReadOwner for why it is already empty on the owner.
            if (!__state || __instance == null || Sapling.All.Count == 0) return;

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

            FedHere++;
            best.Feed(weight);

            if (GroveConfig.Messages.Value && best.Progress < 1f) Count(best);
        }

        /// <summary>Reused so a kill does not allocate a list every time.</summary>
        private static readonly List<Player> Nearby = new List<Player>();

        /// <summary>
        /// Everyone defending it sees the count, not just whoever landed the last blow.
        ///
        /// This used to message Player.m_localPlayer, which is wrong in every co-op game
        /// and looks like the mod being broken. Feed only acts on the client that *owns* the
        /// creature (ReadOwner checks, since a death animation calls OnDeath everywhere), so
        /// with two players clearing greydwarfs around one sapling the counter appeared for whichever of them happened to own
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

        /// <summary>
        /// `vaettir sapling` in the console: how many kills THIS machine has fed to a sapling this
        /// session, and the nearest sapling's count and which machine has it.
        ///
        /// Written for paired-kill-credit-killer.txt and paired-kill-credit-watcher.txt in
        /// Utangard's scenarios (LHM-36), which check that a kill feeds a sapling once however many
        /// players watch it die. The sapling's own count cannot say that. A second machine feeding
        /// the same kill claims the sapling and writes its copy of the count plus one, and while
        /// that copy is a moment old it writes the very number the owner has just written, so a
        /// kill fed twice can read as fed once. The count a machine keeps of its own feeds moves
        /// only when that machine fed, whatever the sapling ends up saying.
        ///
        /// isCheat false: it reads a sapling this game has loaded and a number kept in memory, and
        /// changes nothing.
        /// </summary>
        [HarmonyPatch]
        internal static class Readout
        {
            /// <summary>How far it looks for a sapling. Well past FeedRange's default of 24.</summary>
            private const float Reach = 30f;

            /// <summary>
            /// Process-wide: Terminal's command table is a private static nothing clears, so a
            /// second registration would be a duplicate that outlives the world.
            /// </summary>
            private static bool _registered;

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Terminal), "InitTerminal")]
            private static void Register()
            {
                if (_registered) return;
                _registered = true;

                new Terminal.ConsoleCommand("vaettir",
                    "vaettir sapling - the kills this machine has fed to a sapling, and the nearest sapling's count and owner",
                    new Terminal.ConsoleEvent(OnCommand), isCheat: false);
            }

            private static void OnCommand(Terminal.ConsoleEventArgs args)
            {
                var term = args.Context;
                if (term == null) return;

                if (args.Length < 2 || args[1].ToLowerInvariant() != "sapling")
                {
                    term.AddString("vaettir sapling - how many kills this machine has fed to a sapling this session, "
                                   + "and how much the nearest sapling within 30 m has been fed and which machine has it");
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    term.AddString("vaettir sapling: no character in a world yet");
                    return;
                }

                term.AddString("vaettir sapling: fedhere=" + FedHere
                               + "   (kills this machine fed to a sapling this session; only the machine that has the creature feeds)");

                Sapling best = null;
                var bestDistance = Reach;

                foreach (var sapling in Sapling.All)
                {
                    if (sapling == null) continue;

                    var distance = Vector3.Distance(sapling.transform.position, player.transform.position);
                    if (distance > bestDistance) continue;

                    best = sapling;
                    bestDistance = distance;
                }

                if (best == null)
                {
                    term.AddString("vaettir sapling: nearest=none   (no sapling within 30 m)");
                    return;
                }

                ZNetView nview;
                var owner = !best.TryGetComponent(out nview) || !nview.IsValid() ? "nobody"
                          : nview.IsOwner() ? "here" : "elsewhere";

                term.AddString("vaettir sapling: fed=" + Mathf.FloorToInt(best.Blood) + "/" + Mathf.FloorToInt(best.Needed)
                               + " owner=" + owner
                               + "   (" + SaplingPrefab.Name + ", "
                               + bestDistance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                               + " m away, belongs to "
                               + (owner == "here" ? "this machine" : owner == "elsewhere" ? "another machine" : "no machine")
                               + ")");
            }
        }
    }
}
