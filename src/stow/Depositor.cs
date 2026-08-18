using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// Emptying one inventory into the chests that asked for its contents.
    ///
    /// The resolution is deliberately per-item rather than per-chest. Walking the chests
    /// and asking each "what of this do you want" is the obvious loop and it is the wrong
    /// one - whichever chest is asked first gets first refusal, so the answer depends on
    /// the order chests happen to be found in. Asking each *item* which chest wants it
    /// most makes the outcome a property of the rules instead of of the search.
    ///
    /// Two callers, one core: a stowing post emptying itself, and the optional keybind
    /// emptying your pack. The post is the real one; the key is off by default.
    /// </summary>
    internal static class Depositor
    {
        private static readonly Collider[] Hits = new Collider[256];

        private sealed class Plan
        {
            public ItemDrop.ItemData Item;
            public int Tier;
        }

        /// <summary>
        /// The core. Everything in <paramref name="source"/> that some chest near
        /// <paramref name="origin"/> wants, goes there. Returns how many units moved.
        /// </summary>
        public static int Distribute(Inventory source, Vector3 origin, out int chestCount,
                                     System.Predicate<ItemDrop.ItemData> allow = null)
        {
            chestCount = 0;
            if (source == null || !ItemGroups.Ready) return 0;

            var rules = new List<ChestFilter.Rule>();
            CollectChests(origin, rules);
            if (rules.Count == 0) return 0;

            var plans = new List<Plan>();

            foreach (var item in new List<ItemDrop.ItemData>(source.GetAllItems()))
            {
                if (allow != null && !allow(item)) continue;

                var tier = BestTier(rules, item);
                if (tier == ChestFilter.TierNone) continue;

                plans.Add(new Plan { Item = item, Tier = tier });
            }

            if (plans.Count == 0) return 0;

            // Most specific first, so the chest that actually asked for a thing is filled
            // before a catch-all eats the shelf space it needed.
            plans.Sort((a, b) => b.Tier.CompareTo(a.Tier));

            var moved = 0;
            var touched = new List<Container>();

            foreach (var plan in plans)
                moved += Place(source, rules, plan.Item, touched);

            foreach (var container in touched)
                if (InventoryGui.instance != null && InventoryGui.instance.m_moveItemEffects != null)
                    InventoryGui.instance.m_moveItemEffects.Create(
                        container.transform.position, Quaternion.identity);

            chestCount = touched.Count;
            return moved;
        }

        /// <summary>The optional keybind: empty your pack from where you stand.</summary>
        public static void Run(Player player)
        {
            if (player == null) return;

            if (!ItemGroups.Ready) { Announce("Item catalogue not loaded yet."); return; }

            // Equipped gear, the hotbar and the never-stow list are player concerns with
            // no meaning for a post, so they are passed in as a filter rather than built
            // into the core. An earlier version lifted the protected items out of the
            // inventory and put them back afterwards, which would have dropped your
            // equipped axe on the floor the first time its slot had been taken.
            int chests;
            var moved = Distribute(player.GetInventory(), player.transform.position, out chests,
                                   item => Stowable(player, item));

            if (moved == 0) { Announce("Nothing here belongs in those chests."); return; }

            Announce("Stowed " + moved + " item" + (moved == 1 ? "" : "s")
                     + " into " + chests + " chest" + (chests == 1 ? "" : "s") + ".");
        }

        /// <summary>
        /// Move one stack, spilling into the next-best chest when the first one fills up.
        ///
        /// The chest is re-chosen on every pass rather than once up front, because the run
        /// changes the world as it goes: a chest that had room when the plan was drawn may
        /// have been filled by an earlier item in the same run.
        /// </summary>
        private static int Place(Inventory source, List<ChestFilter.Rule> rules,
                                 ItemDrop.ItemData item, List<Container> touched)
        {
            var moved = 0;

            while (item.m_stack > 0)
            {
                var target = BestChestWithRoom(rules, item);
                if (target == null) break;

                bool emptied;
                var went = Deposit(source, target, item, out emptied);
                if (went < 0) break;

                moved += went;

                // Only chests that actually received something count towards the tally or
                // get the little put-away effect played over them.
                if (went > 0 && !touched.Contains(target)) touched.Add(target);

                if (emptied) break;
                if (went == 0) break; // nothing fit; stop retrying the same chest
            }

            return moved;
        }

        /// <summary>
        /// One stack into one named chest. Returns units moved, or -1 if the chest could
        /// not be written to at all.
        ///
        /// Split out of Place because the carrier needs exactly this and nothing else: a
        /// trip has already decided where it is going by the time it gets there, so the
        /// re-choosing loop above would be re-answering a question that was asked when the
        /// spirit picked the stack up. Both callers share the write itself, which is the
        /// part with the ownership handshake in it and the part worth having one copy of.
        /// </summary>
        public static int Deposit(Inventory source, Container target,
                                  ItemDrop.ItemData item, out bool emptied)
        {
            emptied = false;

            if (source == null || target == null || item == null) return -1;

            var nview = target.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return -1;

            var inventory = target.GetInventory();
            if (inventory == null) return -1;

            // Same handshake the game does for Take All: own it, then write to it.
            // Without this the write lands on a copy the owner will overwrite.
            nview.ClaimOwnership();

            // How much of the stack this trip is allowed to carry. A spirit used to take
            // whatever was in the slot, so a full stack of fifty wood crossed the room as
            // easily as one, and the size of a load was invisible.
            //
            // Clone rather than move-and-put-back. The item in the post is the real one
            // and the safety rule is that it never leaves until a trip lands, so what goes
            // into AddItem is a copy carrying the trip's share; the original is decremented
            // by however much of that copy the chest actually took. A chest that offers
            // room and then takes none leaves both untouched.
            var cap = StowConfig.ItemsPerTrip.Value;
            var take = cap > 0 ? Mathf.Min(cap, item.m_stack) : item.m_stack;

            var load = item.Clone();
            load.m_stack = take;

            var placed = inventory.AddItem(load);

            // AddItem tops up existing stacks first and only then gives up, so a false
            // return still means the remainder is on the item it was handed.
            var went = placed ? take : take - load.m_stack;

            item.m_stack -= went;

            if (item.m_stack <= 0)
            {
                source.RemoveItem(item);
                emptied = true;
            }

            if (went > 0 && StowConfig.Verbose.Value)
                StowRuntime.Log.LogInfo("stowed " + went + "x " + item.m_shared.m_name
                                       + " into " + target.m_name);

            return went;
        }

        /// <summary>
        /// A stack in a nearby chest that this post has asked for, and the chest holding
        /// it. False when nothing in range is wanted.
        ///
        /// The mirror image of NextTrip: that asks "who wants what is in the post", this
        /// asks "who has what the post wants". Both walk the same chests through the same
        /// Usable gate, so a warded or occupied chest is as invisible to fetching as it is
        /// to stowing.
        /// </summary>
        public static bool NextFetch(Container post, Vector3 origin,
                                     List<ItemDrop.ItemData> reserved,
                                     out ItemDrop.ItemData item, out Container source)
        {
            item = null;
            source = null;

            if (post == null || !ItemGroups.Ready) return false;

            var wanted = PostRules.Fetch(post);
            if (wanted.Count == 0) return false;

            var into = post.GetInventory();
            if (into == null) return false;

            var rules = new List<ChestFilter.Rule>();
            CollectChests(origin, rules);

            foreach (var rule in rules)
            {
                var inventory = rule.Container.GetInventory();
                if (inventory == null) continue;

                foreach (var candidate in inventory.GetAllItems())
                {
                    if (reserved != null && reserved.Contains(candidate)) continue;
                    if (!ChestFilter.Matches(wanted, candidate)) continue;

                    // Asked before the trip as well as on arrival. Flying a spirit to a
                    // chest to fetch something the post has no room for is a round trip
                    // that was always going to end in nothing.
                    if (!into.CanAddItem(candidate)) continue;

                    item = candidate;
                    source = rule.Container;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A stack sitting in a chest whose rule refuses it, and where it belongs.
        ///
        /// Tidying only ever corrects a mistake: the item has to be actively unwelcome
        /// where it is *and* have somewhere better to go. That double test is what stops
        /// it churning a room that is already right - an item nobody refuses is left
        /// exactly where it is, however untidy it looks to anyone else.
        /// </summary>
        public static bool NextTidy(Vector3 origin, List<ItemDrop.ItemData> reserved,
                                    out ItemDrop.ItemData item, out Container source,
                                    out Container target)
        {
            item = null;
            source = null;
            target = null;

            if (!ItemGroups.Ready) return false;

            var rules = new List<ChestFilter.Rule>();
            CollectChests(origin, rules);
            if (rules.Count < 2) return false;

            foreach (var rule in rules)
            {
                var inventory = rule.Container.GetInventory();
                if (inventory == null) continue;

                foreach (var candidate in inventory.GetAllItems())
                {
                    if (reserved != null && reserved.Contains(candidate)) continue;

                    // Refused where it is - not merely unmatched. A chest with no rule
                    // refuses nothing, so an unconfigured chest is never tidied out.
                    if (rule.Match(candidate) != ChestFilter.TierNone) continue;
                    if (!rule.Refuses(candidate)) continue;

                    var home = BestChestWithRoom(rules, candidate);
                    if (home == null || home == rule.Container) continue;

                    item = candidate;
                    source = rule.Container;
                    target = home;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The next stack worth a trip, and where it is going. Null when there is nothing
        /// left that any chest wants.
        ///
        /// Rebuilt from scratch on every call rather than planned once at the start of a
        /// run. A run now takes a minute rather than a frame, and in that minute a chest
        /// can fill up, be torn down, be warded, or be opened by somebody else - so a plan
        /// drawn at the start would be a list of decisions made against a world that has
        /// since moved. The scan is one OverlapSphere per trip, which is the same cost the
        /// instant version paid per press.
        /// </summary>
        public static bool NextTrip(Inventory source, Vector3 origin,
                                    List<ItemDrop.ItemData> reserved,
                                    out ItemDrop.ItemData item, out Container target)
        {
            item = null;
            target = null;

            if (source == null || !ItemGroups.Ready) return false;

            var rules = new List<ChestFilter.Rule>();
            CollectChests(origin, rules);
            if (rules.Count == 0) return false;

            var plans = new List<Plan>();

            foreach (var candidate in source.GetAllItems())
            {
                // Already in the air on another spirit's trip. The item is still sitting
                // in the post - that is the whole safety story - so without this it would
                // be picked up again by the next courier and delivered twice, and the
                // second delivery would find it gone and count nothing.
                if (reserved != null && reserved.Contains(candidate)) continue;

                var tier = BestTier(rules, candidate);
                if (tier == ChestFilter.TierNone) continue;

                plans.Add(new Plan { Item = candidate, Tier = tier });
            }

            if (plans.Count == 0) return false;

            // Most specific first, same as the instant path: the chest that actually
            // asked for a thing is filled before a catch-all eats the shelf space it
            // needed. With one stack per trip this also decides what you watch move
            // first, which is worth having be the deliberate answer rather than grid order.
            plans.Sort((a, b) => b.Tier.CompareTo(a.Tier));

            foreach (var plan in plans)
            {
                var chest = BestChestWithRoom(rules, plan.Item);
                if (chest == null) continue;

                item = plan.Item;
                target = chest;
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ choosing

        private static int BestTier(List<ChestFilter.Rule> rules, ItemDrop.ItemData item)
        {
            var best = ChestFilter.TierNone;

            foreach (var rule in rules)
            {
                var tier = rule.Match(item);
                if (tier > best) best = tier;
            }

            return best;
        }

        /// <summary>Where a single item would go right now, or null. Also drives the preview.</summary>
        public static Container BestChestWithRoom(List<ChestFilter.Rule> rules,
                                                  ItemDrop.ItemData item)
        {
            ChestFilter.Rule best = null;
            var bestTier = ChestFilter.TierNone;

            foreach (var rule in rules)
            {
                var tier = rule.Match(item);
                if (tier == ChestFilter.TierNone) continue;

                if (tier < bestTier) continue;
                if (tier == bestTier && best != null && rule.Distance >= best.Distance) continue;

                var inventory = rule.Container.GetInventory();
                if (inventory == null || !inventory.CanAddItem(item, 1)) continue;

                best = rule;
                bestTier = tier;
            }

            return best == null ? null : best.Container;
        }

        // ------------------------------------------------------------------ gathering

        public static void CollectChests(Vector3 point, List<ChestFilter.Rule> into)
        {
            into.Clear();

            var range = StowConfig.Range.Value;
            var count = Physics.OverlapSphereNonAlloc(point, range, Hits);

            var seen = new List<Container>();

            for (var i = 0; i < count; i++)
            {
                var container = Hits[i].GetComponentInParent<Container>();
                if (container == null || seen.Contains(container)) continue;
                seen.Add(container);

                if (!Usable(container, point, range)) continue;

                into.Add(ChestFilter.RuleFor(container,
                    Vector3.Distance(container.transform.position, point)));
            }
        }

        /// <summary>
        /// Everything that would stop you opening the chest by hand also stops this.
        ///
        /// A hotkey that quietly ignores a ward or a privacy lock would be a duplication
        /// exploit, not a convenience.
        /// </summary>
        public static bool Usable(Container container, Vector3 point, float range)
        {
            var nview = container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return false;

            // A post is a sorting table, not a destination. Without this the nearest post
            // would happily accept its own contents back and the run would do nothing.
            if (StowPost.Is(container)) return false;

            if (Vector3.Distance(container.transform.position, point) > range) return false;

            // A chest on a cart or a ship moves; stowing into it from across the yard
            // would be putting things somewhere you did not mean them to go.
            if (container.GetComponentInParent<Vagon>() != null) return false;
            if (container.GetComponentInParent<Ship>() != null) return false;

            if (container.m_privacy != Container.PrivacySetting.Public) return false;
            if (container.IsInUse()) return false;

            if (container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position))
                return false;

            return true;
        }

        private static bool Stowable(Player player, ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_equipped || player.IsItemEquiped(item)) return false;

            if (StowConfig.KeepHotbar.Value && item.m_gridPos.y == 0) return false;

            var prefab = ItemGroups.PrefabNameOf(item);
            if (prefab != null && NeverStow().Contains(prefab)) return false;

            return true;
        }

        private static HashSet<string> _neverStow;
        private static string _neverStowRaw;

        private static HashSet<string> NeverStow()
        {
            var raw = StowConfig.NeverStow.Value ?? "";
            if (_neverStow != null && raw == _neverStowRaw) return _neverStow;

            _neverStowRaw = raw;
            _neverStow = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var name in raw.Split(','))
            {
                var trimmed = name.Trim();
                if (trimmed.Length > 0) _neverStow.Add(trimmed);
            }

            return _neverStow;
        }

        private static void Announce(string message)
        {
            if (!StowConfig.Messages.Value || Player.m_localPlayer == null) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(message), 0, null);
        }
    }
}
