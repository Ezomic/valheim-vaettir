using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Grove;
using Stow;

namespace Hod
{
    /// <summary>
    /// The chests around the post, and what may be taken out of them.
    ///
    /// There is no static registry of Containers in the game. CraftingStation keeps one and
    /// Piece keeps one; Container does not, so the only way to ask "what chests are near
    /// this" is a physics query. That is why the list is cached per frame rather than per
    /// call: the crafting panel runs HaveRequirements over every recipe in the game once a
    /// frame, each of those walks its requirements, and each requirement asks CountItems once
    /// per quality level. An OverlapSphere per CountItems would be thousands of physics
    /// queries a frame.
    ///
    /// The frame cache is not only an optimisation, and this is the important part. The
    /// counting patch and the consuming patch MUST agree: if the count sums a chest the
    /// consume then skips, HaveRequirements says yes and ConsumeResources takes less than the
    /// recipe costs - and vanilla's RemoveItem(string, ...) returns void and stops quietly
    /// when it runs out, so the result is a free craft with nothing in the log.
    ///
    /// That agreement is THREE promises here where Hirsla had two, and the third is what the
    /// fold changed:
    ///
    ///   which centre    <see cref="HodScope"/>, resolved once a frame and asked by this file
    ///                   rather than passed in by nine callers. Hirsla keyed this cache on the
    ///                   frame alone and carried a comment saying that was safe only because
    ///                   every caller happened to pass the player's position - an invariant
    ///                   maintained by eye. There is no point parameter any more, so a new
    ///                   caller cannot get it wrong.
    ///   which chests    the identical List instance, built once for the frame and handed to
    ///                   both halves.
    ///   what counts     the identical predicate. <see cref="Matches"/> plus
    ///                   <see cref="HodGate.AllowsStack"/>, called from the tally, from the
    ///                   per-container count and from both write paths. There is one copy of
    ///                   it on purpose - the tally used to inline its own, which made the
    ///                   central invariant something maintained by reading two functions side
    ///                   by side.
    ///
    /// Getting into a chest is TWO tests and the second one is the one everybody forgets:
    ///
    ///   privacy   Container.CheckAccess, private, taking a long playerID. Public is open to
    ///             everyone, Private only to the piece's creator, and Group returns false for
    ///             everyone in this build INCLUDING the person who set it.
    ///   the ward  NOT inside CheckAccess. Every vanilla call site repeats
    ///             m_checkGuardStone &amp;&amp; !PrivateArea.CheckAccess(position) for itself.
    ///             Skip this half and the bench reaches into a warded chest the player cannot
    ///             even open by hand.
    ///
    /// The rule that governs every write in this file: <b>nothing here ever writes a container
    /// it does not own.</b> Hirsla's first version called ZNetView.ClaimOwnership and then
    /// mutated the local copy, letting Container.Save write the whole thing back as one base64
    /// blob. Every part of that is unsound and it is worth writing down why, because the code
    /// read as if it were careful:
    ///
    ///   ClaimOwnership is not a lock. It is "if (!IsOwner()) m_zdo.SetOwner(sessionID)" and
    ///   nothing else - no handshake, no acknowledgement, no refusal. TWO CLIENTS CAN BOTH
    ///   SUCCEED, and each then believes it holds the chest.
    ///
    ///   The write does not merge. Save serialises the ENTIRE inventory, so a client working
    ///   from a stale copy does not lose its own change, it restores everything the other
    ///   client removed.
    ///
    ///   And the fork does not heal. ZDOMan.RPC_ZDOData drops an incoming ZDO whose
    ///   DataRevision is not strictly greater than the copy it holds, with no merge and no
    ///   compare-and-swap; ZDOPeer.ShouldSend then declines to offer it again, so nothing in
    ///   vanilla can repair it. The disagreement lasts the session and it resolves in favour
    ///   of the STALE client.
    ///
    /// So the write path is: the client that OWNS the container's ZDO does the removal against
    /// its own live inventory, and every other client asks it to. See <see cref="HodWithdraw"/>
    /// for the request/response half; this file keeps the reading, the reachability rules and
    /// the owner-side removal itself.
    ///
    /// Counting stays optimistic and that is deliberate. The crafting panel asks for counts
    /// every frame for every recipe in the game, so it cannot round-trip; the numbers on
    /// screen are what the local replica says, and the local replica is refreshed by vanilla
    /// once a second. <b>Display is optimistic; payment is authoritative.</b> A recipe can
    /// therefore look available and then decline, and that is the honest outcome - the
    /// alternative is a craft paid for with material that is not there.
    /// </summary>
    internal static class HodChests
    {
        /// <summary>
        /// Grown rather than fixed, because a fixed one fails silently.
        ///
        /// OverlapSphereNonAlloc counts COLLIDERS, not objects, and fills to capacity without
        /// any way to say it stopped early. At the default 20m radius a longhouse clears a
        /// thousand colliders on its walls, beams and floors alone, so a fixed 512 would drop
        /// whichever chests the broadphase happened to return last - and the broadphase order
        /// shifts as things move, so it would come and go between frames. What a player sees
        /// is a bench refusing material that is visibly in a chest ten metres away, with
        /// nothing in the log, and the first thing they would go and edit is BossBiomes.
        ///
        /// Not a free craft - the count and the consume walk the same truncated list - but the
        /// feature silently doing nothing in exactly the base it exists for.
        /// </summary>
        private static Collider[] _hits = new Collider[256];

        /// <summary>
        /// Where growing stops. A query that still saturates at this size is a base that has
        /// outrun the approach, and one warning is worth more than an unbounded allocation.
        /// </summary>
        private const int MaxHits = 8192;

        private static bool _warnedSaturated;

        private static readonly List<Container> Reachable = new List<Container>();

