using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// What one post has been told to do, stored on that post's own ZDO.
    ///
    /// Per post, not per config file, and that was the whole point of asking. Two posts in
    /// two rooms can be set differently, the setting travels to everyone on a server with
    /// no syncing of our own, and a post that gets torn down takes its settings with it. A
    /// config file keyed by position would have to guess at all three - and worse, would
    /// make "tidy" a decision about every post you will ever build rather than about this
    /// one.
    ///
    /// Same shape as ChestFilter, deliberately: the fetch list is the same
    /// comma-separated string a chest's rule uses, so the same grid, the same chips and the
    /// same parser serve both. A post asking for ore and a chest holding ore are the same
    /// sentence pointed in opposite directions.
    ///
    /// **Nothing sets any of this on main.** SetFetch, SetTidy and SetPresence have no
    /// callers: the only thing that ever called them was the post's own panel, which was
    /// shelved to the v1.2-panel branch unproven. So the readers below always answer with
    /// the default, which makes the Fetch, Tidy and Resting errands in CarryRun and the
    /// NextFetch/NextTidy searches in Depositor unreachable code on this branch.
    ///
    /// Left standing rather than stripped, and that is a trade rather than an oversight.
    /// The changelog promises 1.2 is a merge and not a rebuild, and deleting the half that
    /// lives here would make it a rebuild. The cost is that a reader hunting for what sets
    /// a post to LivesHere finds nothing, which is what this paragraph is for.
    /// </summary>
    internal static class PostRules
    {
        private const string ZFetch = "stowFetch";
        private const string ZTidy = "stowTidy";
        private const string ZPresence = "stowPresence";

        /// <summary>Where a spirit is when there is nothing to carry.</summary>
        public enum Presence
        {
            /// <summary>Fades in when there is work and goes out when there is none.</summary>
            OnlyWorking = 0,

            /// <summary>Lives at the heartwood and sleeps there between runs.</summary>
            LivesHere = 1
        }

        // ------------------------------------------------------------------ fetch

        public static List<string> Fetch(Container container)
        {
            return Clean(Read(container, ZFetch));
        }

        public static void SetFetch(Container container, IEnumerable<string> entries)
        {
            Write(container, ZFetch, string.Join(",", Clean(entries).ToArray()));
        }

        /// <summary>
        /// Whether this post wants an item brought to it.
        ///
        /// Reuses ChestFilter's rule machinery rather than a second matcher: a fetch list
        /// is a chest rule, and "@ore,-Tin" has to mean the same thing on both sides or the
        /// mod has two dialects of its own format.
        /// </summary>
        public static bool Wants(Container post, ItemDrop.ItemData item)
        {
            var entries = Fetch(post);
            if (entries.Count == 0) return false;

            return ChestFilter.Matches(entries, item);
        }

        // ------------------------------------------------------------------ switches

        public static bool Tidy(Container container)
        {
            return Read(container, ZTidy) == "1";
        }

        public static void SetTidy(Container container, bool on)
        {
            Write(container, ZTidy, on ? "1" : "");
        }

        public static Presence Where(Container container)
        {
            return Read(container, ZPresence) == "1" ? Presence.LivesHere : Presence.OnlyWorking;
        }

        public static void SetPresence(Container container, Presence presence)
        {
            Write(container, ZPresence, presence == Presence.LivesHere ? "1" : "");
        }

        // ------------------------------------------------------------------ the zdo

        private static string Read(Container container, string key)
        {
            var nview = container == null ? null : container.GetComponent<ZNetView>();
            return nview == null || !nview.IsValid() ? "" : nview.GetZDO().GetString(key, "");
        }

        private static void Write(Container container, string key, string value)
        {
            var nview = container == null ? null : container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            // Same handshake as editing a chest's rule: writing to a ZDO you do not own is
            // silently discarded, so claim it first.
            nview.ClaimOwnership();
            nview.GetZDO().Set(key, value ?? "");
        }

        private static List<string> Clean(IEnumerable<string> entries)
        {
            var cleaned = new List<string>();
            if (entries == null) return cleaned;

            foreach (var raw in entries)
            {
                if (raw == null) continue;

                var entry = raw.Trim();
                if (entry.Length == 0) continue;
                if (!cleaned.Contains(entry)) cleaned.Add(entry);
            }

            return cleaned;
        }

        private static List<string> Clean(string packed)
        {
            return Clean((packed ?? "").Split(','));
        }
    }
}
