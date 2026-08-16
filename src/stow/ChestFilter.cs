using System.Collections.Generic;

namespace Stow
{
    /// <summary>
    /// What a chest has been told to hold, and how well a given item answers that.
    ///
    /// The rule is stored on the chest's own ZDO, which is the only place it can live and
    /// still be true: it is saved with the world rather than with a config file, it travels
    /// to everyone on a server without any syncing of our own, and a chest that gets torn
    /// down takes its rule with it. A config file keyed by position would have to guess at
    /// all three.
    ///
    /// The format is a plain comma-separated string - "@ore,@bars,Coal" - deliberately
    /// legible, because a ZDO is not a place you can put a breakpoint and the first thing
    /// anyone debugging this will want is to read it.
    /// </summary>
    internal static class ChestFilter
    {
        private const string ZFilter = "stowFilter";

        /// <summary>An entry meaning "anything nothing else wanted".</summary>
        public const string CatchAll = "*";

        public const int TierNone     = -1;
        public const int TierContents = 0;
        public const int TierCatchAll = 1;
        public const int TierGroup    = 2;
        public const int TierItem     = 3;

        public static string Read(Container container)
        {
            var zdo = Zdo(container);
            return zdo == null ? "" : zdo.GetString(ZFilter, "");
        }

        public static void Write(Container container, IEnumerable<string> entries)
        {
            var nview = container == null ? null : container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            // Editing another client's chest is the same problem as taking from it, and
            // gets the same answer the game gives itself: claim it first.
            nview.ClaimOwnership();
            nview.GetZDO().Set(ZFilter, string.Join(",", Clean(entries).ToArray()));
        }