        /// <summary>-1 rather than 0, because frame 0 is a real frame.</summary>
        private static int _collectedFrame = -1;

        /// <summary>
        /// The network the cached list was gathered round. A bench question and a build question
        /// can land in one frame with different networks, and a list keyed on the frame alone
        /// would answer the second with the first one's chests.
        /// </summary>
        private static HodNet _collectedNet;

        /// <summary>
        /// Every reachable chest in the serving jib network's reach, gathered at most once a frame.
        ///
        /// The returned list is the live cache rather than a copy. Callers only read it, and
        /// handing out the same instance is precisely the guarantee that counting and
        /// consuming see the same chests in the same order.
        ///
        /// <b>It takes no position.</b> The network is <see cref="HodScope.Net"/>, resolved
        /// once a frame - see that class for why the feature measures from pieces in the world
        /// rather than from the player. An empty list when the scope is shut.
        /// </summary>
        public static List<Container> Near()
        {
            var net = HodScope.Net;
            if (_collectedFrame == Time.frameCount && ReferenceEquals(_collectedNet, net))
                return Reachable;

            _collectedFrame = Time.frameCount;
            _collectedNet = net;
            _tallyDirty = true;
            Reachable.Clear();

            if (!HodConfig.Enabled.Value) return Reachable;
            if (net == null) return Reachable;

            var radius = Mathf.Max(0f, HodConfig.Range.Value);
            if (radius <= 0f) return Reachable;

            var mask = SearchMask();

            // The union of every member's circle, each jib asked in turn and a chest in two
            // circles kept once. The members come in a fixed order out of HodNetwork's cached
            // list, so the count and the spend walk the same chests in the same order.
            for (var m = 0; m < net.Members.Count; m++)
            {
                var jib = net.Members[m];
                if (jib == null) continue;

                GatherAround(jib.transform.position, radius, mask);
            }

            return Reachable;
        }

        private static void GatherAround(Vector3 centre, float radius, int mask)
        {
            var count = Physics.OverlapSphereNonAlloc(centre, radius, _hits, mask);

            // Saturation is indistinguishable from "that was all of them", so it has to be
            // treated as an error rather than a result. Grow and ask again until the answer
            // fits, which for any real base happens on the first or second try.
            while (count >= _hits.Length && _hits.Length < MaxHits)
            {
                _hits = new Collider[Mathf.Min(_hits.Length * 2, MaxHits)];
                count = Physics.OverlapSphereNonAlloc(centre, radius, _hits, mask);
            }

            if (count >= _hits.Length && !_warnedSaturated)
            {
                _warnedSaturated = true;
                GrovePlugin.LogOnce(
                    "More than " + MaxHits + " colliders within " + radius + "m of a hod jib; "
                    + "the chest search was truncated and some chests will not be reached. "
                    + "Lower HodRange.");
            }

            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit == null) continue;

                // GetComponentInParent, because a chest's collider lives on a child of the
                // object carrying the Container.
                var container = hit.GetComponentInParent<Container>();
                if (container == null || Reachable.Contains(container)) continue;

                if (!Usable(container)) continue;

