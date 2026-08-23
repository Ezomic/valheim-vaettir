using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// A map marker on a planted sapling, and taking it off again.
    ///
    /// The sapling is the one piece in the mod you are meant to walk away from. It grows on
    /// kills rather than on a clock, so the intended play is to plant it, hold the clearing
    /// for a while, and come back - and a seed in bare ground is not findable from fifty
    /// metres away. Losing one to "I know it was around here" costs the same ancient seed as
    /// losing it to a brute.
    ///
    /// Pins are per-player and client-side. Nothing here is networked and nothing here
    /// touches a ZDO: each player who has seen the sapling gets their own marker, which is
    /// how vanilla's own pins work and means a server needs to know nothing about this.
    ///
    /// <b>The pin is removed by reconciling, not by an event.</b> The first version cleared
    /// it from Sapling.OnDestroy, guarded by a test for whether the ZDO was really gone or
    /// the zone had merely unloaded - and that test could not work, which is why pins stayed
    /// on the map after a sapling opened. ZDOMan.DestroyZDO does not remove anything: it
    /// adds the uid to m_destroySendList and the ZDO leaves m_objectsByID some frames later,
    /// so on the frame OnDestroy runs the ZDO is still there and the sapling reads as merely
    /// unloaded. Both endings failed the same way.
    ///
    /// Asking the world instead removes the guess. A pin whose zone is loaded and which has
    /// no sapling standing under it is stale, whatever became of the sapling - opened,
    /// destroyed, removed by another player, or taken by a console command - and a pin in an
    /// unloaded zone is simply not answerable and is left alone. It is the same rule the
    /// prefab registry runs on: ask the world, never a flag.
    /// </summary>
    internal static class SaplingPin
    {
        /// <summary>
        /// Minimap.m_pins is private and so is GetClosestPin, so the list is read directly.
        ///
        /// Reflected rather than tracked in a dictionary of our own because the pins are
        /// <i>saved</i> into the player profile: a dictionary is empty again after a relog
        /// while the pin is still on the map, so every session would add a second marker on
        /// top of the first. Asking the map what it already has is the only answer that
        /// survives being closed and reopened.
        /// </summary>
        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        /// <summary>How close a pin and a sapling have to be to be the same thing. Generous:
        /// a sapling does not move, so anything nearby wearing our name is ours.</summary>
        private const float Same = 3f;

        /// <summary>
        /// Seconds between sweeps. A pin lingering for a second after the spirit rises is
        /// not worth a per-frame walk of the whole pin list, and the list includes every pin
        /// the player has ever placed by hand.
        /// </summary>
        private const float Sweep = 1f;

        private static float _lastSweep = -99f;

        /// <summary>Pins that looked stale on the previous sweep, keyed by rounded position.
        /// A pin has to look stale twice running before it is taken off.</summary>
        private static readonly HashSet<string> _missing = new HashSet<string>();

        private static string Key(Vector3 at)
        {
            return Mathf.RoundToInt(at.x) + ":" + Mathf.RoundToInt(at.z);
        }

        /// <summary>
        /// Puts a marker on this sapling if there is not one there already.
        ///
        /// Called from Start rather than Awake. Awake runs before ZNetView has necessarily
        /// handed out its ZDO, and more practically the Minimap does not exist during the
        /// first frames of a world load - this simply does nothing then, and the sapling
        /// gets its pin when its zone next loads, which is the frame you can see it anyway.
        /// </summary>
        public static void Mark(Vector3 at, string label)
        {
            if (!GroveConfig.PinSaplings.Value) return;

            var map = Minimap.instance;
            if (map == null) return;

            if (Find(map, at, label) != null) return;

            map.AddPin(at, GroveConfig.PinIcon.Value, label, save: true, isChecked: false);

            if (GroveConfig.Verbose.Value)
                GrovePlugin.Log.LogInfo("Pinned " + label + " at " + at + ".");
        }

        /// <summary>
        /// Takes off any of our markers that no longer has a sapling under it.
        ///
        /// Deliberately not gated on PinSaplings. Turning the setting off should stop new
        /// pins appearing, not strand the ones already on the map with no way of ever
        /// clearing them - and this is also what tidies up after the version of this file
        /// that could not clear them at all.
        /// </summary>
        public static void Reconcile(string label)
        {
            if (Time.time - _lastSweep < Sweep) return;
            _lastSweep = Time.time;

            var map = Minimap.instance;
            var zones = ZoneSystem.instance;
            if (map == null || zones == null || string.IsNullOrEmpty(label)) return;

            List<Minimap.PinData> pins;
            if (!TryPins(map, out pins)) return;

            // Backwards, because removing from the list this is walking is the whole point.
            for (var i = pins.Count - 1; i >= 0; i--)
            {
                var pin = pins[i];
                if (pin == null || pin.m_name != label) continue;

                // An unloaded zone cannot be asked. No sapling object exists there whether
                // or not one is standing, so anything decided here would delete the pin of
                // every sapling the player has walked away from - which is the one moment
                // the pin is the only thing they have.
                if (!zones.IsZoneLoaded(pin.m_pos)) continue;

                if (Standing(pin.m_pos)) { _missing.Remove(Key(pin.m_pos)); continue; }

                // Two strikes, a second apart. A zone reports itself loaded a moment before
                // the objects in it have been instantiated, so a single sweep landing in
                // that window finds the pin with no sapling under it and removes one that
                // is about to exist - the log showed an Unpinned immediately followed by a
                // Pinned at the same spot on every world load.
                var key = Key(pin.m_pos);
                if (!_missing.Contains(key)) { _missing.Add(key); continue; }

                _missing.Remove(key);
                map.RemovePin(pin);

                if (GroveConfig.Verbose.Value)
                    GrovePlugin.Log.LogInfo("Unpinned " + label + " at " + pin.m_pos + ".");
            }
        }

        /// <summary>Whether a loaded sapling is standing at this spot.</summary>
        private static bool Standing(Vector3 at)
        {
            foreach (var sapling in Sapling.All)
            {
                if (sapling == null) continue;

                // Flat distance. The pin sits at the sapling's own height, but ground
                // smoothed after planting can move one a metre in y from where it was
                // pinned, and the map is read from above regardless.
                var dx = sapling.transform.position.x - at.x;
                var dz = sapling.transform.position.z - at.z;
                if (dx * dx + dz * dz <= Same * Same) return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest pin wearing our label. Matched on the name as well as the place, so a
        /// player's own marker sitting on the same spot is never mistaken for ours.
        /// </summary>
        private static Minimap.PinData Find(Minimap map, Vector3 at, string label)
        {
            List<Minimap.PinData> pins;
            if (!TryPins(map, out pins)) return null;

            foreach (var pin in pins)
            {
                if (pin == null || pin.m_name != label) continue;

                var dx = pin.m_pos.x - at.x;
                var dz = pin.m_pos.z - at.z;
                if (dx * dx + dz * dz <= Same * Same) return pin;
            }

            return null;
        }

        private static bool TryPins(Minimap map, out List<Minimap.PinData> pins)
        {
            pins = null;

            try { pins = PinsRef(map); }
            catch (System.Exception e)
            {
                GrovePlugin.LogOnce("Could not read the map's pins: " + e.Message);
                return false;
            }

            return pins != null;
        }
    }
}
