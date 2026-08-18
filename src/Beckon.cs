using System.Collections.Generic;
using HarmonyLib;
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

        /// <summary>How full the seed under this spawner is, 0 to 1. Read by the wave patch,
        /// which has a SpawnArea and needs the sapling's number off it.</summary>
        internal float Progress
        {
            get { return _sapling != null ? _sapling.Progress : 0f; }
        }

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

            if (Time.time - _lastHunt < HuntSweep) return;
            _lastHunt = Time.time;

            Hunt();
        }

        /// <summary>Seconds between hunt sweeps. They spawn no faster than one per two
        /// seconds, so anything tighter than this is walking the list for nothing.</summary>
        private const float HuntSweep = 2f;

        private static float _lastHunt = -99f;

        /// <summary>
        /// Tells everything the sapling called to come and find you.
        ///
        /// This is the other half of arriving from a distance. Spawning them out in the
        /// trees is easy; without this they simply stay out there, because a greydwarf with
        /// nothing to be angry about wanders where it spawned. SetHuntPlayer is what a
        /// vanilla raid uses and it means exactly this: stop wandering, go and find someone.
        ///
        /// Hunting players rather than the sapling, deliberately. There is no vanilla "go
        /// and attack this object" to ride, the player is standing at the sapling anyway,
        /// and it makes the raid land on the person holding the ground rather than on the
        /// thing they are holding it for - which is both better to play and much easier on
        /// a piece with 500 health.
        ///
        /// No bookkeeping of which ones have been told. SetHuntPlayer returns immediately
        /// when the flag already matches, so calling it again every two seconds costs a
        /// comparison, and a creature that arrived by some other route joining in is a
        /// feature rather than a leak.
        /// </summary>
        private void Hunt()
        {
            var roster = Names();
            if (roster.Count == 0) return;

            var here = transform.position;
            var reach = _area.m_nearRadius;

            foreach (var ai in BaseAI.BaseAIInstances)
            {
                if (ai == null) continue;

                if (Utils.DistanceXZ(ai.transform.position, here) > reach) continue;

                // Tamed creatures are excluded for the same reason vanilla's own instance
                // count excludes them: a boar somebody raised is not part of the raid, and
                // sending it hunting players means sending it at its owner.
                var character = ai.GetComponent<Character>();
                if (character == null || character.IsTamed()) continue;

                if (!roster.Contains(Utils.GetPrefabName(ai.gameObject))) continue;

                ai.SetHuntPlayer(true);
            }
        }

        /// <summary>The roster's prefab names, resolved once. Compared by name because a
        /// spawned creature is a clone and carries the "(Clone)" suffix.</summary>
        private static HashSet<string> _names;

        private static HashSet<string> Names()
        {
            if (_names != null) return _names;

            _names = new HashSet<string>();
            foreach (var entry in (GroveConfig.BeckonRoster.Value ?? "").Split(','))
            {
                var name = entry.Split(':')[0].Trim();
                if (name.Length > 0) _names.Add(name);
            }

            return _names;
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

        /// <summary>
        /// How far out they appear, nearest first.
        ///
        /// A band rather than a single radius, because vanilla's FindSpawnPoint picks its
        /// point at Random.Range(0, m_spawnRadius) - uniform across a disc, which puts most
        /// of them near the middle however wide the radius is. That is why they used to
        /// materialise on top of the sapling: widening the radius alone would only have
        /// scattered them between nought and the edge.
        /// </summary>
        /// <summary>
        /// How many arrive together, for a sapling this far along.
        ///
        /// Ramped for the same reason the interval is: the fight should be at its heaviest
        /// when the seed is nearly open. Rounded rather than floored, so the top of the
        /// range is actually reached on the last few kills instead of only exactly at full.
        /// </summary>
        internal static int Pack(float progress)
        {
            var parts = (GroveConfig.BeckonPack.Value ?? "").Split('-');

            var fewest = Mathf.Max(1f, Parse(parts.Length > 0 ? parts[0] : "", 2f));
            var most = Mathf.Max(fewest, Parse(parts.Length > 1 ? parts[1] : "", 5f));

            return Mathf.Clamp(Mathf.RoundToInt(
                Mathf.Lerp(fewest, most, Mathf.Clamp01(progress))), 1, 20);
        }

        /// <summary>
        /// Where the current wave is coming from, in degrees, or a negative number when
        /// nothing is arriving.
        ///
        /// A wave scattered evenly around the ring is four separate greydwarfs that happen
        /// to share a timer; one that comes out of the trees on a single side is a war band,
        /// and you can turn to face it. The spawn-point prefix reads this and jitters around
        /// it rather than picking a fresh angle per creature.
        /// </summary>
        internal static float WaveAngle = -1f;

        internal static void Band(out float near, out float far)
        {
            var parts = (GroveConfig.BeckonDistance.Value ?? "").Split('-');

            near = Parse(parts.Length > 0 ? parts[0] : "", 35f);
            far = Parse(parts.Length > 1 ? parts[1] : "", 60f);

            if (far < near)
            {
                var swap = near;
                near = far;
                far = swap;
            }

            // Far enough out to be out of sight, and inside the zone the game has actually
            // loaded. Past about 64m the ground under a spawn point may not exist yet, and
            // FindFloor simply fails - which reads as the sapling having stopped calling.
            near = Mathf.Clamp(near, 5f, 60f);
            far = Mathf.Clamp(far, near + 5f, 70f);
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

            float near, far;
            Band(out near, out far);

            // Vanilla's own upper bound, kept in step with ours. FindSpawnPoint is replaced
            // by a prefix below so this is not what actually places anything - but if that
            // patch ever fails to apply, this is the number the original falls back to, and
            // it should be the far edge rather than the old twelve metres.
            area.m_spawnRadius = far;

            // Wide enough to count everything on its way in, not just what has arrived.
            // m_maxNear is the cap that matters, and if it only counted the clearing then
            // six fighting you plus ten still running would all be legal at once.
            area.m_nearRadius = far + 10f;
            area.m_farRadius = 1000f;

            area.m_maxNear = Mathf.Max(1, GroveConfig.BeckonMaxNear.Value);
            area.m_maxTotal = Mathf.Max(area.m_maxNear, GroveConfig.BeckonMaxTotal.Value);

            // Vanilla's nests use 256m, which is most of a zone in every direction and
            // would have a sapling filling a forest you are nowhere near. Close enough to
            // hear is the whole intent.
            area.m_triggerDistance = Mathf.Max(8f, GroveConfig.BeckonRange.Value);

            area.m_onGroundOnly = true;

            // False, and this is half of why they come at you. SetPatrolPoint makes a
            // creature treat where it spawned as the place it belongs, so with it on they
            // stood around in the trees exactly where they appeared. Off, they are free to
            // move, and Hunt below is what tells them where.
            area.m_setPatrolSpawnPoint = false;

            if (clone.GetComponent<Beckon>() == null) clone.AddComponent<Beckon>();

            GrovePlugin.Log.LogInfo(string.Format(
                "The sapling calls {0} kind(s), every {1:0}s falling to {2:0}s, arriving "
                + "from {3:0}-{4:0}m, while a player is within {5:0}m.",
                area.m_prefabs.Count, slowest, fastest, near, far, area.m_triggerDistance));
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

    /// <summary>
    /// Turns vanilla's one-creature-per-interval into a wave.
    ///
    /// SpawnArea spawns exactly one per interval, which is a queue rather than a raid: a
    /// greydwarf, a wait, another greydwarf, and nothing that has to be handled as a group.
    /// The prefix picks a direction for the wave and the postfix runs the game's own
    /// SpawnOne for the rest of it.
    ///
    /// Calling SpawnOne rather than instantiating anything is what keeps the caps honest -
    /// it checks MaxNear and MaxTotal itself at the top, so a wave that would breach them
    /// comes up short instead of overrunning them. The reentrancy flag is not optional:
    /// without it each extra spawn would re-enter this postfix and the first wave would
    /// never end.
    /// </summary>
    [HarmonyPatch(typeof(SpawnArea), "SpawnOne")]
    internal static class BeckonWave
    {
        private static readonly System.Func<SpawnArea, bool> SpawnOne =
            AccessTools.MethodDelegate<System.Func<SpawnArea, bool>>(
                AccessTools.Method(typeof(SpawnArea), "SpawnOne"));

        private static bool _inWave;

        [HarmonyPrefix]
        private static void Direction(SpawnArea __instance)
        {
            if (_inWave || __instance == null) return;
            if (__instance.GetComponent<Beckon>() == null) return;

            Beckon.WaveAngle = Random.Range(0f, 360f);
        }

        [HarmonyPostfix]
        private static void Rest(SpawnArea __instance, bool __result)
        {
            if (_inWave || !__result || __instance == null) return;

            var beckon = __instance.GetComponent<Beckon>();
            if (beckon == null) return;

            var wanted = Beckon.Pack(beckon.Progress) - 1;
            if (wanted <= 0 || SpawnOne == null) return;

            _inWave = true;
            try
            {
                // Stops at the first refusal. SpawnOne returns false when a cap is reached
                // or when it could not find ground, and both mean the rest of this wave has
                // nowhere to go - retrying nine more times would only walk the instance list
                // nine more times for nothing.
                for (var i = 0; i < wanted; i++)
                    if (!SpawnOne(__instance)) break;
            }
            finally
            {
                _inWave = false;
                Beckon.WaveAngle = -1f;
            }
        }
    }

    /// <summary>
    /// Where a called greydwarf appears: out in the trees, not on top of the seed.
    ///
    /// Vanilla's FindSpawnPoint takes Random.Range(0f, m_spawnRadius), which is uniform
    /// across a disc - so most points land near the middle whatever the radius is set to.
    /// That is why widening the radius alone does not work and why they seemed to
    /// materialise around the sapling: they were.
    ///
    /// Replacing the search rather than nudging its answer, because the answer has already
    /// been validated against the ground by the time a postfix could see it, and moving a
    /// checked point puts a greydwarf inside a rock. The replacement makes the same two
    /// ZoneSystem calls in the same order; only the distance is drawn from a band.
    ///
    /// Every other SpawnArea in the game - every nest, every camp - is left alone. The
    /// prefix runs vanilla's own method for anything that is not one of ours.
    /// </summary>
    [HarmonyPatch(typeof(SpawnArea), "FindSpawnPoint")]
    internal static class BeckonSpawnPoint
    {
        private const int Attempts = 12;

        [HarmonyPrefix]
        private static bool Ring(SpawnArea __instance, ref Vector3 point, ref bool __result)
        {
            if (__instance == null || __instance.GetComponent<Beckon>() == null) return true;

            var zones = ZoneSystem.instance;
            if (zones == null) return true;

            float near, far;
            Beckon.Band(out near, out far);

            var centre = __instance.transform.position;

            for (var i = 0; i < Attempts; i++)
            {
                // One side of the ring while a wave is arriving, anywhere on it otherwise.
                // The spread is deliberately narrow: much wider and a "wave" is just four
                // greydwarfs that happen to share a timer.
                var angle = Beckon.WaveAngle >= 0f
                    ? Beckon.WaveAngle + Random.Range(-20f, 20f)
                    : Random.Range(0f, 360f);

                var spot = centre
                           + Quaternion.Euler(0f, angle, 0f)
                           * Vector3.forward * Random.Range(near, far);

                float height;
                if (!zones.FindFloor(spot, out height)) continue;
                if (__instance.m_onGroundOnly && zones.IsBlocked(spot)) continue;

                spot.y = height + 0.1f;
                point = spot;
                __result = true;
                return false;
            }

            // Failing is normal and harmless: SpawnArea simply tries again in two seconds.
            // A band that lands in water or on a cliff the whole way round is a bad place
            // to have planted, not a bug.
            point = Vector3.zero;
            __result = false;
            return false;
        }
    }
}
