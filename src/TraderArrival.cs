using UnityEngine;

namespace Grove
{
    /// <summary>
    /// Brings the visitor to your bed, once, after you have housed a spirit.
    ///
    /// It is not spawned in front of you. The rule is that it is *already there* when
    /// the area next loads - you come home and it has arrived - because something that
    /// pops into existence while you watch is a spawn, and something that is standing
    /// there when you get back has come to see you. That difference is the entire
    /// feature and it costs nothing but where the check runs.
    ///
    /// Deliberately built on the bed rather than on a piece you place. A vaettr that
    /// comes because you housed one is a consequence; a vaettr you build a hut for is
    /// furniture.
    /// </summary>
    internal static class TraderArrival
    {
        /// <summary>
        /// The world's memory that a spirit has been housed here.
        ///
        /// A global key, so it is world state rather than character state and a second
        /// player who never grew a sapling still finds the visitor at their own bed.
        /// Valueless and written exactly once: every GlobalKeyAdd also writes
        /// key + " " + value into the saved player profile's m_knownWorldKeys, so a key
        /// whose value moves grows that dictionary forever. This one never moves.
        /// </summary>
        private const string HousedKey = "vaettir_housed";

        /// <summary>How long between attempts. This walks loaded objects, so not often.</summary>
        private const float Interval = 5f;

        private static float _nextTry;
        private static bool _done;

        public static void Invalidate()
        {
            _done = false;
            _nextTry = 0f;
        }

        /// <summary>
        /// Sets the key, if it is not already set.
        ///
        /// Checked before writing, and that check is not politeness. RPC_SetGlobalKey has
        /// no permission gate and ends in SendGlobalKeys(Everybody), so every write
        /// rebroadcasts the entire key list to every player in the world. Writing it on
        /// each heartwood taken would do that every time.
        /// </summary>
        public static void RememberHoused()
        {
            var zone = ZoneSystem.instance;
            if (zone == null || zone.GetGlobalKey(HousedKey)) return;

            zone.SetGlobalKey(HousedKey);
            GrovePlugin.Log.LogInfo("A spirit has been housed in this world. Someone "
                                    + "will come.");
        }

        /// <summary>
        /// Idempotent, cheap once done, and safe to call every frame.
        /// </summary>
        public static void Check()
        {
            if (_done || !GroveConfig.TraderEnabled.Value) return;
            if (Time.time < _nextTry) return;
            _nextTry = Time.time + Interval;

            var zone = ZoneSystem.instance;
            var player = Player.m_localPlayer;
            if (zone == null || player == null || Game.instance == null) return;
            if (!TraderPrefab.Ready) return;

            if (!zone.GetGlobalKey(HousedKey)) return;

            // The bed, as the game itself understands it: the point you respawn at,
            // written when you sleep. Guessing at "home" any other way - the nearest
            // workbench, the middle of your pieces - would be a heuristic that is wrong
            // in somebody's base.
            var profile = Game.instance.GetPlayerProfile();
            if (profile == null || !profile.HaveCustomSpawnPoint()) return;

            var bed = profile.GetCustomSpawnPoint();

            // Only when you are actually there. Instantiating into a zone nobody has
            // loaded is how you get an object that exists and is not in the world, and
            // the whole point is that it is standing there when you arrive.
            if (Vector3.Distance(player.transform.position, bed) > 48f) return;

            if (AlreadyHere(bed))
            {
                _done = true;
                return;
            }

            Arrive(bed);
        }

        /// <summary>
        /// Looks for one that has already come.
        ///
        /// By Trader component rather than by prefab name, and across everything loaded
        /// rather than only near the bed: a visitor the player has since pushed, or that
        /// settled a little off, still counts. The failure this guards against is two of
        /// them, which cannot be undone without deleting a ZDO by hand.
        /// </summary>
        private static bool AlreadyHere(Vector3 bed)
        {
            foreach (var trader in Object.FindObjectsOfType<Trader>())
            {
                if (trader == null) continue;

                // Utils.GetPrefabName strips the "(Clone)" that Instantiate appends.
                if (Utils.GetPrefabName(trader.gameObject) != TraderPrefab.Name) continue;
                if (Vector3.Distance(trader.transform.position, bed) > 96f) continue;

                return true;
            }

            return false;
        }

        private static void Arrive(Vector3 bed)
        {
            var prefab = ZNetScene.instance.GetPrefab(TraderPrefab.Name);
            if (prefab == null) return;

            var spot = Spot(bed);

            // Owned by whoever placed it, which is whoever came home. The ZNetView is
            // the donor's and is persistent, so this survives the zone unloading and is
            // still there next time.
            Object.Instantiate(prefab, spot, Quaternion.LookRotation(
                Vector3.ProjectOnPlane(bed - spot, Vector3.up).normalized));

            _done = true;

            GrovePlugin.Log.LogInfo("The visitor has arrived at " + spot + ".");
        }

        /// <summary>
        /// Somewhere near the bed, on the ground, at a stable angle.
        ///
        /// The angle comes off the bed's own coordinates rather than a random roll, so
        /// the visitor stands in the same place every time the question is asked. A
        /// random spot would mean two clients racing the check could each pick their own
        /// and produce two of them at different angles.
        /// </summary>
        private static Vector3 Spot(Vector3 bed)
        {
            var distance = Mathf.Max(2f, GroveConfig.TraderDistance.Value);

            var angle = (Mathf.Abs(bed.x * 7919f + bed.z * 6271f) % 360f) * Mathf.Deg2Rad;
            var spot = bed + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            // Onto the ground. A bed upstairs would otherwise put the visitor in mid-air
            // over the doorstep, and a Character with a Rigidbody would then fall.
            float ground;
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetSolidHeight(spot, out ground))
                spot.y = ground;

            return spot;
        }
    }
}
