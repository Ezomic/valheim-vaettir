using System.Collections.Generic;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// The planted seed calls the forest to it.
    ///
    /// Without this the sapling is entirely passive, and the quest it sets is really "go
    /// and find a place greydwarfs already walk through, then stand in it". That is a
    /// scouting problem rather than a defending one, and it is the wrong half of what the
    /// design notes say the piece is for: you are meant to be fighting for an hour beside
    /// something you can lose. A seed that draws them makes the place the thing that
    /// matters, and it is what finally earns the sapling's health, its "is being torn at"
    /// message and the whole argument for why it can be destroyed at all.
    ///
    /// It also closes the loop. What it summons is what feeds it, so a sapling planted in
    /// an empty meadow is slow rather than impossible, and one planted next to a real camp
    /// is still faster - the camp's greydwarfs count too.
    ///
    /// Ridden on vanilla's own SpawnArea, which is the component a greydwarf nest is made
    /// of, rather than a spawner of ours. That buys the whole of it: the near/total caps,
    /// the level-up rolls, finding a floor to stand on, the spawn effect, and - the part
    /// that matters most here - UpdateSpawn's own guards, which run only on the owner,
    /// only inside the active area and only with a player in range. Nothing happens while
    /// you are asleep, which is the sapling's first principle.
    /// </summary>
    internal sealed class Beckon : MonoBehaviour
    {
        private Sapling _sapling;
        private SpawnArea _area;

        private float _slowest;
        private float _fastest;

        private void Awake()
        {
            _sapling = GetComponent<Sapling>();
            _area = GetComponent<SpawnArea>();

            Interval(out _slowest, out _fastest);
        }

        /// <summary>
        /// The closer it is to opening, the harder it calls.
        ///
        /// A constant rate would be a wave you learn to stand in. Ramping it means the last
        /// few kills are the loudest part of the fight, which puts the tension where the
        /// end is - and it reads without any explanation, because you can hear it.
        ///
        /// Both ends of the range are raid numbers rather than wildlife numbers. The first
        /// pair here was 90 seconds falling to 30, which was one greydwarf every minute and
        /// a half: something to deal with between other jobs rather than something to hold
        /// ground against, and the whole point of the sapling is the holding.
        ///
        /// Written into the component rather than tracked ourselves: SpawnArea compares its
        /// own timer against this field every two seconds, so changing it is the whole of
        /// the mechanism.
        /// </summary>
        private void Update()
        {
            if (_area == null || _sapling == null) return;

            _area.m_spawnIntervalSec =
                Mathf.Lerp(_slowest, _fastest, Mathf.Clamp01(_sapling.Progress));
        }

        private static void Interval(out float slowest, out float fastest)
        {
            var parts = (GroveConfig.BeckonInterval.Value ?? "").Split('-');

            slowest = Parse(parts.Length > 0 ? parts[0] : "", 20f);
            fastest = Parse(parts.Length > 1 ? parts[1] : "", 6f);

            // Guarded rather than trusted. The two are written slowest-first because that
            // is the order they happen in, and somebody reading "20-6" as a range will
            // eventually write "6-20" - which would make a nearly-open sapling the quietest
            // it has ever been.
            if (fastest > slowest)
            {
                var swap = slowest;
                slowest = fastest;
                fastest = swap;
            }

            // Two, not five. The floor is SpawnArea's own two-second repeat - it wakes on
            // that schedule and spawns at most one creature each time, so nothing below it
            // can mean anything. Five was written when this ran at 90 and 30 and no setting
            // could ever have reached it; at raid numbers it sits inside the useful range,
            // and someone asking for 2 would have quietly been given 5.
            slowest = Mathf.Max(2f, slowest);
            fastest = Mathf.Max(2f, fastest);
        }

        private static float Parse(string text, float fallback)
        {
            float value;
            return float.TryParse((text ?? "").Trim(),
                                  System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture,
                                  out value) ? value : fallback;
        }

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Puts the spawner on the sapling prefab, once, at build time.
        ///
        /// On the prefab rather than added per instance, because the prefab is built inside
        /// an inactive holder where no Awake runs - so SpawnArea's own InvokeRepeating does
        /// not start on the shared copy, and every planted sapling gets a live one of its
        /// own. Adding it in a component's Awake instead would start the repeat on the very
        /// frame we were still configuring it.
        /// </summary>
        public static void Attach(GameObject clone)
        {
            if (!GroveConfig.Beckon.Value) return;

            var area = clone.GetComponent<SpawnArea>();
            if (area == null) area = clone.AddComponent<SpawnArea>();

            area.m_prefabs = Roster();
            if (area.m_prefabs.Count == 0)
            {
                GrovePlugin.LogOnce("Nothing in BeckonRoster resolved, so the sapling will "
                                    + "call nothing. It still grows on kills made near it.");
                Object.DestroyImmediate(area);
                return;
            }

            float slowest, fastest;
            Interval(out slowest, out fastest);
            area.m_spawnIntervalSec = slowest;

            area.m_spawnRadius = Mathf.Max(1f, GroveConfig.BeckonRadius.Value);

            // The near radius is what m_maxNear counts inside, so it has to be wider than
            // the spawn radius or the cap counts a circle the spawns mostly land outside of
            // and never stops anything.
            area.m_nearRadius = Mathf.Max(area.m_spawnRadius + 4f,
                                          GroveConfig.BeckonRadius.Value * 2f);
            area.m_farRadius = 1000f;

            area.m_maxNear = Mathf.Max(1, GroveConfig.BeckonMaxNear.Value);
            area.m_maxTotal = Mathf.Max(area.m_maxNear, GroveConfig.BeckonMaxTotal.Value);

            // Vanilla's nests use 256m, which is most of a zone in every direction and
            // would have a sapling filling a forest you are nowhere near. Close enough to
            // hear is the whole intent.
            area.m_triggerDistance = Mathf.Max(8f, GroveConfig.BeckonRange.Value);

            area.m_onGroundOnly = true;
            area.m_setPatrolSpawnPoint = true;

            if (clone.GetComponent<Beckon>() == null) clone.AddComponent<Beckon>();

            GrovePlugin.Log.LogInfo(string.Format(
                "The sapling calls {0} kind(s), every {1:0}s falling to {2:0}s, "
                + "within {3:0}m of a player.",
                area.m_prefabs.Count, slowest, fastest, area.m_triggerDistance));
        }

        /// <summary>
        /// What it calls, read from config and resolved against the world.
        ///
        /// The same shape as FeedWeights on purpose - name:number, comma separated - and
        /// deliberately a separate setting from it. They answer different questions: one is
        /// what a death is worth and the other is what turns up, and tying them together
        /// would mean a shaman worth three points had to arrive three times as often.
        ///
        /// A name that does not resolve is logged and skipped rather than thrown, like
        /// every other prefab list in this mod.
        /// </summary>
        private static List<SpawnArea.SpawnData> Roster()
        {
            var list = new List<SpawnArea.SpawnData>();
            var scene = ZNetScene.instance;
            if (scene == null) return list;

            foreach (var entry in (GroveConfig.BeckonRoster.Value ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var name = parts[0].Trim();
                if (name.Length == 0) continue;

                float weight;
                if (!float.TryParse(parts[1].Trim(),
                                    System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out weight) || weight <= 0f) continue;

                var prefab = scene.GetPrefab(name);
                if (prefab == null)
                {
                    GrovePlugin.LogOnce("BeckonRoster names '" + name + "', which does not "
                                        + "exist in this world.");
                    continue;
                }

                // Refused rather than spawned. SpawnArea reads BaseAI and Character off
                // whatever it is handed and dereferences the Character without checking,
                // so a prefab that is not a creature throws inside vanilla's own code -
                // an hour after planting, in a stack trace that names none of this.
                if (prefab.GetComponent<Character>() == null)
                {
                    GrovePlugin.LogOnce("BeckonRoster names '" + name + "', which is not a "
                                        + "creature. Skipped.");
                    continue;
                }

                list.Add(new SpawnArea.SpawnData
                {
                    m_prefab = prefab,
                    m_weight = weight,

                    // One star only. Levelling is what a nest does over a whole night in a
                    // biome you chose to live in; here it would mean a two-star brute
                    // arriving on a piece with 500 health because you were unlucky.
                    m_minLevel = 1,
                    m_maxLevel = 1
                });
            }

            return list;
        }
    }
}
