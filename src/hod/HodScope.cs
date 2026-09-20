using UnityEngine;
using Stow;

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
    /// <c>RemoveItem(string, …)</c> returns void and stops quietly when it runs out. A free
    /// craft, with nothing in the log.
    ///
    /// So the point is not a parameter any more. <see cref="HodChests"/> asks this, every
    /// counting and spending path goes through <see cref="HodChests"/>, and a new caller
    /// cannot pass the wrong centre because there is nowhere to pass one.
    ///
    /// <b>The centre is the post, not the player.</b> Hirsla measured from the player because
    /// there was nothing else to measure from - it was a mod, and the sphere had to hang off
    /// the only thing in the world that moves. Here the feature is bought with a piece: a hod
    /// jib standing beside a stowing post, both of which are nailed down. Hanging the sphere
    /// off the post buys three things that a player-centred one cannot have:
    ///
    ///   it does not move      the set of chests being counted is the same at the start of a
    ///                         craft and at the end of it, whatever the player does with WASD
    ///                         in between. A bench that worked a moment ago cannot stop
    ///                         working because somebody took half a step.
    ///   it is visible         the post is a thing you can walk up to and look at, and the
    ///                         jib draws motes to it. "Which chests does this bench see" has
    ///                         an answer you can point at.
    ///   it is bought          the service is the jib's, so it belongs to the jib's post
    ///                         rather than following whoever walks through the room.
    ///
    /// The scope is shut whenever any link in the chain is missing, and there is no fallback
    /// to a player-centred sphere for the cases it cannot cover. A feature that quietly
    /// widens when its own piece is out of reach is a feature nobody can predict.
    /// </summary>
    internal static class HodScope
    {
        /// <summary>
        /// Resolved at most once a frame, and cached on the frame number for the same reason
        /// <see cref="HodChests"/> caches its container list on it: the crafting panel asks
        /// <c>HaveRequirements</c> for every recipe in the game once a frame, each of those
        /// walks its requirements, and each requirement asks <c>CountItems</c> once per
        /// quality level. Resolving per call would be thousands of walks of the upgrade list
        /// a frame, and - much worse - it would let the answer change halfway through a frame
        /// in which a count and a consume both happen.
        ///
        /// -1 rather than 0, because frame 0 is a real frame.
        /// </summary>
        private static int _frame = -1;

        private static StowPost _post;

        /// <summary>
        /// The post whose chests are being counted, or null when the scope is shut.
        ///
        /// A live UnityEngine.Object compared with plain <c>== null</c>, never <c>?.</c> -
        /// Unity overloads <c>==</c> so a destroyed post compares equal to null while the
        /// null-propagating operators sail straight past the overload and hand you a
        /// destroyed component to dereference somewhere unrelated later.
        /// </summary>
        public static StowPost Post
        {
            get
            {
                Resolve();
                return _post;
            }
        }

        /// <summary>Whether a station in reach of a jib-bearing post is being used.</summary>
        public static bool IsOpen
        {
            get { return Post != null; }
        }

        /// <summary>
        /// The centre of every sphere this feature draws.
        ///
        /// Vector3.zero when the scope is shut, which is never used for anything: every
        /// caller is already behind <see cref="IsOpen"/>, either directly or through
        /// <see cref="HodChests.Near"/>, which returns an empty list rather than searching
        /// the world's origin.
        /// </summary>
        public static Vector3 Centre
        {
            get
            {
                var post = Post;
                return post == null ? Vector3.zero : post.transform.position;
            }
        }

        /// <summary>
        /// The four facts that have to line up, asked in the order that makes the common
        /// answer - "no" - cheapest.
        ///
        /// The station is read off the player rather than off InventoryGui, and that matters
        /// for what the feature promises. <c>Player.m_currentStation</c> is set by
        /// <c>Player.SetCraftingStation</c> and is exactly what
        /// <c>Player.HaveRequirementItems</c> and <c>InventoryGui</c> themselves consult when
        /// deciding whether a recipe may be made here - so the scope opens for precisely the
        /// benches the game already considers you to be standing at, and for nothing else. No
        /// station means no scope: a recipe you can make out of your own pack anywhere is not
        /// a recipe a piece of furniture gets to help with.
        /// </summary>
        private static void Resolve()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _post = null;

            if (!HodConfig.Enabled.Value) return;

            // Shut for a reason that is not a setting: another mod asking the same questions
            // of the same chests (HodRuntime.Conflicts), or this build's own world hooks
            // failing to apply, which would leave the count running against a gate that was
            // never built (HodRuntime.LoseWorldHooks). Checked here rather than only at load
            // because this is what the whole feature hangs off, and one branch on a
            // frame-cached path is cheaper than trusting that nothing ever calls in another
            // way.
            if (HodRuntime.Shut) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            var station = player.GetCurrentCraftingStation();
            if (station == null) return;

            // The one call that answers the whole question: the nearest post within Range of
            // the STATION that has a hod jib beside it. PostUpgrade walks the placed upgrades
            // - a handful per loaded zone - and each already knows its own post, so there is
            // no searching inside this.
            _post = PostUpgrades.ServingPost(
                station.transform.position, UpgradeKind.Jib,
                Mathf.Max(0f, HodConfig.Range.Value));
        }

        /// <summary>
        /// Drops the resolved post, so the next question asks again.
        ///
        /// For a world change. The frame counter alone would clear this within a frame
        /// anyway, which is why this is belt and braces rather than the mechanism - but a
        /// StowPost from a world that has been torn down is exactly the kind of reference
        /// that should not be reachable at all, and leaving it costs nothing to correct.
        /// </summary>
        public static void Forget()
        {
            _frame = -1;
            _post = null;
        }
    }
}
