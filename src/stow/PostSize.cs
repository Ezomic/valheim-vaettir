// No `using System` here, for the same reason PostUpgrades does without it: this file
// leans on UnityEngine types throughout and importing System makes every bare `Object`
// ambiguous. The one System type used below is named in full.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// How many slots one post has, and what becomes of what is in them when that number
    /// falls.
    ///
    /// The size used to be two config entries read once, in StowPost.Build, and written
    /// onto the prefab - which is exactly as global as it sounds: every post in the world
    /// was the same size because there was only one number. The creel rail makes it a
    /// property of a post rather than of the mod, so it has to be applied to a live
    /// Container instead, and that turns out to be the whole of the difficulty. The rest of
    /// this file is what the game does with a container whose grid changes underneath it,
    /// read out of Container and Inventory rather than guessed:
    ///
    /// - **Container.Awake builds the inventory once**, `new Inventory(m_name, m_bkg,
    ///   m_width, m_height)`, and never looks at those two fields again. So changing them
    ///   on a placed post does nothing by itself; the live Inventory is what has to change.
    /// - **Inventory has SetHeight and no SetWidth.** `m_width` is private with no setter
    ///   at all, which is why there is a reflection reference below and why it is bound
    ///   lazily - a FieldRef bound in a static initialiser throws at type-init when the
    ///   field name is wrong and then poisons every method on the type.
    /// - **Container.UpdateRows overwrites the height on every load.** It computes
    ///   `max(m_height, lowest occupied row + 1)` from the Container field and calls
    ///   SetHeight with it, so a height set on the Inventory alone is quietly reverted the
    ///   next time the ZDO revision changes. Both have to be written, which is why Resize
    ///   sets the Container's fields as well as the Inventory's.
    /// - **The window follows on its own.** InventoryGrid.UpdateGui re-reads GetWidth and
    ///   GetHeight every frame and rebuilds its elements when either changed, and
    ///   InventoryGui.UpdateContainer calls it every frame while a container is open. So a
    ///   post that grows or shrinks with its window open redraws itself with no help.
    /// - **Growing is free and shrinking destroys items.** Inventory.Load re-adds every
    ///   saved item through the private `AddItem(item, amount, x, y, skipValidPositionCheck)`,
    ///   whose bounds test is `x < 0 || y < 0 || x >= m_width || (y >= m_height &&
    ///   !skipValidPositionCheck)`. Load passes skipValidPositionCheck **true**, so a row
    ///   past the bottom survives - Container.UpdateRows then grows the grid back to fit it -
    ///   but a **column past the right-hand edge is rejected outright**, silently, and the
    ///   next save writes the survivors. A post shrunk from eight columns to six with a
    ///   stack sitting in column seven loses it for good, with nothing in any log.
    ///
    /// That last point is the reason this file exists rather than two lines in StowPost.
    ///
    /// <b>The size is remembered on the post's own ZDO, and that is the change that makes
    /// the rest of this safe.</b> The first version of this file guessed instead: every post
    /// opened at the widest a post can ever be and settled down to its real size five
    /// seconds later, on the argument that being too wide is harmless and being too narrow
    /// destroys a column. The argument is sound and the arrangement was still wrong, in
    /// three ways that all have the same root - five seconds of a grid nobody had checked is
    /// five seconds in which somebody can put something in it:
    ///
    ///   - An **unupgraded** post showed two extra columns and a row on every zone load. Drop
    ///     a stack in one and the settle either shuffled it or threw it in the grass, for a
    ///     feature that post did not have.
    ///   - A client that **never owned** the post never shrank it at all - the shrink is
    ///     owner-only - so a guest saw the widest grid for as long as the post stayed loaded.
    ///     Opening a container claims its ZDO, so the shrink then fired a frame or two after
    ///     the window was already on screen, under the cursor, with no settle window left.
    ///   - With `UpgradesEnabled` **off**, where no rail can be built at all, every post in
    ///     the world still did it.
    ///
    /// Two ints on the ZDO answer all three at once. A post opens at the size it was last
    /// saved at - six by two for a plain one, eight by three for a railed one - so its items
    /// load into the grid they were saved from, there is no provisional maximum and no
    /// settle window to be caught in. It is free: the post is already writing this ZDO on
    /// every delivery, and `ZDO.Set` only bumps the revision when the value actually
    /// changes. A post that has never recorded one - built before this existed, or placed a
    /// moment ago - still opens at the widest and records its real size the first time an
    /// owner settles it, so the old behaviour survives exactly once per post and then stops.
    ///
    /// <b>Nothing here ever puts an item on the ground on its own.</b> A shrink that cannot
    /// relocate every homeless stack inside the post is refused outright and the post stays
    /// large until there is room - see <see cref="Fits"/>. The drop path below is still
    /// written and is still vanilla's own, but it is now the backstop for arithmetic being
    /// wrong rather than the plan.
    /// </summary>
    internal static class PostSize
    {
        /// <summary>
        /// Where a post remembers how big it is.
        ///
        /// Two plain ints rather than one packed one, because the next person to read a ZDO
        /// dump should not have to divide by anything. Unknown keys cost a ZDO nothing -
        /// vanilla ignores what it does not recognise, and an older build of Vaettir simply
        /// falls back to guessing - so unlike a prefab name these are not permanent and
        /// dropping them later costs a re-derivation rather than a world of pieces.
        /// </summary>
        private const string WidthKey = "vaettir_stow_w";
        private const string HeightKey = "vaettir_stow_h";

        /// <summary>
        /// How long after a post wakes up before it is allowed to take slots away.
        ///
        /// A backstop now rather than the mechanism. The real questions - have this post's
        /// items arrived, and is the ground an upgrade could be standing on actually in the
        /// world - are both asked exactly, in <see cref="Settled"/> and in
        /// StowPost.NeighbourhoodLoaded. This is what covers the two of them if either
        /// answer ever stops being available, and it costs nothing at all now that a post
        /// spends the wait at its recorded size rather than at its widest.
        /// </summary>
        private const float SettleSeconds = 5f;

        // ------------------------------------------------------------------ the numbers

        /// <summary>
        /// The post as built. Clamped to what a container window can actually draw - the
        /// same 8x4 ceiling StowPost.Build has always clamped to, kept here so the two
        /// cannot drift apart.
        /// </summary>
        public static Vector2i Plain
        {
            get
            {
                return new Vector2i(Mathf.Clamp(StowConfig.PostWidth.Value, 1, 8),
                                    Mathf.Clamp(StowConfig.PostHeight.Value, 1, 4));
            }
        }

        /// <summary>
        /// The post with a creel rail standing beside it.
        ///
        /// Taken as the larger of the two on each axis rather than as written. An upgrade
        /// that could make a post *smaller* because somebody edited one number and not the
        /// other would spill the contents of the extra slots on the floor at the moment it
        /// was built, which is the one thing an upgrade must never do.
        ///
        /// Deliberately NOT gated on `UpgradesEnabled`. Turning that off removes the three
        /// pieces from the hammer and leaves every one already built standing - the config
        /// comment says so, and it has to, because unregistering a prefab discards its ZDOs.
        /// A rail that is still standing is still a rail, so a post that answers "yes, I
        /// have one" must still be given the slots, or switching the build menu off would
        /// shrink every railed post in the world. The reason the gate looked necessary was
        /// that an unupgraded post used to open at this size too; it no longer does.
        /// </summary>
        public static Vector2i Railed
        {
            get
            {
                var plain = Plain;

                return new Vector2i(
                    Mathf.Max(plain.x, Mathf.Clamp(PostUpgrades.RailWidth.Value, 1, 8)),
                    Mathf.Max(plain.y, Mathf.Clamp(PostUpgrades.RailHeight.Value, 1, 4)));
            }
        }

        /// <summary>
        /// The biggest a post can ever be, whatever is standing next to it.
        ///
        /// Only reached now by a post that has never recorded a size - see Open.
        /// </summary>
        public static Vector2i Widest { get { return Railed; } }

        // ------------------------------------------------------------------ applying

        /// <summary>
        /// The size a post opens at, applied before its items arrive.
        ///
        /// Read back rather than guessed. Container.Awake has just built the inventory at
        /// the prefab's own six columns and scheduled `InvokeRepeating("CheckForChanges",
        /// 0f, 1f)`, so the first load of the saved items is imminent, and loading eight
        /// columns of items into a six-column grid deletes two of them permanently. The
        /// post's ZDO already knows which it is, because the last owner to settle it wrote
        /// it there, so this is an exact answer available at the one moment it is needed.
        ///
        /// A post with nothing recorded falls back to the widest, which is the old
        /// behaviour and the safe direction to be wrong in: too wide costs two spare slots
        /// for a few seconds, too narrow costs a stack of black metal that never existed.
        /// That fallback is reached once per post - the first owner to settle it records a
        /// size - and never again.
        /// </summary>
        public static void Open(StowPost post, Container container)
        {
            if (post == null || container == null) return;

            var inventory = container.GetInventory();

            // Null on a placement ghost and on the prefab itself: Container.Awake returns
            // before building one when there is no ZDO.
            if (inventory == null) return;

            var nview = post.GetComponent<ZNetView>();
            var recorded = Recorded(nview);

            Resize(container, inventory, Known(recorded) ? recorded : Widest);
        }

        /// <summary>
        /// Sizes a post to what is standing beside it right now, and deals with whatever
        /// falls outside when that is smaller than what it was.
        ///
        /// Recomputed, never remembered. <paramref name="railed"/> is an answer the post
        /// got from the world a moment ago rather than a flag anybody wrote down, so a rail
        /// torn down shrinks the post it was serving and a world reloaded works it out
        /// again from scratch. The ZDO record is not a second source of truth for that - it
        /// is only where the *result* is left so the next Awake does not have to guess.
        /// </summary>
        public static void Apply(StowPost post, Container container, bool railed, float awoke)
        {
            if (post == null || container == null) return;

            var inventory = container.GetInventory();
            if (inventory == null) return;

            var nview = post.GetComponent<ZNetView>();
            var valid = nview != null && nview.IsValid();
            var owner = valid && nview.IsOwner();

            var recorded = Recorded(nview);
            var known = Known(recorded);
            var settled = Settled(container, awoke);

            Vector2i wanted;

            if (!settled)
            {
                // Hold whatever it opened at. For a post with a record that is its real
                // size and this branch does nothing at all; for one without, it is the
                // widest, and holding it there is the whole point of the wait.
                wanted = known ? recorded : Widest;
            }
            else if (railed)
            {
                // **A rail never takes slots away.** Floored at what the post already is,
                // and that is not belt and braces - it is what stops one client's config
                // emptying everybody's post. Core only imposes the host's values on a client
                // whose own EnforceConfig is true, so a guest can genuinely be carrying
                // RailWidth 6 while the host and the world are on 8. Without this floor that
                // guest walks into the storage room, is handed ownership of a railed post
                // two seconds later, and narrows it for everyone - with the rail still
                // standing and nothing anywhere saying why.
                //
                // The cost is that lowering RailWidth does not take effect while the rail is
                // up. Break the rail and rebuild it and the post comes back at the new
                // number; the config comment says so.
                wanted = known ? Max(Railed, recorded) : Railed;
            }
            else
            {
                wanted = Plain;
            }

            var width = inventory.GetWidth();
            var height = inventory.GetHeight();

            // The state every post is in almost all of the time, and the reason this can be
            // asked every frame without anybody noticing.
            if (width == wanted.x && height == wanted.y)
            {
                // Catch up the record for a post that was already the right size - a
                // freshly placed one, or one whose owner changed between sessions. Only
                // once: ZDO.Set compares before it writes, so a matching value costs a
                // dictionary lookup and never a revision.
                if (settled && owner && !Same(recorded, wanted)) Record(nview, wanted);

                post.ShrinkBlocked(false);
                return;
            }

            // Growing takes nothing away, so it happens everywhere and at once. It matters
            // that it is not owner-gated: a client that never owns the post still has to
            // draw the right grid the moment it opens one, and opening is what claims
            // ownership - so an owner-only grow would arrive a frame after it was needed.
            if (wanted.x >= width && wanted.y >= height)
            {
                Resize(container, inventory, wanted);
                if (owner) Record(nview, wanted);

                post.ShrinkBlocked(false);
                return;
            }

            // -------------------------------------------------------------- shrinking

            // Everything from here down is a guard, and each one of them is a way the post
            // could otherwise have been narrowed on an answer that was not what it looked
            // like.

            // A post that has already failed one of these does not try again. Set only by
            // the branch at the very bottom, which is the "this should not be possible" one,
            // and never cleared for the life of the component: without it the post would
            // resize, fail, put the resize back and do the whole thing again on the next
            // frame, forever.
            if (post.Faulted) return;

            // Wait for the items and for the world. Reachable while unsettled when the post
            // has no record and is therefore sitting at its widest, which is exactly the
            // case the wait exists for.
            if (!settled) return;

            // The owner, and only the owner. A client that truncated its own view of a post
            // it does not own would be holding a copy of the inventory with two columns
            // missing, and the moment it claimed ownership for any reason - opening the
            // window, a delivery landing - the next save would write that copy over the
            // real one. Stow already routes every write through the owner for this reason;
            // this is the same rule applied to a read that can turn into a write.
            if (!owner)
            {
                // And cleared on the way past, because a non-owner has no opinion about
                // whether the post could shrink and should not be showing a hover line that
                // says it has one. Ownership moves every couple of seconds on a busy server.
                post.ShrinkBlocked(false);
                return;
            }

            // Not while somebody has it open. Container.Load early-returns on m_inUse so
            // the write would stick, and the first version of this file used that to argue
            // the open window was the *best* case to do it in. It is safe and it is still
            // unpleasant: the grid changes under a cursor that may be mid-drag, and there is
            // nothing to gain by not waiting. Closing the window is a moment that always
            // comes, and Stow already treats it as the moment a post is done being handled.
            if (container.IsInUse()) return;

            // **Is the ground a rail could be standing on actually in the world?** The
            // absence of a registered PostUpgrade is not the same fact as the absence of a
            // rail, and the commonest reason for the difference has nothing to do with load
            // timing: ZNetScene creates and destroys by sector, and a post and its rail are
            // at most UpgradeRange apart while a zone is 64m across. A pair straddling a
            // zone line therefore has a range of player positions at which the post is
            // instantiated and the rail is not - the rail is destroyed, the post reads
            // "no rail" within half a second, and every guard above is satisfied. Without
            // this the post would narrow itself every time that base was approached from
            // that direction, with the rail still standing five metres away.
            if (!post.NeighbourhoodLoaded()) return;

            // **Nothing is ever thrown on the ground by a resize.** If every homeless stack
            // cannot be found a slot inside the smaller grid, the shrink does not happen at
            // all and the post keeps the size it has until there is room. A post that is too
            // full to shrink is a post with some spare slots; the alternative is a mod that
            // empties a player's storage onto the floor while they are not looking, and no
            // arrangement of the guards above can be trusted enough to be worth that.
            if (!Fits(inventory, wanted))
            {
                post.ShrinkBlocked(true);
                return;
            }

            post.ShrinkBlocked(false);

            // Resize first, spill second, and the order is load-bearing: Spill puts a
            // homeless stack back into the grid, and Inventory.FindEmptySlot searches
            // whatever the current width and height are. Spilling first would "rescue" a
            // stack into a column that is about to stop existing.
            //
            // Both in one frame. InventoryGrid maps an item to a slot by `y * width + x`,
            // so a stack in column seven of a six-wide grid would draw one row down and one
            // column in, on top of whatever is really there. It cannot see that state,
            // because nothing between these two lines yields.
            var before = new Vector2i(width, height);

            Resize(container, inventory, wanted);

            if (Spill(post, container, inventory, wanted) > 0)
            {
                // A stack that is still outside the grid after all that. It should not be
                // possible - Fits said there was room and Spill's own drop path is the
                // backstop for it not being - but the consequence of being wrong is exactly
                // the one this file exists to prevent: an item sitting in a column the next
                // Inventory.Load will reject without a word. So the resize is put back
                // instead. The post keeps the slots, nothing has moved anywhere it cannot
                // be reached, and the error below is the only evidence anybody will get.
                //
                // Reachable in principle through another mod: Inventory.RemoveItem is a
                // Harmony target and MultiUserChest already installs a prefix that forces
                // it to false, which is why Stow's own removal paths check its return value
                // too rather than assuming.
                Resize(container, inventory, before);
                post.ShrinkFaulted();
                return;
            }

            Record(nview, wanted);
        }

        /// <summary>
        /// Writes a size onto both halves of a live container.
        ///
        /// The Inventory's own fields are what the grid, the add paths and the save/load
        /// bounds test all read. The Container's are what Container.UpdateRows uses as the
        /// floor when it recomputes the height on every load - leave those at six and two
        /// and a post grown to three rows quietly loses the third the next time its ZDO
        /// revision changes, which is every time a spirit delivers anything.
        /// </summary>
        private static void Resize(Container container, Inventory inventory, Vector2i size)
        {
            container.m_width = size.x;
            container.m_height = size.y;

            var width = WidthRef();
            if (width != null) width(inventory) = size.x;

            inventory.SetHeight(size.y);
        }

        /// <summary>
        /// Whether every stack that would fall outside <paramref name="size"/> can be given
        /// a slot inside it.
        ///
        /// Deliberately pessimistic: it counts whole slots and ignores the room left in
        /// stacks that are already there, so three loose iron that vanilla would have merged
        /// into an existing stack are counted as needing three slots. Being wrong this way
        /// refuses a shrink that would have worked, and the post stays two columns wider
        /// than it needs to be until something is taken out of it - which nobody is harmed
        /// by. Being wrong the other way is the one outcome this whole file exists to
        /// prevent, so the arithmetic is kept simple enough to read in one pass rather than
        /// exact.
        ///
        /// What makes it sufficient: Inventory.AddItem tops up matching stacks first and
        /// only then calls FindEmptySlot, so a stray that is offered at least one free slot
        /// inside the grid is always placed. Spill removes each stray before it re-adds it,
        /// so the strays still waiting occupy positions outside the grid that FindEmptySlot
        /// never looks at.
        /// </summary>
        private static bool Fits(Inventory inventory, Vector2i size)
        {
            var strays = 0;
            var inside = 0;

            foreach (var item in inventory.GetAllItems())
            {
                if (item == null) continue;

                if (item.m_gridPos.x < size.x && item.m_gridPos.y < size.y) inside++;
                else strays++;
            }

            if (strays == 0) return true;

            return strays <= size.x * size.y - inside;
        }

        /// <summary>
        /// Gets everything out of the slots that no longer exist.
        ///
        /// Only ever called after <see cref="Fits"/> has said there is room for all of it,
        /// so in practice this relocates and never drops. The drop path is kept all the same
        /// and is vanilla's own - **ItemDrop.DropItem(item, 0, position, rotation)**, the
        /// exact call Container.DropAllItems makes when a chest is destroyed, same scatter,
        /// same random yaw, same 0 meaning "the whole stack" - because the alternative to
        /// dropping a stack that unexpectedly would not fit is destroying it. It is the
        /// backstop for the arithmetic being wrong, not the plan, and if it ever fires it
        /// says so at error level.
        ///
        /// The removal comes first on purpose. Inventory.AddItem tops up matching stacks
        /// before it looks for an empty slot, and a stack still in the list would be found
        /// by that search and merged with itself.
        ///
        /// A stack the player happens to be dragging needs no handling here, which is worth
        /// writing down because it looks like it should: InventoryGui checks
        /// `m_dragInventory.ContainsItem(m_dragItem)` before acting on a release, in both
        /// the drop-on-the-world path and the drop-on-a-grid path, and cancels the drag when
        /// the answer is no. (The window is held off entirely by Apply now, so this is a
        /// second line rather than the first.)
        ///
        /// <b>One save, not two per stack.</b> Every RemoveItem and every AddItem ends in
        /// Inventory.Changed, which Container.Awake wired to OnContainerChanged, which
        /// serialises the whole inventory into a fresh ZPackage and writes it to the ZDO -
        /// on the owner, which is the only machine that reaches this. A full railed post
        /// losing twelve stacks' worth of slots therefore did twenty-four whole-inventory
        /// serialisations and twenty-four ZDO writes inside one frame, for what is logically
        /// one change. Container.m_loading is the flag OnContainerChanged already honours
        /// (`if (!m_loading &amp;&amp; IsOwner()) Save()`), so the loop runs behind it and one
        /// final Changed does the single write at the end. If the reflection does not bind
        /// the flag stays false and the old behaviour returns: correct and slow beats clever
        /// and unbound.
        /// </summary>
        /// <returns>
        /// How many stacks are still sitting outside <paramref name="size"/> when it is
        /// done - zero on every path that is supposed to be reachable. The caller puts the
        /// resize back rather than leave one in a column the next load would reject.
        /// </returns>
        private static int Spill(StowPost post, Container container, Inventory inventory,
                                 Vector2i size)
        {
            List<ItemDrop.ItemData> strays = null;

            foreach (var item in inventory.GetAllItems())
            {
                if (item == null) continue;
                if (item.m_gridPos.x < size.x && item.m_gridPos.y < size.y) continue;

                if (strays == null) strays = new List<ItemDrop.ItemData>();
                strays.Add(item);
            }

            if (strays == null) return 0;

            var stuck = 0;
            var rescued = 0;
            var dropped = 0;

            var quiet = Quiet(container, true);

            try
            {
                foreach (var item in strays)
                {
                    // Checked rather than assumed. Inventory.RemoveItem is a Harmony target
                    // and at least one storage mod forces it to false; a stack left in the
                    // list at a position outside the grid is one the next Inventory.Load
                    // throws away, so the count goes back to the caller instead.
                    if (!inventory.RemoveItem(item)) { stuck++; continue; }

                    // True when the stack was placed in a free slot *or* absorbed entirely
                    // into stacks that were already there. False leaves the remainder on the
                    // item, which is exactly what wants dropping.
                    if (inventory.AddItem(item)) { rescued++; continue; }

                    if (item.m_dropPrefab == null)
                    {
                        // Nothing can be instantiated without it, so there is no honest way
                        // to hand this back. Said loudly because it is the one branch in
                        // this file that does lose something.
                        StowRuntime.Log.LogError(
                            "A stowing post shrank with " + item.m_stack + "x "
                            + item.m_shared.m_name + " in a slot that no longer exists, and "
                            + "the item has no drop prefab to put it in the world with. It "
                            + "is gone - report it.");
                        continue;
                    }

                    // Container.DropAllItems' own scatter, copied rather than invented so a
                    // spill looks like the spill every player has already seen.
                    var position = post.transform.position + Vector3.up * 0.5f
                                   + Random.insideUnitSphere * 0.3f;
                    var rotation = Quaternion.Euler(0f, Random.Range(0, 360), 0f);

                    ItemDrop.DropItem(item, 0, position, rotation);
                    dropped++;
                }
            }
            finally
            {
                // In a finally because leaving m_loading set would mean the post stopped
                // saving for the rest of its life - every delivery after it discarded
                // silently, which is the worst kind of quiet.
                Quiet(container, quiet);
            }

            // The one write. Inventory.Changed is private, but the only part of it the loop
            // actually suppressed is reachable and public: m_onChanged is the Action
            // Container.Awake hung OnContainerChanged on, and Changed's other two jobs -
            // UpdateTotalWeight and the cheated-item popup - ran on every RemoveItem and
            // AddItem above regardless, because m_loading gates Container's handler rather
            // than Inventory's notification. So this is precisely the save that was held
            // back, delivered once.
            if (inventory.m_onChanged != null) inventory.m_onChanged();

            if (dropped > 0)
            {
                StowRuntime.Log.LogError(
                    "A stowing post shrank and " + dropped + " stack(s) would not fit "
                    + "anywhere inside it, so they were dropped at its feet. Nothing should "
                    + "reach this - the shrink is refused when it cannot relocate everything "
                    + "- so please report it.");

                if (StowConfig.Messages.Value && Player.m_localPlayer != null)
                    Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                        Localization.instance.Localize(
                            "The post shrank - " + dropped + " stack"
                            + (dropped == 1 ? "" : "s") + " dropped at its feet."), 0, null);
            }
            else
            {
                StowRuntime.Log.LogInfo("Stowing post gave up " + strays.Count + " slot"
                    + (strays.Count == 1 ? "" : "s") + " with something in "
                    + (strays.Count == 1 ? "it" : "them") + "; " + rescued
                    + " moved to a slot that still exists.");
            }

            return stuck;
        }

        // ------------------------------------------------------------------ the world

        /// <summary>
        /// Whether a shrink can be believed yet.
        ///
        /// The item race, which is the one that ends with a column deleted. A post's
        /// inventory is empty between Container.Awake and the first CheckForChanges, so a
        /// post shrunk in that window looks like it has nothing to relocate - and then its
        /// saved items are loaded against the smaller grid, which is where Inventory's own
        /// bounds test throws away everything past the new right-hand edge.
        /// Container.m_lastRevision answers this exactly: it is uint.MaxValue until Load or
        /// Save has run once, and nothing else writes it.
        ///
        /// The neighbour race used to be in here too, as a five-second timer standing in for
        /// "has the zone finished instantiating". It has moved to
        /// StowPost.NeighbourhoodLoaded, which asks the game rather than guessing, and
        /// answers a second question the timer never could - whether a rail could be
        /// standing somewhere that is not loaded at all. The timer stays as the backstop for
        /// both.
        /// </summary>
        private static bool Settled(Container container, float awoke)
        {
            if (Time.time - awoke < SettleSeconds) return false;

            var revision = RevisionRef();
            if (revision == null) return true;   // timer only; it is the longer of the two

            return revision(container) != uint.MaxValue;
        }

        // ------------------------------------------------------------------ the record

        private static bool Known(Vector2i size) { return size.x > 0 && size.y > 0; }

        private static bool Same(Vector2i a, Vector2i b) { return a.x == b.x && a.y == b.y; }

        private static Vector2i Max(Vector2i a, Vector2i b)
        {
            return new Vector2i(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>
        /// The size this post was last settled at, or (0,0) when it has never recorded one.
        ///
        /// Zero is the sentinel because no post is ever zero slots wide, so there is no
        /// value to confuse it with and no second key needed to say "yes, really".
        /// </summary>
        private static Vector2i Recorded(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return new Vector2i(0, 0);

            var zdo = nview.GetZDO();
            if (zdo == null) return new Vector2i(0, 0);

            return new Vector2i(Mathf.Clamp(zdo.GetInt(WidthKey, 0), 0, 8),
                                Mathf.Clamp(zdo.GetInt(HeightKey, 0), 0, 4));
        }

        /// <summary>
        /// Leaves the size where the next Awake will find it. Owner only - a write to a ZDO
        /// this client does not own is discarded without a word, so doing it anywhere else
        /// would be a line of code that looked like it worked.
        /// </summary>
        private static void Record(ZNetView nview, Vector2i size)
        {
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            var zdo = nview.GetZDO();
            if (zdo == null) return;

            // ZDO.Set compares before it writes and only bumps DataRevision on a real
            // change, so calling this with the value it already holds costs two dictionary
            // lookups and sends nothing.
            zdo.Set(WidthKey, size.x);
            zdo.Set(HeightKey, size.y);
        }

        // ------------------------------------------------------------------ reflection

        // All three bound on first use inside a try/catch rather than in a field
        // initialiser. AccessTools.FieldRefAccess throws when the field name or the owner
        // type is wrong, and a throw from a static initialiser is a TypeInitializationException
        // on every later call to anything on this type - which would present as the post
        // losing its inventory entirely rather than as a missing field. A failed binding
        // costs the one feature it belongs to and nothing else.

        private static AccessTools.FieldRef<Inventory, int> _width;
        private static bool _widthBound;

        private static AccessTools.FieldRef<Inventory, int> WidthRef()
        {
            if (_widthBound) return _width;
            _widthBound = true;

            try
            {
                _width = AccessTools.FieldRefAccess<Inventory, int>("m_width");
            }
            catch (System.Exception e)
            {
                // Height still works - Inventory.SetHeight is public - so the rail keeps
                // half its effect rather than none, and this says which half is missing.
                StowRuntime.Log.LogWarning(
                    "Inventory.m_width could not be reached (" + e.Message + "), so a creel "
                    + "rail can add rows but not columns. Nothing is lost by it.");
            }

            return _width;
        }

        private static AccessTools.FieldRef<Container, uint> _revision;
        private static bool _revisionBound;

        private static AccessTools.FieldRef<Container, uint> RevisionRef()
        {
            if (_revisionBound) return _revision;
            _revisionBound = true;

            try
            {
                _revision = AccessTools.FieldRefAccess<Container, uint>("m_lastRevision");
            }
            catch (System.Exception e)
            {
                StowRuntime.Log.LogWarning(
                    "Container.m_lastRevision could not be reached (" + e.Message + "), so a "
                    + "post waits on its timer alone before giving up a slot.");
            }

            return _revision;
        }

        private static AccessTools.FieldRef<Container, bool> _loading;
        private static bool _loadingBound;

        /// <summary>
        /// Sets Container.m_loading and returns what it was, so a caller can put it back.
        ///
        /// Returns false when the field cannot be reached, which reads as "it was already
        /// off" and leaves the caller's finally harmless - the saves simply happen the way
        /// they used to.
        /// </summary>
        private static bool Quiet(Container container, bool quiet)
        {
            if (_loadingBound == false)
            {
                _loadingBound = true;

                try
                {
                    _loading = AccessTools.FieldRefAccess<Container, bool>("m_loading");
                }
                catch (System.Exception e)
                {
                    StowRuntime.Log.LogWarning(
                        "Container.m_loading could not be reached (" + e.Message + "), so a "
                        + "post that gives up slots saves itself once per stack moved "
                        + "instead of once. Nothing is lost by it.");
                }
            }

            if (_loading == null || container == null) return false;

            var previous = _loading(container);
            _loading(container) = quiet;
            return previous;
        }
    }
}
