using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Grove
{
    /// <summary>
    /// A map marker on a planted sapling, and taking it off again.
    ///
    /// The sapling is the one piece in the mod you are meant to walk away from. It grows
    /// on kills rather than on a clock, so the intended play is to plant it, go and do
    /// something else near it for an hour, and come back - and until now the mod gave you
    /// nothing at all to come back *to*. A seed in bare ground at the edge of a forest is
    /// not findable from fifty metres away, and losing one to "I know it was around here"
    /// costs the same ancient seed as losing it to a brute.
    ///
    /// Pins are per-player and client-side. Nothing here is networked and nothing here
    /// touches a ZDO: each player who has seen the sapling gets their own marker, which is
    /// how vanilla's own pins work and means a server needs to know nothing about this.
    /// </summary>
    internal static class SaplingPin
    {
        /// <summary>
        /// Minimap.m_pins is private and so is GetClosestPin, so the list is read directly.
        ///
        /// Reflected rather than tracked in a dictionary of our own because the pins are
        /// *saved* into the player profile: a dictionary is empty again after a relog while
        /// the pin is still on the map, so every session would add a second marker on top
        /// of the first. Asking the map what it already has is the only answer that
        /// survives being closed and reopened.
        /// </summary>
        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> PinsRef =
            AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");

        /// <summary>How close two pins have to be to count as the same one. Generous: a
        /// sapling does not move, so anything nearby wearing our name is ours.</summary>
        private const float Same = 3f;

        /// <summary>
        /// Puts a marker on this sapling if there is not one there already.
        ///
        /// Called from Start rather than Awake. Awake runs before ZNetView has necessarily
        /// handed out its ZDO, and more practically the Minimap does not exist during the
        /// first frames of a world load - this simply does nothing then and the sapling
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
        /// Takes the marker off, whether the sapling opened or was destroyed.
        ///
        /// Both endings want the same thing - there is no longer a sapling there - and
        /// neither wants a stale pin left behind for the player to walk to. A spirit that
        /// has risen is standing in the same place for as long as it takes you to press
        /// use, so nothing is lost by clearing on open.
        /// </summary>
        public static void Clear(Vector3 at, string label)
        {
            var map = Minimap.instance;
            if (map == null) return;

            var pin = Find(map, at, label);
            if (pin == null) return;

            map.RemovePin(pin);

            if (GroveConfig.Verbose.Value)
                GrovePlugin.Log.LogInfo("Unpinned " + label + " at " + at + ".");
        }

        /// <summary>
        /// The nearest pin wearing our label. Matched on the name as well as the place, so
        /// a player's own marker sitting on the same spot is never removed by us.
        /// </summary>
        private static Minimap.PinData Find(Minimap map, Vector3 at, string label)
        {
            List<Minimap.PinData> pins;

            try { pins = PinsRef(map); }
            catch (System.Exception e)
            {
                GrovePlugin.LogOnce("Could not read the map's pins: " + e.Message);
                return null;
            }

            if (pins == null) return null;

            foreach (var pin in pins)
            {
                if (pin == null || pin.m_name != label) continue;

                // Flat distance. A pin sits at the sapling's own height and the map is
                // read from above, but a sapling on a slope can be a metre off in y from
                // where the pin was placed if the ground was smoothed afterwards.
                var dx = pin.m_pos.x - at.x;
                var dz = pin.m_pos.z - at.z;
                if (dx * dx + dz * dz <= Same * Same) return pin;
            }

            return null;
        }
    }
}
