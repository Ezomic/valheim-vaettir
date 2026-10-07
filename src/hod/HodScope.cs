using UnityEngine;

namespace Hod
{
    /// <summary>
    /// Where the bench service is looking, and whether it is looking at all.
    ///
    /// <b>This class is the single answer to every "where" question in this folder, and that
    /// is the point of it rather than a tidiness.</b> Hirsla asked each of its nine call
    /// sites to pass <c>Player.m_localPlayer.transform.position</c>, with a comment on the
    /// chest cache saying the cache was only safe because every caller happened to pass the
    /// same point. That is an invariant maintained by eye, and the failure when it breaks is
    /// the worst one this feature has: the count sums one set of chests, the consume walks
    /// another, <c>HaveRequirements</c> says yes on stock that is never spent, and vanilla's
    /// <c>RemoveItem(string, ...)</c> returns void and stops quietly when it runs out. A free
    /// craft, with nothing in the log.
    ///
    /// So the point is not a parameter. <see cref="HodChests"/> asks this, every counting and
    /// spending path goes through <see cref="HodChests"/>, and a new caller cannot pass the
    /// wrong centre because there is nowhere to pass one.
    ///
    /// <b>The scope is a jib network, not a post (LHM-77).</b> A bench is served when it lies in
    /// the reach of a <see cref="HodNetwork"/>, and the chests that serve it are those in the
    /// reach of the SAME network. The network is a fixed thing in the world: the set of chests
    /// does not change while the player shuffles at the bench, and a bench that worked a moment
    /// ago cannot stop working because somebody took half a step.
    ///
    /// The hammer has no bench to measure from (LHM-76), so while a build question is open
    /// (<see cref="Building"/> above zero) the PLAYER's position stands in for the station's.
    /// Not the placement ghost's: the menu's greying and the requirement panel are asked with no
    /// ghost at all, and one rule for all three means a piece cannot look affordable in the
    /// menu and be refused at the click.
    ///
    /// The scope is shut whenever any link in the chain is missing, and there is no fallback
    /// to a wider sphere. A feature that quietly widens when its own piece is out of reach is a
    /// feature nobody can predict.
    /// </summary>
    internal static class HodScope
    {
        /// <summary>
        /// Resolved at most once a frame per kind of question, because the crafting panel asks
        /// <c>HaveRequirements</c> for every recipe in the game once a frame, each of those
        /// walks its requirements, and each requirement asks <c>CountItems</c> once per
        /// quality level. It would also let the answer change halfway through a frame in which
        /// a count and a consume both happen.
        ///
        /// -1 rather than 0, because frame 0 is a real frame.
        /// </summary>
        private static int _frame = -1;

        /// <summary>
        /// Whether the cached network was resolved for a build question. A bench question and a
        /// build question can both be asked in one frame and do not have the same answer.
        /// </summary>
        private static bool _cachedForBuilding;

        private static HodNet _net;

        /// <summary>
        /// Raised by <see cref="HodBuilding"/> around the hammer's own questions (the build
        /// menu, the requirement panel, the click that places a piece). A counter and not a
        /// bool, for the reason HodCrafting's depth is one: the brackets nest.
        /// </summary>
        internal static int Building;

        /// <summary>The jib network whose chests are being counted, or null when the scope is shut.</summary>
        public static HodNet Net
        {
            get
            {
                Resolve();
                return _net;
            }
        }

        /// <summary>Whether a station, or a builder, in reach of a jib network is being served.</summary>
        public static bool IsOpen
        {
            get { return Net != null; }
        }

        private static void Resolve()
        {
            var building = Building > 0;
            if (_frame == Time.frameCount && _cachedForBuilding == building) return;
            _frame = Time.frameCount;
            _cachedForBuilding = building;
            _net = null;

            if (!HodConfig.Enabled.Value) return;

            // Shut for a reason that is not a setting: another mod asking the same questions
            // of the same chests (HodRuntime.Conflicts), or this build's own world hooks
            // failing to apply, which would leave the count running against a gate that was
            // never built (HodRuntime.LoseWorldHooks). Checked here rather than only at load
            // because this is what the whole feature hangs off.
            if (HodRuntime.Shut) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (building)
            {
                if (!HodConfig.BuildFromChests.Value || HodRuntime.BuildYields) return;

                _net = HodNetwork.Serving(player.transform.position);
                return;
            }

            // The station is read off the player rather than off InventoryGui, and that matters
            // for what the feature promises. Player.m_currentStation is exactly what
            // HaveRequirementItems and InventoryGui themselves consult when deciding whether a
            // recipe may be made here, so the scope opens for precisely the benches the game
            // already considers you to be standing at, and for nothing else. No station means
            // no scope: a recipe you can make out of your own pack anywhere is not a recipe a
            // piece of furniture gets to help with.
            var station = player.GetCurrentCraftingStation();
            if (station == null) return;

            _net = HodNetwork.Serving(station.transform.position);
        }

        /// <summary>
        /// Drops the resolved network, so the next question asks again. For a world change; the
        /// frame counter alone would clear it within a frame, but a reference into a world that
        /// has been torn down should not be reachable at all.
        /// </summary>
        public static void Forget()
        {
            _frame = -1;
            _net = null;
            Building = 0;
        }
    }
}
