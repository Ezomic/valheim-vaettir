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
    /// The awkward case is a rail torn down while somebody has the post open, and it lands
    /// on the one client that is guaranteed to be able to handle it: opening a container
    /// claims its ZDO, so the player looking at the grid IS the owner, Container.Load is
    /// held off entirely while m_inUse is true, and the write this file makes is the one
    /// that gets saved. The resize and the spill happen in the same frame so the window is
    /// never shown a grid with something outside it, and a stack the player happens to be
    /// dragging at that moment is safe because InventoryGui already checks
    /// `m_dragInventory.ContainsItem(m_dragItem)` on release and cancels a drag whose item
    /// has gone.
    /// </summary>
    internal static class PostSize
    {
        /// <summary>
        /// How long after a post wakes up before it is allowed to take slots away.
        ///
        /// Deliberately generous, because the two things it is waiting for are both
        /// invisible and the cost of waiting is nothing at all. A post one column too wide
        /// for five seconds is a post with two spare slots; a post one column too narrow
        /// for one frame is a stack of black metal that never existed.
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
        /// Used at Awake, before anything in the world is known - see Open.
        /// </summary>
        public static Vector2i Widest { get { return Railed; } }

        // ------------------------------------------------------------------ applying

        /// <summary>
        /// The size a post opens at, applied before its items arrive.
        ///
        /// Always the widest, and that is not an optimisation - it is the only order that
        /// cannot lose anything. Container.Awake has just built the inventory at the
        /// prefab's own six columns and scheduled `InvokeRepeating("CheckForChanges", 0f,
        /// 1f)`, so the first load of the saved items is imminent and this code has no way
        /// yet to know whether there is a rail outside: the pieces in a zone are
        /// instantiated over several frames and a railed post looks exactly like a plain one
        /// until the rail's own Awake has run. Loading eight columns of items into a
        /// six-column grid deletes two of them, permanently. Loading six columns of items
        /// into an eight-column grid costs nothing and Apply settles it back down a moment
        /// later, with the items present and the spill path available if any of them are in
        /// the way.
        ///
        /// Wrong in the safe direction for a few seconds, in other words, rather than right
        /// most of the time and silently destructive the rest.
        /// </summary>
        public static void Open(Container container)
        {
            if (container == null) return;

            var inventory = container.GetInventory();

            // Null on a placement ghost and on the prefab itself: Container.Awake returns
            // before building one when there is no ZDO.
            if (inventory == null) return;

            Resize(container, inventory, Widest);
        }

        /// <summary>
        /// Sizes a post to what is standing beside it right now, and deals with whatever
        /// falls outside when that is smaller than what it was.
        ///
        /// Recomputed, never remembered. <paramref name="railed"/> is an answer the post
        /// got from the world a moment ago rather than a flag anybody wrote down, so a rail
        /// torn down shrinks the post it was serving and a world reloaded works it out
        /// again from scratch.
        /// </summary>
        public static void Apply(StowPost post, Container container, bool railed, float awoke)
        {
            if (post == null || container == null) return;

            var inventory = container.GetInventory();
            if (inventory == null) return;

            var settled = Settled(container, awoke);

            // Held at its widest until the world is worth believing, which is the same
            // argument Open makes and is repeated here rather than left to Open alone:
            // Awake order between two components on one object is not something to bet an
            // inventory on, and if Container.Awake had not yet built this one when StowPost
            // woke up then this is the only place the post is ever made wide enough before
            // its items arrive.
            var wanted = settled ? (railed ? Railed : Plain) : Widest;

            var width = inventory.GetWidth();
            var height = inventory.GetHeight();

            // The state every post is in almost all of the time, and the reason this can be
            // asked twice a second without anybody noticing.
            if (width == wanted.x && height == wanted.y) return;

            // Growing takes nothing away, so it happens everywhere and at once. It matters
            // that it is not owner-gated: a client that never owns the post still has to
            // draw the right grid the moment it opens one, and opening is what claims
            // ownership - so an owner-only grow would arrive a frame after it was needed.
            if (wanted.x >= width && wanted.y >= height)
            {
                Resize(container, inventory, wanted);
                return;
            }

            // Reachable while unsettled only when somebody has edited RailWidth downwards
            // between sessions, so the post is currently wider than anything it is allowed
            // to be. That still waits - it is a shrink, and shrinks wait.
            if (!settled) return;

            // The owner, and only the owner. A client that truncated its own view of a post
            // it does not own would be holding a copy of the inventory with two columns
            // missing, and the moment it claimed ownership for any reason - opening the
            // window, a delivery landing - the next save would write that copy over the
            // real one. Stow already routes every write through the owner for this reason;
            // this is the same rule applied to a read that can turn into a write.
            var nview = post.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            // Resize first, spill second, and the order is load-bearing: Spill puts a
            // homeless stack back into the grid if there is room, and Inventory.FindEmptySlot
            // searches whatever the current width and height are. Spilling first would
            // "rescue" a stack into a column that is about to stop existing.
            //
            // Both in one frame, which is what makes the window-open case work rather than
            // merely survive. A player watching the post while somebody tears the rail down
            // sees the grid lose two columns and a row and the stacks that were in them move
            // or fall, all at once; the UI is never shown the half-state where the grid has
            // shrunk and an item is still sitting outside it. InventoryGrid would draw that
            // state wrong if it ever saw it - it maps an item to a slot by `y * width + x`,
            // so a stack in column seven of a six-wide grid appears one row down and one
            // column in, on top of whatever is really there. It cannot see it, because
            // nothing between these two lines yields.
            Resize(container, inventory, wanted);
            Spill(post, inventory, wanted);
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
        /// Gets everything out of the slots that no longer exist.
        ///
        /// Two answers, in order of how much a player would thank you for them. A stack
        /// that still fits somewhere in the smaller grid is simply moved there - the post
        /// is a piece of furniture whose entire purpose is moving things about, and
        /// throwing a stack of iron on the floor while there is an empty slot a column to
        /// the left would be absurd. Only what genuinely does not fit is dropped, and it is
        /// dropped through **ItemDrop.DropItem(item, 0, position, rotation)**, which is the
        /// exact call Container.DropAllItems makes when a chest is destroyed - same
        /// scatter, same random yaw, same 0 meaning "the whole stack". A player who has
        /// broken a chest before already knows what just happened and where to look.
        ///
        /// The removal comes first on purpose. Inventory.AddItem tops up matching stacks
        /// before it looks for an empty slot, and a stack still in the list would be found
        /// by that search and merged with itself.
        ///
        /// A stack the player happens to be dragging at that moment needs no handling here,
        /// which is worth writing down because it looks like it should: InventoryGui checks
        /// `m_dragInventory.ContainsItem(m_dragItem)` before acting on a release, in both
        /// the drop-on-the-world path and the drop-on-a-grid path, and cancels the drag when
        /// the answer is no. Removing an item out from under a drag therefore ends the drag
        /// rather than duplicating the stack.
        /// </summary>
        private static void Spill(StowPost post, Inventory inventory, Vector2i size)
        {
            List<ItemDrop.ItemData> strays = null;

            foreach (var item in inventory.GetAllItems())
            {
                if (item == null) continue;
                if (item.m_gridPos.x < size.x && item.m_gridPos.y < size.y) continue;

                if (strays == null) strays = new List<ItemDrop.ItemData>();
                strays.Add(item);
            }

            if (strays == null) return;

            var rescued = 0;
            var dropped = 0;

            foreach (var item in strays)
            {
                if (!inventory.RemoveItem(item)) continue;

                // True when the stack was placed in a free slot *or* absorbed entirely into
                // stacks that were already there. False leaves the remainder on the item,
                // which is exactly what wants dropping.
                if (inventory.AddItem(item)) { rescued++; continue; }

                if (item.m_dropPrefab == null)
                {
                    // Nothing can be instantiated without it, so there is no honest way to
                    // hand this back. Said loudly because it is the one branch in this file
                    // that does lose something.
                    StowRuntime.Log.LogError(
                        "A stowing post shrank with " + item.m_stack + "x "
                        + item.m_shared.m_name + " in a slot that no longer exists, and the "
                        + "item has no drop prefab to put it in the world with. It is gone - "
                        + "report it.");
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

            StowRuntime.Log.LogInfo("Stowing post lost " + strays.Count + " slot"
                + (strays.Count == 1 ? "" : "s") + " with something in "
                + (strays.Count == 1 ? "it" : "them") + ": " + rescued
                + " moved into the post, " + dropped + " dropped at its feet.");

            // Only worth a message when something actually landed on the ground - the
            // rescued half is invisible and needs no explaining. Said through the player
            // rather than the log because the items are now lying in the grass and nobody
            // reads a log to find out why.
            if (dropped > 0 && StowConfig.Messages.Value && Player.m_localPlayer != null)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize(
                        "The post shrank - " + dropped + " stack"
                        + (dropped == 1 ? "" : "s") + " dropped at its feet."), 0, null);
        }

        // ------------------------------------------------------------------ the world

        /// <summary>
        /// Whether a shrink can be believed yet.
        ///
        /// Two separate races, both of which end with items deleted and neither of which
        /// announces itself.
        ///
        /// The first is the load. A post's inventory is empty between Container.Awake and
        /// the first CheckForChanges, so a post shrunk in that window looks like it has
        /// nothing to spill - and then its saved items are loaded against the smaller grid,
        /// which is where Inventory's own bounds test throws away everything past the new
        /// right-hand edge. Container.m_lastRevision answers this exactly: it is
        /// uint.MaxValue until Load or Save has run once, and nothing else writes it.
        ///
        /// The second is the neighbours. A zone is instantiated over several frames, so for
        /// the first of them a post with a creel rail beside it is a post with no rail
        /// registered yet - and shrinking on that answer would spill the contents of the
        /// extra slots of every railed post in the world, every time it loaded. There is no
        /// field to read for that one, so it is a timer, and the timer is set long enough
        /// to cover the load as well in case the reflection below ever stops binding.
        /// </summary>
        private static bool Settled(Container container, float awoke)
        {
            if (Time.time - awoke < SettleSeconds) return false;

            var revision = RevisionRef();
            if (revision == null) return true;   // timer only; it is the longer of the two

            return revision(container) != uint.MaxValue;
        }

        // ------------------------------------------------------------------ reflection

        // Both bound on first use inside a try/catch rather than in a field initialiser.
        // AccessTools.FieldRefAccess throws when the field name or the owner type is wrong,
        // and a throw from a static initialiser is a TypeInitializationException on every
        // later call to anything on this type - which would present as the post losing its
        // inventory entirely rather than as a missing field. A failed binding costs the one
        // feature it belongs to and nothing else.

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
    }
}