                Reachable.Add(container);
            }
        }

        /// <summary>Drops the cache, so the next question rebuilds it. For a world change.</summary>
        public static void Forget()
        {
            _collectedFrame = -1;
            _collectedNet = null;
            _talliedFrame = -1;
            Reachable.Clear();
            Totals.Clear();

            // The mask is derived from ZNetScene's prefab list, and ZNetScene is one of the
            // two things torn down and rebuilt on every world load.
            _maskBuilt = false;
        }

        // ------------------------------------------------------------------ the layer mask

        private static int _mask;
        private static bool _maskBuilt;

        /// <summary>
        /// Which physics layers a container can be found on, asked of the game rather than
        /// typed here.
        ///
        /// Without a mask the query has to compete for its buffer against every wall, floor,
        /// beam, trigger volume and dropped item in a 40m sphere, which is the whole reason
        /// the buffer above had to grow. The obvious alternative was to hardcode the piece
        /// layers off the layer table, and it was rejected for the usual reason: a guess that
        /// is wrong drops chests silently, which is the exact failure being fixed. Every
        /// Container in the game reaches the world through a ZNetScene prefab - a ZNetView is
        /// the only way it gets a valid ZDO, and Usable refuses one without - so the union of
        /// the collider layers on those prefabs is complete by construction, and a modded
        /// container is covered without anybody adding a line.
        ///
        /// Built once per world. That is a walk of the whole prefab list, a few tens of
        /// milliseconds on the first crafting question after a world loads, paid instead of a
        /// mask that is wrong in a way nobody can see.
        /// </summary>
        private static int SearchMask()
        {
            if (_maskBuilt) return _mask;

            var scene = ZNetScene.instance;

            // Not built yet. Ask everything this frame rather than latching an empty mask -
            // the next frame will have a scene.
            if (scene == null) return ~0;

            var mask = 0;

            for (var i = 0; i < scene.m_prefabs.Count; i++)
            {
                var prefab = scene.m_prefabs[i];
                if (prefab == null) continue;

                // true, because a prefab asset is not active in a scene and the default
                // overload skips every inactive child.
                if (prefab.GetComponentInChildren<Container>(true) == null) continue;

                var colliders = prefab.GetComponentsInChildren<Collider>(true);
                for (var c = 0; c < colliders.Length; c++)
                    if (colliders[c] != null) mask |= 1 << colliders[c].gameObject.layer;
            }

            _maskBuilt = true;

            // A scene that yielded nothing is a scene this code does not understand, and
            // querying every layer is the answer that cannot be too narrow.
            _mask = mask == 0 ? ~0 : mask;

            if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo("Hod chest search mask: 0x" + _mask.ToString("X8") + ".");

            return _mask;
        }

        // ------------------------------------------------------------------ can we use it

        private static bool Usable(Container container)
        {
            var nview = NetViewOf(container);
            if (nview == null || !nview.IsValid()) return false;

            if (container.GetInventory() == null) return false;

            // The stowing post itself is never one of its own chests, and this is the one
            // rule in the file that is Vaettir's rather than Hirsla's.
            //
            // A post stands inside the reach of a jib built beside it, so without this it
            // would be counted every time. That is not merely odd, it is the one place where
            // two halves of this mod would be reaching for the same stack: a post is a table
            // you put things ON so that a spirit can take them somewhere, and CarryRun holds
            // the stacks it has planned trips for by reference. Spending one out from under a
            // courier costs a wasted flight rather than an item - the take is clamped to the
            // live stack and the courier re-finds by identity on arrival - but a player who
            // drops twenty iron into a post and watches a craft eat it has no way to read
            // that as anything but the mod losing things.
            //
            // So the post is for passing things through and the chests are for taking things
            // out of, and those stay separate.
            if (StowPost.Is(container)) return false;

            // The ward half, which is not inside CheckAccess. flash:false deliberately - this
            // runs on every chest in range every frame the crafting panel is open, and the
            // default flashes the ward's shield effect. Container.GetHoverText passes the same
            // false for the same reason.
            if (container.m_checkGuardStone
                && !PrivateArea.CheckAccess(container.transform.position, 0f, false))
                return false;

            if (!PrivacyAllows(container)) return false;

            if (!VehicleAllows(container, nview)) return false;

            if (HeldByAnotherPlayer(container, nview)) return false;

            // Nothing is forced to re-read here. Hirsla's first version called Container.Load
            // at this point believing it narrowed the multiplayer window; Load reads
            // m_nview.GetZDO().GetString(...), which is the LOCAL replica, so it narrowed
            // nothing - and it broke the container's own CheckForChanges a moment later, since
            // Load early-returns on an unmoved DataRevision and the container then skipped
            // both UpdateUseVisual and the m_autoDestroyEmpty self-destruct. That bug fired in
            // SINGLEPLAYER and had nothing to do with any race.

            return true;
        }

        /// <summary>
        /// Whether somebody has this container open right now.
        ///
        /// Vanilla's own RPC_RequestOpen refuses a second opener, so reaching into one behind
        /// their back is writing to an inventory another client is editing - their window then
        /// shows stock that is gone, and whichever of the two saves last wins.
        ///
        /// The flag is read off the ZDO and not through IsInUse(). m_inUse is a local bool, so
        /// a remote client asking about somebody else's open chest gets false; the ZDO is where
        /// the answer really lives and Container.UpdateUseVisual reads it exactly this way for
        /// non-owners.
        ///
        /// Whether the opener is US is then the local bool and nothing else. Container.SetInUse
        /// refuses to run unless this client owns the view, and opening a container hands
        /// ownership to the opener, so m_inUse is true here only on the machine whose window is
        /// open - which is the chest everybody expects to be served by, and the one an
        /// unqualified ZDO test would have excluded.
        ///
        /// Deliberately NOT "are we the owner". ZDOMan.ReleaseNearbyZDOS hands a chest standing
        /// in your active area to you within two seconds whether or not you have ever touched
        /// it, so owning a chest says nothing about whether somebody is looking at its
        /// contents.
        ///
        /// One race this cannot see, and it is written down rather than papered over.
        /// RPC_RequestOpen sets the ZDO's owner and answers "granted"; the opener writes
        /// s_inUse only afterwards, when InventoryGui.Show calls SetInUse. For that round trip
        /// the chest reads here as neither ours nor in use.
        /// </summary>
        private static bool HeldByAnotherPlayer(Container container, ZNetView nview)
        {
            var zdo = nview.GetZDO();
            if (zdo == null) return true;

            if (zdo.GetInt(ZDOVars.s_inUse) != 1) return false;

            return !container.IsInUse();
        }

        /// <summary>
        /// Carts and boat holds, which are a container hanging off a vehicle's ZNetView.
        ///
        /// Two separate refusals and they are not the same refusal.
        ///
        /// A cart in use. All three of vanilla's container RPCs - RequestOpen, RequestStack and
        /// RequestTakeAll - carry the clause "m_wagon &amp;&amp; m_wagon.InUse()" and refuse,
        /// and Vagon.RPC_RequestOwn refuses to hand ownership over while it holds. Reaching in
        /// here is therefore reaching past a refusal the game would have made by hand.
        /// Vagon.InUse is answerable remotely, unlike Container's: it is the container's local
        /// bool OR IsAttached, and IsAttached falls back to reading s_attachJointHash off the
        /// ZDO.
        ///
        /// A vehicle somebody else owns. Writing to any container means claiming its view, and
        /// on a vehicle that view is also the physics authority: Vagon.FixedUpdate ends in
        /// "else if (IsAttached()) Detach()", so the frame the owner changes, the player towing
        /// the cart drops it in the road with no message and no cause they could name. A ship's
        /// helmsman loses control of the boat the same way. Taking that away from another player
        /// to spend one log is not a trade this mod should make.
        ///
        /// An unowned vehicle, or one already ours, is fine - claiming it is exactly what
        /// pressing Use on it would have done. Ordinary chests are untouched by any of this;
        /// m_rootObjectOverride is null on all of them.
        /// </summary>
        private static bool VehicleAllows(Container container, ZNetView nview)
        {
            if (container.m_wagon != null && container.m_wagon.InUse()) return false;

            if (container.m_rootObjectOverride == null) return true;

            var zdo = nview.GetZDO();
            if (zdo == null) return false;

            return zdo.IsOwner() || !zdo.HasOwner();
        }

        /// <summary>
        /// The ZNetView the Container itself uses.
        ///
        /// Not simply GetComponent&lt;ZNetView&gt;. Container.Awake prefers m_rootObjectOverride
        /// when it is set, which is how a cart's inventory hangs off the vehicle rather than off
        /// its own object, and asking the wrong view about ownership would answer about the
        /// wrong object.
        /// </summary>
        public static ZNetView NetViewOf(Container container)
        {
            if (container == null) return null;

            if (container.m_rootObjectOverride != null)
                return container.m_rootObjectOverride.GetComponent<ZNetView>();

            ZNetView nview;
            return container.TryGetComponent(out nview) ? nview : null;
        }

        // ------------------------------------------------------------------ privacy

        /// <summary>Bound lazily, never in a static initialiser. See BindAccess.</summary>
        private static Func<Container, long, bool> _checkAccess;

        private static bool _accessBound;

        private static bool PrivacyAllows(Container container)
        {
            var game = Game.instance;
            if (game == null) return false;

            return PrivacyAllows(container, game.GetPlayerProfile().GetPlayerID());
        }

        /// <summary>
        /// The same rule asked about somebody else's character.
        ///
        /// Split out for the owner side of a network request, which is judging a player who is
        /// not the one at this keyboard - and on a dedicated server there is no player at this
        /// keyboard at all, so the Game.instance path above would answer for nobody.
        /// </summary>
        private static bool PrivacyAllows(Container container, long playerID)
        {
            BindAccess();

            if (_checkAccess != null) return _checkAccess(container, playerID);

            // The mirror. Reached only when the reflection above failed, which means the game
            // changed shape - so it is a best effort at the rule rather than the rule itself,
            // and it is written to refuse rather than to allow when it is unsure.
            //
            // One deliberate difference from vanilla: a Private container with no Piece on it
            // answers false here, where vanilla would dereference a null m_piece and throw.
            // Refusing to reach into an odd container is a feature not working; throwing out
            // of a Harmony patch mid-frame is a vanilla mechanic breaking, and it would land
            // in Player.log where nobody would look for it.
            switch (container.m_privacy)
            {
                case Container.PrivacySetting.Public:
                    return true;

                case Container.PrivacySetting.Private:
                    Piece piece;
                    return container.TryGetComponent(out piece) && piece.GetCreator() == playerID;

                // Group, and anything a later version adds. Group returns false for everyone
                // in this build, the creator included, so honouring it means refusing it.
                default:
                    return false;
            }
        }

        /// <summary>
        /// Binds Container.CheckAccess, once, inside a try/catch.
        ///
        /// Lazily and not in a static field, because a FieldRef or MethodInfo bound at type
        /// initialisation throws at type-init when the member is not there, and from then on
        /// EVERY Harmony patch that class carries throws TypeInitializationException. That
        /// presents as unrelated vanilla features breaking - "I cannot equip any tool" - and
        /// sends you looking a very long way from the mod that caused it. A failed binding here
        /// costs the reflection and nothing else.
        ///
        /// An open delegate rather than MethodInfo.Invoke: this is asked of every chest in
        /// range every frame the crafting panel is open, and Invoke boxes its arguments into a
        /// fresh object[] each time.
        /// </summary>
        private static void BindAccess()
        {
            if (_accessBound) return;
            _accessBound = true;

            try
            {
                var method = AccessTools.Method(
                    typeof(Container), "CheckAccess", new[] { typeof(long) });

                if (method != null)
                    _checkAccess = (Func<Container, long, bool>)Delegate.CreateDelegate(
                        typeof(Func<Container, long, bool>), method);
            }
            catch (Exception e)
            {
                _checkAccess = null;
                GrovePlugin.LogOnce(
                    "Could not bind Container.CheckAccess (" + e.Message + "); the hod jib is "
                    + "falling back to its own copy of the privacy rule. Private chests may "
                    + "behave differently from the game's own idea of them.");
            }

            if (_checkAccess == null && HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo("Container.CheckAccess not found; using the mirror.");
        }

        // ------------------------------------------------------------------ counting

        /// <summary>
        /// Vanilla's own CountItems predicate, so a filtered count and an unfiltered one can
        /// never disagree about anything except the biome.
        ///
        /// Copied rather than called, because Inventory.CountItems is the method this feature
        /// patches: calling it here on a chest is harmless today - the patch checks that the
        /// inventory is the player's - but it would be a re-entrance waiting for the first
        /// person to widen that check.
        /// </summary>
        private static bool Matches(ItemDrop.ItemData item, string name, int quality,
                                    bool matchWorldLevel)
        {
            if (item == null || item.m_shared == null) return false;
            if (name != null && item.m_shared.m_name != name) return false;
            if (quality >= 0 && quality != item.m_quality) return false;
            if (matchWorldLevel && item.m_worldLevel < Game.m_worldLevel) return false;

            return true;
        }

        /// <summary>How much of this a chest holds that the gate will let through.</summary>
        public static int CountAllowed(Container container, string name, int quality,
                                       bool matchWorldLevel)
        {
            var inventory = container.GetInventory();
            if (inventory == null) return 0;

            var items = inventory.GetAllItems();
            var total = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!Matches(item, name, quality, matchWorldLevel)) continue;

                // The gate is applied per stack rather than per query, because the biome index
                // is keyed on the PREFAB name and CountItems asks by the shared name - which is
                // a localisation token, "$item_wood", and the same token can front more than
                // one prefab.
                if (!HodGate.AllowsStack(item)) continue;

                total += item.m_stack;
            }

            return total;
        }

        /// <summary>
        /// Everything in reach of the post, summed. The count half of the crafting patch.
        ///
        /// This is the hot path in the whole feature and it deserves the arithmetic written
        /// down. InventoryGui.UpdateRecipeList runs HaveRequirements over EVERY recipe in the
        /// game once a frame; each of those walks its requirements, and each requirement asks
        /// CountItems once per quality level. Call it six thousand calls a frame. Scanning
        /// twenty chests of thirty items on each of those is millions of iterations a frame
        /// and would be felt as a stutter with the crafting panel open.
        ///
        /// So the chests are walked ONCE a frame into a name-and-quality tally and every
        /// question after that is a dictionary lookup. The tally is thrown away whenever
        /// something is taken out of a chest, so it can never answer about stock that has
        /// already been spent.
        /// </summary>
        public static int CountAllowed(string name, int quality, bool matchWorldLevel)
        {
            // Two cases the tally cannot answer, both rare, both correct to answer the slow
            // way rather than to approximate. A null name means "anything at all", which the
            // tally is not keyed for; matchWorldLevel false means counting items the tally
            // deliberately left out. Vanilla passes true and a real name everywhere this has
            // been read, so neither is on the hot path.
            if (name == null || !matchWorldLevel)
            {
                var chests = Near();
                var scanned = 0;

                for (var i = 0; i < chests.Count; i++)
                {
                    var container = chests[i];
                    if (container == null) continue;

                    scanned += CountAllowed(container, name, quality, matchWorldLevel);
                }

                return scanned;
            }

            int total;
            return Tally().TryGetValue(new Slot(name, quality), out total) ? total : 0;
        }

        /// <summary>
        /// The same sum restricted to chests this client can actually spend out of, right now,
        /// without asking anybody.
        ///
        /// This is the SPENDABLE count, as against the optimistic one above, and the difference
        /// between the two is the whole of the multiplayer story. The panel shows the optimistic
        /// number because it has to - it asks thousands of times a frame and cannot round-trip -
        /// but the moment material is about to be spent, the only stock that may be counted is
        /// stock this machine may remove synchronously. Anything else has to be fetched into
        /// the pack first and paid for out of the pack.
        ///
        /// Deliberately not tallied. It is asked a handful of times per craft, not thousands of
        /// times a frame, and a second tally would be a second thing to invalidate at exactly
        /// the moments the first one is already delicate.
        /// </summary>
        public static int CountSpendable(string name, int quality, bool matchWorldLevel)
        {
            var chests = Near();
            var total = 0;

            for (var i = 0; i < chests.Count; i++)
            {
                var container = chests[i];
                if (container == null || !Owned(container)) continue;

                total += CountAllowed(container, name, quality, matchWorldLevel);
            }

            return total;
        }

        /// <summary>
        /// Whether anything in reach would answer HaveItem.
        ///
        /// Its one caller - HodCrafting.HaveChestItem - has been dormant since Hirsla was cut
        /// to the crafting panel on 2026-09-06, because the only crafting question that routed
        /// through HaveItem was RequirementMode.CanAlmostBuild and that arrives through the
        /// Piece overload of HaveRequirements, which is not bracketed. Both are kept, and they
        /// have to be kept together: the invariant the whole scope rests on is that a count and
        /// a consume inside one bracket never disagree, and a chest-aware CountItems sitting
        /// beside a pack-only HaveItem is that disagreement waiting for whichever vanilla
        /// version or sibling mod asks the other question.
        /// </summary>
        public static bool HasAllowed(string name, bool matchWorldLevel)
        {
            // Quality -1, which is what HaveItem's predicate amounts to: it asks about the
            // name and the world level and nothing else.
            return CountAllowed(name, -1, matchWorldLevel) > 0;
        }

        // ------------------------------------------------------------------ the tally

        /// <summary>
        /// One item name at one quality level. -1 for quality means "any", which is how
        /// vanilla's own count reads a negative quality.
        ///
        /// A struct implementing IEquatable so Dictionary uses the non-boxing comparer. The
        /// obvious alternative, a "name#quality" string key, allocates a string on every one of
        /// those six thousand lookups a frame.
        /// </summary>
        private struct Slot : IEquatable<Slot>
        {
            private readonly string _name;
            private readonly int _quality;

            internal Slot(string name, int quality)
            {
                _name = name;
                _quality = quality < 0 ? -1 : quality;
            }

            public bool Equals(Slot other)
            {
                return _quality == other._quality && _name == other._name;
            }

            public override bool Equals(object obj)
            {
                return obj is Slot && Equals((Slot)obj);
            }

            public override int GetHashCode()
            {
                return ((_name == null ? 0 : _name.GetHashCode()) * 397) ^ _quality;
            }
        }

        private static readonly Dictionary<Slot, int> Totals = new Dictionary<Slot, int>();

        private static int _talliedFrame = -1;

        /// <summary>
        /// Set the moment anything is removed from a chest, so the next count rebuilds.
        ///
        /// Separate from the frame stamp on purpose. The container LIST is deliberately frozen
        /// for the frame - that is what makes the counting patch and the consuming patch see
        /// the same chests in the same order - but the CONTENTS of those chests change under
        /// this feature's own hand, and a tally that outlived a withdrawal would report stock
        /// that has already been spent.
        /// </summary>
        private static bool _tallyDirty;

        private static Dictionary<Slot, int> Tally()
        {
            // Asked first, and every time: it is cached, and it is what notices that the post
            // changed within a frame and marks the tally dirty. Checking the frame before asking
            // would answer a build question with the bench's totals.
            var chests = Near();

            if (_talliedFrame == Time.frameCount && !_tallyDirty) return Totals;

            _talliedFrame = Time.frameCount;
            _tallyDirty = false;
            Totals.Clear();

            for (var c = 0; c < chests.Count; c++)
            {
                var container = chests[c];
                if (container == null) continue;

                var inventory = container.GetInventory();
                if (inventory == null) continue;

                var items = inventory.GetAllItems();

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    // The shared predicate, asked in its widest form: any name, any quality,
                    // world level matched. That last part is why the tally only answers the
                    // matchWorldLevel:true question and CountAllowed routes the other one to
                    // the slow path.
                    //
                    // Called rather than inlined, and that is the point of it being here. This
                    // loop used to carry its own copy of the clauses, which made "the count and
                    // the consume agree" a thing maintained by reading two functions side by
                    // side - and the day somebody widened one of them, HaveRequirements and
                    // ConsumeResources would have disagreed about the same chest with nothing
                    // in the log to say so.
                    if (!Matches(item, null, -1, true)) continue;

                    if (!HodGate.AllowsStack(item)) continue;

                    Add(new Slot(item.m_shared.m_name, -1), item.m_stack);
                    Add(new Slot(item.m_shared.m_name, item.m_quality), item.m_stack);
                }
            }

            return Totals;
        }

        private static void Add(Slot slot, int stack)
        {
            int existing;
            Totals[slot] = Totals.TryGetValue(slot, out existing) ? existing + stack : stack;
        }

        // ------------------------------------------------------------------ ownership

        /// <summary>
        /// Whether this client owns the container's ZDO, and may therefore write to it.
        ///
        /// This is the whole of the fast-path test, and it is true far more often than the
        /// shape of the code suggests. In singleplayer and on a listen host the local peer
        /// owns everything there is. On a dedicated server, ZDOMan.ReleaseZDOS runs every two
        /// seconds and walks each peer's active area handing over any ZDO that is ownerless or
        /// whose owner is no longer near it - so a player who has been standing at their own
        /// chest wall for more than a couple of seconds owns those chests without ever having
        /// touched them, and ownership is STICKY: another peer at the same wall does not take
        /// them back while the first is still there.
        ///
        /// So the request path is not the common case. It is the case where somebody else is
        /// standing there, or where the chest is far enough from every player that the server
        /// still holds it.
        ///
        /// <b>With one gap, and it is in singleplayer too.</b> ZDO.Load sets Owned = false on
        /// every ZDO read off the world save, and ZDOMan.ReleaseZDOS only runs its claim pass
        /// once its two-second timer comes round - so for up to two seconds after a world load
        /// or a teleport, the chests in front of you have no owner at all. They are countable
        /// and not yet spendable, which is a recipe drawn in blue that declines when pressed
        /// with ShortMessage. It clears itself within the two seconds, and the refusal is the
        /// honest answer rather than a promise: nothing was asked for, because HodWithdraw.Ask
        /// declines to broadcast a request an ownerless ZDO would send to everybody and nobody.
        ///
        /// Widening this to "owned OR ownerless" is the obvious repair and it is WRONG, which
        /// is worth writing down because it looks safe: an ownerless ZDO is nobody's to write,
        /// including ours. Container.OnContainerChanged is "if (!m_loading &amp;&amp; IsOwner())
        /// Save()", and ZDO.IsOwner is the Owner flag, which SetOwnerInternal only ever sets
        /// for a real uid. So the removal would happen in the local Inventory and never reach
        /// the ZDO, the container's own CheckForChanges would restore it on the next revision
        /// bump, and the material would come back after being spent. That is a duplication,
        /// reached by trying to avoid a two-second wait.
        /// </summary>
        public static bool Owned(Container container)
        {
            var nview = NetViewOf(container);
            return nview != null && nview.IsValid() && nview.IsOwner();
        }

        /// <summary>
        /// The last gate before an owner-side write, asked again rather than trusted from the
        /// frame's Usable pass.
        ///
        /// Two of these can change under the feature's feet between the count and the take, and
        /// both are cheap. Asking them again cannot make the count and the consume disagree:
        /// the network layer processes RPCs from ZNet's own Update, so a ZDO's owner and its
        /// in-use flag are fixed for the length of a frame, and the count and the consume are
        /// always in the same one.
        ///
        /// ClaimOwnership is deliberately NOT here, and not anywhere. It is what defeated
        /// MultiUserChest's guard - that mod refuses a removal from an inventory the local
        /// client does not own, and a claim a frame earlier makes the refusal pass - and it is
        /// what let two clients each believe they held one chest. A container this client does
        /// not own is asked, not taken. See HodWithdraw.
        /// </summary>
        private static bool MayWriteHere(Container container)
        {
            var nview = NetViewOf(container);
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return false;

            if (HeldByAnotherPlayer(container, nview)) return false;
            if (!VehicleAllows(container, nview)) return false;

            return true;
        }

        /// <summary>
        /// The owner-side half of the access rules, for a withdrawal that arrived over the
        /// network rather than from the player at this keyboard.
        ///
        /// It is the same set of questions vanilla's own RPC_RequestOpen, RPC_RequestStack and
        /// RPC_RequestTakeAll ask before granting anything, in the same order, and it is asked
        /// HERE - on the owner - because that is the only machine whose answer is authoritative.
        /// A requester's opinion of its own access is not evidence.
        ///
        /// The in-use clause is vanilla's, verbatim in meaning: a container in use refuses
        /// everybody EXCEPT the peer that has it open, because that peer is the owner and
        /// asking itself is how the fast path above reads on a listen host. IsInUse() is a
        /// local bool and this code only ever runs on the owner, which is the one machine where
        /// it is meaningful - the same reason Container's own RPCs can rely on it.
        ///
        /// The ward is deliberately NOT asked here, and it is the one access rule that stays
        /// with the requester. PrivateArea.CheckAccess answers through HaveLocalAccess, whose
        /// body is "m_piece.IsCreator() || IsPermitted(Player.m_localPlayer.GetPlayerID())" - a
        /// question about the machine asking it, not about the ward. On a dedicated server there
        /// is no local player, so that line does not merely answer wrongly, it dereferences null
        /// and throws; and on another player's client it would answer with THEIR permissions
        /// rather than the requester's. Usable() asks it on the requester, where the local player
        /// is the player it is about. Vanilla's own container RPCs are arranged the same way for
        /// the same reason.
        ///
        /// The stowing-post exclusion is NOT repeated here, and that is deliberate rather than
        /// an oversight: it is a rule about which chests a bench may draw from, decided on the
        /// requester where the scope lives, and the owner of a post has no way to know whose
        /// bench is asking. A requester that never asks about a post means the owner is never
        /// asked about one.
        /// </summary>
        public static bool OwnerMayServe(Container container, long playerID, long requester)
        {
            if (container == null) return false;

            var nview = NetViewOf(container);
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return false;

            if (!PrivacyAllows(container, playerID)) return false;

            if (!VehicleAllows(container, nview)) return false;

            // uid != ZNet.GetUID() is vanilla's own escape hatch, and it is the peer id rather
            // than the player id on purpose - the question is "is the machine asking the machine
            // that has it open", not "is it the same character".
            if (container.IsInUse() && requester != ZDOMan.GetSessionID()) return false;

            if (container.m_wagon != null && container.m_wagon.InUse()) return false;

            return true;
        }

        // ------------------------------------------------------------------ taking

        /// <summary>
        /// Removes up to <paramref name="amount"/> out of one chest THIS CLIENT OWNS and
        /// returns how much really came out.
        ///
        /// This is the single removal primitive. Both the local fast path and the owner side of
        /// the network request end up here, which is what keeps "what the owner did" and "what
        /// a local craft does" from being two implementations of one rule.
        ///
        /// <paramref name="harvested"/> is how the network path gets the items back: pass a list
        /// and it receives a clone of each stack that was removed, at the amount removed, so
        /// they can be serialised and handed to the peer that asked. Pass null and the material
        /// is simply destroyed, which is what a local craft wants - the material is being SPENT
        /// rather than carried, so it never touches the player's pack and cannot be stopped by
        /// a full one.
        ///
        /// Three things in here are load-bearing and each of them was a bug in Hirsla's first
        /// version:
        ///
        ///   the owner test    a write to a container this client does not own is discarded by
        ///                     Container.OnContainerChanged ("if (!m_loading &amp;&amp; IsOwner())
        ///                     Save()") and the local copy then drifts from the truth. It used to
        ///                     call ClaimOwnership to make the test pass. It no longer does.
        ///   the clamp         Mathf.Min against the stack as it stands right now, not against
        ///                     what a count a frame ago said. This is the line that closes the
        ///                     race: a request for more than remains gets a partial, never a
        ///                     phantom.
        ///   the return value  Inventory.RemoveItem(ItemData, int) RETURNS whether it removed
        ///                     anything, and this used to ignore it. That matters beyond
        ///                     tidiness: the two-argument overload delegates to the one-argument
        ///                     one whenever amount == item.m_stack, Harmony detours the method
        ///                     itself so an internal call site hits a patch on it, and
        ///                     MultiUserChest installs a prefix there that sets __result = false
        ///                     and skips the original when the caller is not the owner. With that
        ///                     mod installed the old code counted items it had not removed and
        ///                     handed out free crafts.
        ///
        /// <paramref name="spentFrom"/> is the show flight's only input from this file, and it
        /// is deliberately a POSITION rather than the container: what the spirit is given is a
        /// point in the air and an item name, and there is no route from either back to a stack.
        /// See HodShow.
        /// </summary>
        public static int TakeFromOwned(Container container, string name, int quality,
                                        bool matchWorldLevel, int amount,
                                        List<ItemDrop.ItemData> harvested,
                                        List<HodShow.Spent> spentFrom = null)
        {
            if (amount <= 0 || container == null) return 0;
            if (!MayWriteHere(container)) return 0;

            var inventory = container.GetInventory();
            if (inventory == null) return 0;

            var items = inventory.GetAllItems();
            var taken = 0;

            // Backwards, because RemoveItem takes the stack out of this very list when it
            // empties it. Walking down means a removal is always above the index still to be
            // visited.
            for (var i = items.Count - 1; i >= 0 && taken < amount; i--)
            {
                var item = items[i];
                if (!Matches(item, name, quality, matchWorldLevel)) continue;
                if (!HodGate.AllowsStack(item)) continue;

                // Only when the caller wants the items back. An item with no m_dropPrefab
                // cannot be written into a ZPackage - Inventory.Save writes an empty name and
                // Inventory.Load silently skips it - so handing one to a peer would destroy it.
                // Destroying it is fine when it is being spent; it is not fine when it is being
                // carried, so that case is skipped rather than removed.
                if (harvested != null && item.m_dropPrefab == null) continue;

                // Against the stack as it stands, this instant. See the docstring.
                var take = Mathf.Min(item.m_stack, amount - taken);
                if (take <= 0) continue;

                // A clone taken BEFORE the removal, because the removal can free the stack.
                ItemDrop.ItemData copy = null;
                if (harvested != null)
                {
                    copy = item.Clone();
                    copy.m_stack = take;
                }

                // The prefab NAME, read now while the stack is still whole, and nothing else.
                // This is the whole of what leaves this method for the flight: a string, plus
                // the chest's position read from its transform below.
                var cargoName = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;

                // The ItemData overload, not the string one. The string overload walks the list
                // itself and would happily take from a stack this loop just decided the gate
                // refuses - a count and a consume disagreeing inside one method.
                //
                // No explicit save: RemoveItem ends in Changed(), and Container wires that to
                // its own OnContainerChanged, which persists the inventory.
                if (!inventory.RemoveItem(item, take)) continue;

                taken += take;
                if (copy != null) harvested.Add(copy);

                if (spentFrom != null)
                    spentFrom.Add(new HodShow.Spent(container.transform.position, cargoName));

                // The frame's tally described a chest that has just changed.
                _tallyDirty = true;
            }

            if (taken > 0 && HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo(
                    "Hod took " + taken + " " + name + " out of " + container.m_name + ".");

            return taken;
        }

        /// <summary>
        /// Moves up to <paramref name="amount"/> out of a chest THIS CLIENT OWNS into a pack,
        /// one item at a time, and returns how many arrived.
        ///
        /// One at a time on purpose: a pack that fills up mid-transfer must stop the transfer,
        /// not delete the overflow. Inventory.AddItem(ItemData) can partially succeed - it tops
        /// up existing stacks and then fails to find a slot for the remainder, returning false
        /// having already moved some - so the only amount it can be asked for and be believed
        /// about is one.
        ///
        /// A clone of the stack rather than AddItem(string prefabName, ...). That overload
        /// builds a fresh item off the prefab and stamps the CURRENT world level onto it, along
        /// with default durability and no custom data. For coal that is invisible; for anything
        /// carried over from a higher world level it is the mod quietly rewriting an item as it
        /// passes through, which is the one thing a storage mod must never do.
        ///
        /// <b>The order is remove-then-add, and it used to be the other way round.</b> That is
        /// worth writing down at length, because add-then-remove looked like the careful choice
        /// and the compensating remove it relied on could not work:
        ///
        ///   Inventory.AddItem(ItemData) on a stackable item does not add the object you hand it
        ///   when the amount fits in a stack that is already there. It loops m_stack times,
        ///   finds a matching stack through FindFreeStackItem, increments THAT stack's count,
        ///   and returns true having never touched m_inventory and never decremented the
        ///   argument's m_stack. So after a successful merge the clone is not in the pack and
        ///   still says it holds one.
        ///
        ///   The undo was pack.RemoveItem(one, 1). Its first line is
        ///   "amount = Min(item.m_stack, amount)", which is 1, which equals item.m_stack, so it
        ///   delegates to RemoveItem(ItemData) - whose first line is a m_inventory.Contains test
        ///   that the clone fails. It logged "Item is not in this container", returned false,
        ///   and removed nothing. The code written to prevent a duplication was the duplication.
        ///
        /// Removing first is safe here in a way it would not be for a larger amount, and the
        /// reason is that the amount is one. AddItem with a stack of one either merges, or takes
        /// an empty slot, or does nothing at all and returns false - there is no partial outcome
        /// to unwind. So on a refused add the unit goes straight back into the chest it came out
        /// of a line earlier, which cannot be full, because it just got smaller.
        ///
        /// <paramref name="spentFrom"/> is the show flight's other input, and it is the same
        /// deliberately narrow thing TakeFromOwned records: a POSITION and a prefab NAME, read
        /// while the stack is still whole, with no route from either back to an item. One entry
        /// per chest rather than one per unit - this loop moves items one at a time and twenty
        /// arrows out of one chest is one journey as far as anybody watching is concerned, so a
        /// per-unit record would be twenty identical entries for HodShow to collapse again.
        /// </summary>
        public static int MoveFromOwned(Container container, Inventory pack, string name,
                                        int quality, bool matchWorldLevel, int amount,
                                        List<HodShow.Spent> spentFrom = null)
        {
            if (amount <= 0 || pack == null || container == null) return 0;
            if (!MayWriteHere(container)) return 0;

            var store = container.GetInventory();
            if (store == null) return 0;

            var moved = 0;

            // The prefab name of whatever this chest ended up handing over, kept for the one
            // flight entry written at the bottom. A string, taken off the stack before it is
            // touched - nothing here holds an item for the flight's sake.
            string cargoName = null;

            while (moved < amount)
            {
                var stack = FirstAllowed(store, name, quality, matchWorldLevel);
                if (stack == null) break;

                if (cargoName == null && stack.m_dropPrefab != null)
                    cargoName = stack.m_dropPrefab.name;

                var one = stack.Clone();
                one.m_stack = 1;

                // The removal first, and its return value read. A false here is the chest
                // refusing - MultiUserChest's prefix on the one-argument RemoveItem does exactly
                // that when the caller is not the owner, and the two-argument overload delegates
                // to it whenever the amount equals the stack - so nothing has moved and the loop
                // simply stops.
                if (!store.RemoveItem(stack, 1)) break;

                if (pack.AddItem(one))
                {
                    moved++;

                    // The frame's tally described a chest that has just changed.
                    _tallyDirty = true;
                    continue;
                }

                // A full pack, which is the ordinary way for this to end. The unit goes back
                // where it came from; the chest cannot refuse it, because it lost one a line
                // ago. If it somehow does, the floor beats destroying it.
                if (!store.AddItem(one))
                {
                    GrovePlugin.LogOnce(
                        "A chest would not take back one " + name + " after a full pack refused "
                        + "it. It was dropped rather than lost.");

                    if (one.m_dropPrefab != null)
                        ItemDrop.DropItem(one, 1,
                            container.transform.position + Vector3.up * 0.5f,
                            Quaternion.identity);
                }

                _tallyDirty = true;
                break;
            }

            // One entry for the whole trip out of this chest, and only when something really
            // left it. Written here rather than inside the loop so a stack moved a unit at a
            // time is one journey rather than twenty spirits in a queue.
            if (moved > 0 && spentFrom != null && cargoName != null)
                spentFrom.Add(new HodShow.Spent(container.transform.position, cargoName));

            if (moved > 0 && HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo(
                    "Hod drew " + moved + " " + name + " out of " + container.m_name + ".");

            return moved;
        }

        public static ItemDrop.ItemData FirstAllowed(Inventory inventory, string name,
                                                     int quality, bool matchWorldLevel)
        {
            var items = inventory.GetAllItems();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!Matches(item, name, quality, matchWorldLevel)) continue;
                if (!HodGate.AllowsStack(item)) continue;

                return item;
            }

            return null;
        }
    }
}