        public static List<string> Entries(Container container)
        {
            return Clean(Read(container).Split(','));
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

        public static bool IsGroup(string entry)
        {
            return entry != null && entry.Length > 1 && entry[0] == '@';
        }

        public static string GroupId(string entry)
        {
            return IsGroup(entry) ? entry.Substring(1) : null;
        }

        /// <summary>
        /// "-Tin" and "-@ore" mean never put this here.
        ///
        /// A leading minus rather than a second ZDO field, so the whole rule stays one
        /// legible string - "@ore,-Tin" says what it means to anyone reading a ZDO dump,
        /// and a chest configured by an older version still parses.
        /// </summary>
        public static bool IsExclusion(string entry)
        {
            return entry != null && entry.Length > 1 && entry[0] == '-';
        }

        /// <summary>The entry with any leading minus taken off.</summary>
        public static string Bare(string entry)
        {
            return IsExclusion(entry) ? entry.Substring(1) : entry;
        }

        /// <summary>A rule read once and then asked about many times, for one stow run.</summary>
        public sealed class Rule
        {
            public Container Container;
            public float Distance;

            public bool Configured;
            public bool CatchesAll;

            /// <summary>Shared names named outright.</summary>
            public readonly HashSet<string> Items = new HashSet<string>();

            public readonly List<ItemGroup> Groups = new List<ItemGroup>();

            /// <summary>Named outright as unwelcome, whatever else says otherwise.</summary>
            public readonly HashSet<string> NotItems = new HashSet<string>();

            public readonly List<ItemGroup> NotGroups = new List<ItemGroup>();

            /// <summary>
            /// How well this chest answers for an item. Higher wins; ties go to the nearer
            /// chest.
            ///
            /// A configured chest is never offered the contents fallback. Saying "this one
            /// holds ore" and then finding nails in it because nails happened to be in
            /// there already would make configuring a chest actively worse than not
            /// configuring it.
            /// </summary>
            public int Match(ItemDrop.ItemData item)
            {
                if (item == null || item.m_shared == null) return TierNone;

                var shared = item.m_shared.m_name;

                // Exclusions are checked before everything, including before the contents
                // fallback. "@ore,-Tin" has to beat the group that contains tin, and
                // "-Tin" on an otherwise unconfigured chest has to beat the fact that
                // there is already tin sitting in it - otherwise the one case you would
                // reach for it in is the one case it does not work.
                if (Excludes(shared)) return TierNone;

                if (Configured)
                {
                    if (Items.Contains(shared)) return TierItem;

                    foreach (var group in Groups)
                        if (group.Members.Contains(shared)) return TierGroup;

                    return CatchesAll ? TierCatchAll : TierNone;
                }

                if (!StowConfig.MatchContents.Value) return TierNone;

                var inventory = Container.GetInventory();
                return inventory != null && inventory.ContainsItemByName(shared)
                    ? TierContents
                    : TierNone;
            }

            /// <summary>
            /// Does this chest actively refuse the item, as opposed to merely not asking
            /// for it? Tidying turns on the difference.
            /// </summary>
            public bool Refuses(ItemDrop.ItemData item)
            {
                return item != null && item.m_shared != null && Excludes(item.m_shared.m_name);
            }

            private bool Excludes(string shared)
            {
                if (NotItems.Contains(shared)) return true;

                foreach (var group in NotGroups)
                    if (group.Members.Contains(shared)) return true;

                return false;
            }
        }

        public static Rule RuleFor(Container container, float distance)
        {
            var rule = new Rule { Container = container, Distance = distance };

            foreach (var entry in Entries(container))
            {
                // An exclusion subtracts; it never configures. A chest whose only rule is
                // "-Tin" still matches on its contents like any unconfigured chest -
                // flipping it to configured would silently switch that fallback off, so
                // saying "not tin" would also mean "and nothing else either".
                var excluded = IsExclusion(entry);
                var bare = Bare(entry);

                if (bare == CatchAll)
                {
                    if (excluded) continue; // "not anything else" is the default already

                    rule.CatchesAll = true;
                    rule.Configured = true;
                    continue;
                }

                if (IsGroup(bare))
                {
                    var group = ItemGroups.Find(GroupId(bare));
                    if (group == null) continue;

                    if (excluded) { rule.NotGroups.Add(group); continue; }

                    rule.Groups.Add(group);
                    rule.Configured = true;
                    continue;
                }

                var shared = ItemGroups.SharedNameOf(bare);
                if (shared == null) continue;

                if (excluded) { rule.NotItems.Add(shared); continue; }

                rule.Items.Add(shared);
                rule.Configured = true;
            }

            return rule;
        }

        /// <summary>
        /// Does a loose list of entries want this item?
        ///
        /// Pulled out for the post's fetch list, which is the same format pointed the
        /// other way - "@ore,-Tin" has to mean the same thing whether a chest is offering
        /// to hold it or a post is asking for it, and two matchers would be two dialects.
        ///
        /// Unlike Rule.Match there is no contents fallback and no tiers: a fetch list is
        /// something you wrote down, so wanting is a yes or a no.
        /// </summary>
        public static bool Matches(List<string> entries, ItemDrop.ItemData item)
        {
            if (entries == null || item == null || item.m_shared == null) return false;

            var shared = item.m_shared.m_name;
            var wanted = false;

            foreach (var entry in entries)
            {
                var excluded = IsExclusion(entry);
                var bare = Bare(entry);

                bool hit;
                if (bare == CatchAll) hit = true;
                else if (IsGroup(bare))
                {
                    var group = ItemGroups.Find(GroupId(bare));
                    hit = group != null && group.Members.Contains(shared);
                }
                else hit = ItemGroups.SharedNameOf(bare) == shared;

                if (!hit) continue;

                // A refusal wins outright, wherever it sits in the list. Same rule as
                // Rule.Match, and for the same reason: "@ore,-Tin" is only useful if the
                // minus beats the group that contains tin.
                if (excluded) return false;

                wanted = true;
            }

            return wanted;
        }

        /// <summary>One line for the hover text, so a chest says what it is without opening it.</summary>
        public static string Summary(Container container)
        {
            var entries = Entries(container);
            if (entries.Count == 0) return null;

            var parts = new List<string>();
            var nots = new List<string>();

            foreach (var entry in entries)
            {
                var excluded = IsExclusion(entry);
                var bare = Bare(entry);

                string label;
                if (bare == CatchAll) label = "anything else";
                else if (IsGroup(bare))
                {
                    var group = ItemGroups.Find(GroupId(bare));
                    label = group != null ? group.Display.ToLowerInvariant() : GroupId(bare);
                }
                else label = ItemGroups.DisplayNameOf(bare);

                (excluded ? nots : parts).Add(label);
            }

            // Exclusions last and gathered under one "not", because they read as a
            // qualifier on what came before rather than as more things the chest holds.
            // Interleaved in rule order, "ore, not tin, bars" invites you to read the
            // negation as applying to the bars.
            var summary = string.Join(", ", parts.ToArray());
            if (nots.Count == 0) return summary;

            var refused = "not " + string.Join(" or ", nots.ToArray());
            return summary.Length == 0 ? refused : summary + " - " + refused;
        }

        private static ZDO Zdo(Container container)
        {
            if (container == null) return null;

            var nview = container.GetComponent<ZNetView>();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }
    }
}
