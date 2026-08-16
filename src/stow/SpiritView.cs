using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// Draws whatever spirits a post has published, on every client that can see it.
    ///
    /// This is the whole of the multiplayer story, and the reason it is so small is that
    /// nothing about a spirit is authoritative. CarryRun decides and publishes; this reads
    /// and renders. The owner runs both, and runs *this* one exactly as everyone else does
    /// - which matters more than it sounds: a host that drew its spirits by some other
    /// path would be the only client whose rendering was never tested.
    ///
    /// Local GameObjects, never networked ones. They are rebuilt from the published string
    /// whenever it changes and destroyed when it empties, so a player walking into range
    /// mid-run picks up the flight already in progress and a player walking out leaves
    /// nothing behind.
    /// </summary>
    internal class SpiritView
    {
        private readonly StowPost _post;
        private readonly List<Carrier> _spirits = new List<Carrier>();

        private string _showing = "";

        public SpiritView(StowPost post)
        {
            _post = post;
        }

        /// <summary>
        /// Reads the post's published trips and makes the local spirits match.
        ///
        /// The string comparison is the throttle. A ZDO read is cheap, a reconcile is not,
        /// and the published value only changes when a spirit takes off or lands - so on
        /// nearly every frame this is one string compare and nothing else.
        /// </summary>
        public void Tick()
        {
            var nview = _post != null ? _post.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid()) { Clear(); return; }

            if (!StowConfig.CarrierEnabled.Value) { Clear(); return; }

            var published = SpiritTrips.Read(nview);
            if (published == _showing) return;

            _showing = published;
            Show(SpiritTrips.Decode(published));
        }

        /// <summary>Sends every spirit out, and forgets what was showing.</summary>
        public void Clear()
        {
            if (_spirits.Count == 0 && _showing.Length == 0) return;

            foreach (var spirit in _spirits)
                if (spirit != null) spirit.Dismiss();

            _spirits.Clear();
            _showing = "";
        }

        private void Show(List<SpiritTrips.Flight> flights)
        {
            // Grown and shrunk rather than rebuilt. A spirit that is merely starting its
            // next leg should carry on being the same object - destroying and respawning
            // it every trip would fade one out and pop another in at the same point, which
            // reads as a stutter rather than as a turn.
            for (var i = _spirits.Count - 1; i >= 0; i--)
                if (_spirits[i] == null) _spirits.RemoveAt(i);

            while (_spirits.Count > flights.Count)
            {
                var last = _spirits.Count - 1;
                if (_spirits[last] != null) _spirits[last].Dismiss();
                _spirits.RemoveAt(last);
            }

            while (_spirits.Count < flights.Count)
            {
                var born = CarrierModel.Build(flights[_spirits.Count].From);
                if (born == null) return; // no meshes; CarrierModel has said so once

                _spirits.Add(born);
            }

            for (var i = 0; i < flights.Count; i++)
            {
                if (_spirits[i] == null) continue;

                _spirits[i].Follow(flights[i]);
                _spirits[i].Carry(Cargo(flights[i].Cargo));
            }
        }

        /// <summary>
        /// The model for what a spirit is holding, or null when it is flying home empty.
        ///
        /// Built from the prefab name on the wire rather than from an ItemData, because
        /// the item itself never leaves the owner's post - remote clients have no ItemData
        /// to look at, only a name.
        /// </summary>
        private static GameObject Cargo(string prefabName)
        {
            return string.IsNullOrEmpty(prefabName) ? null : CarriedItem.For(prefabName);
        }
    }
}
