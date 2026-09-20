using System;
using System.Collections.Generic;
using UnityEngine;
using Grove;

namespace Hod
{
    /// <summary>
    /// Taking material out of a chest this client does not own, by asking the client that does.
    ///
    /// The rule this file exists to keep is one sentence: <b>nothing here ever writes a
    /// container it does not own.</b> Everything else follows from it.
    ///
    /// Why that rule and not the obvious alternative. Hirsla's first version called
    /// ZNetView.ClaimOwnership and then wrote the container's inventory locally. ClaimOwnership
    /// is "if (!IsOwner()) m_zdo.SetOwner(sessionID)" - no handshake, no acknowledgement, no
    /// refusal - so two clients can both succeed and each then believes it holds the chest.
    /// Container.Save then writes the WHOLE inventory back as one base64 blob, so a client
    /// working from a stale copy does not merge, it restores; and ZDOMan.RPC_ZDOData drops any
    /// incoming ZDO whose DataRevision is not strictly greater, with no compare-and-swap, so
    /// the resulting fork does not heal within a session and it resolves in favour of the STALE
    /// client. HodChests carries the long version.
    ///
    /// The shape is vanilla's own. Container has three request/response pairs already -
    /// RequestOpen/OpenRespons, RPC_RequestStack/RPC_StackResponse and
    /// RequestTakeAll/TakeAllRespons - and every one of them is "ask the owner, the owner
    /// decides, the owner answers the one peer that asked". This is a fourth pair in the same
    /// mould. What it does NOT copy is RPC_TakeAllRespons, where the REQUESTER calls
    /// ClaimOwnership and moves items out of its own local copy behind a two-second debounce:
    /// that is the very defect being avoided here, and vanilla's own Take All has it.
    ///
    /// The line that actually closes the race is on the owner and it is one line - the clamp in
    /// HodChests.TakeFromOwned, Mathf.Min against the stack as it stands at that instant. A
    /// requester asking for more than remains gets a partial, never a phantom. Everything
    /// around it is bookkeeping: a correlation id so a reply can be matched to a request, a
    /// timeout because delivery is best effort, and an in-flight guard so the same stack is not
    /// asked for twice while an answer is on its way.
    ///
    /// Two registrations, on ZRoutedRpc rather than on each Container's ZNetView, and that is a
    /// deliberate difference from Container's own six. A ZNetView-routed RPC is delivered by
    /// ZRoutedRpc.HandleRoutedRPC only after ZNetScene.FindInstance resolves the target ZDO to
    /// a live object, so a request aimed at a container the owner has since unloaded is dropped
    /// with nothing said and the requester waits out its timeout. Routing to the peer and
    /// carrying the ZDOID as a parameter means the owner always gets the message and can answer
    /// "nothing", which turns a silent timeout into an immediate honest refusal. It also avoids
    /// ZNetView.Register's m_functions.Add throwing on a container whose Awake runs twice.
    ///
    /// <b>What happens when a reply never arrives.</b> Nothing is credited, because nothing is
    /// credited until a reply says so. The request is dropped from the table after
    /// RequestTimeout seconds and the in-flight guard is released, so the next craft asks
    /// again. The material simply does not appear in the pack and the craft declines with a
    /// message - which is the same outcome as the owner answering "I had none". The three ways
    /// a reply can fail to arrive are: the owner does not have Vaettir; the owner disconnected
    /// mid-request; or ownership moved and the new owner never saw the message. None of them
    /// can duplicate an item, because none of them removed one.
    ///
    /// The first of those three is <b>completely silent on both machines</b>, and that is worth
    /// stating because it sends people looking for a log line that is never written.
    /// ZRoutedRpc.HandleRoutedRPC's unregistered-method path is
    /// "if (m_functions.TryGetValue(hash, out var value)) value.Invoke(...); return;" - there
    /// is no else and no warning. The "Failed to find rpc method" line lives in
    /// ZNetView.HandleRoutedRPC, which is the ZDO-TARGETED branch this design deliberately does
    /// not use, so it is not reached here at all. The only signal anybody gets is the timeout
    /// on this side, which is why Tick logs one under Verbose naming the container. Vaettir
    /// registers with Core at Requirement.Everyone, so on a gated server this cannot arise.
    ///
    /// The one case that can lose material is a reply that was sent and not received - the
    /// owner removed the items and the package went nowhere. Valheim's routed RPCs ride an
    /// ordered, reliable socket, so that means the connection dropped; on reconnect the chest's
    /// real contents are re-read from the server and the loss is the items that were in flight.
    /// It is written down rather than defended against: acknowledging the reply would need a
    /// third leg and a place for the owner to hold the items in the meantime, which is a
    /// transaction log for a convenience feature.
    /// </summary>
    internal static class HodWithdraw
    {
        /// <summary>
        /// Prefixed with the mod, because a routed RPC name is a global namespace shared with
        /// every mod in the process - ZRoutedRpc keys m_functions on name.GetStableHashCode()
        /// and Add throws on a collision, which would take the game's own Game.Start down with
        /// it.
        ///
        /// <b>Deliberately NOT Hirsla's names.</b> The fold is not merged: Hirsla is still its
        /// own repository, still in both build lists and still deployed, so the two can be in
        /// one process today. Sharing a name there would be a guaranteed collision on whichever
        /// of the two registered second - caught, but with the request path dead for the rest of
        /// the session. Different names mean the two pairs simply do not see each other. That
        /// does NOT make running both safe: see HodRuntime.Conflicts, which refuses to run the
        /// bench service beside a mod that counts the same chests.
        /// </summary>
        private const string RequestRpc = "Vaettir_HodWithdraw";

        private const string ResponseRpc = "Vaettir_HodWithdrawResponse";

        /// <summary>
        /// Bumped when the layout of either package changes.
        ///
        /// The first two fields of both packages - an int version, then a long correlation id -
        /// are FROZEN. That is what lets a mismatched build still answer instead of falling
        /// silent: the owner can read the id out of a package it otherwise cannot parse and
        /// reply "nothing", so the requester gets a refusal in one round trip rather than a
        /// timeout.
        ///
        /// Carried across from Hirsla at 2 rather than reset to 1. The number means "the layout
        /// this code speaks", and the layout is unchanged - resetting it would be inventing a
        /// second meaning for a field whose whole job is to be compared.
        /// </summary>
        private const int Wire = 2;

        /// <summary>
        /// The grid the removed items are carried in, and it must be the same number on both
        /// machines.
        ///
        /// Inventory.Save writes each item's m_gridPos and Inventory.Load feeds it straight back
        /// to AddItem(item, amount, x, y), which returns false - silently, its result ignored by
        /// Load - for any position outside the receiving inventory's width and height. A chest's
        /// own grid is up to 8x4 in vanilla and a modded container can be larger, so the parcel
        /// is not sized from the chest: the owner re-adds each removed stack into a parcel of
        /// this fixed size, which reassigns the positions, and the requester unpacks into a
        /// parcel of exactly the same size. 64 slots is far more than any one craft can ask for.
        /// </summary>
        private const int ParcelWidth = 8;

        private const int ParcelHeight = 8;

        // ------------------------------------------------------------------ the pending table

        private class Sent
        {
            public ZDOID Container;

            /// <summary>
            /// The peer the request was sent to, kept so a reply can be checked against it.
            ///
            /// Not decoration. A routed RPC can be invoked by ANY peer in the session against
            /// any other, so without this the response handler credits whatever arrives from
            /// whoever sends it - a peer running a modified client could write items straight
            /// into this character's inventory, and a player file is saved outside the ZDO
            /// system, so nothing on the chest side could ever take them back.
            ///
            /// <b>Be honest about how much this one field buys.</b> The sender id it is
            /// compared against is ZRoutedRpc.RoutedRPCData.m_senderPeerID, and that is read
            /// straight off the wire by Deserialize and relayed by RouteRPC without being
            /// rewritten - so it is a value the sender chooses, not one the transport attests.
            /// Against an accident - a stray reply, a second mod whose RPC name happens to share
            /// a stable hash, a reply about a different chest - this is a complete answer.
            /// Against a purpose-built client it is one of three facts that have to line up, and
            /// the one doing the real work is the correlation id, which is why
            /// <see cref="NextId"/> makes that random rather than a counter.
            /// </summary>
            public long Peer;

            public string Name;
            public int Quality;
            public int Amount;
            public float At;

            /// <summary>
            /// Set when the timeout passes. The entry is kept a while longer.
            ///
            /// Two separate jobs used to be one removal. Releasing the in-flight guard has to
            /// happen at the timeout, or one unanswered request shuts that chest out of that
            /// material for the session. Remembering that the id was real has to outlive it, or
            /// a reply that merely took its time cannot be told from an invented one - and a
            /// late reply must still be credited, because the items are already out of the
            /// chest and refusing them destroys somebody's stock to keep a tidy table.
            /// </summary>
            public bool Expired;
        }

        private static readonly Dictionary<long, Sent> Outstanding = new Dictionary<long, Sent>();

        /// <summary>
        /// A random id rather than a counter, and the difference is the whole of the forgery
        /// story.
        ///
        /// Uniqueness alone would be satisfied by 1, 2, 3 - the reply comes back to this client
        /// and is looked up in this client's own table, and it is never compared against
        /// anybody else's. What a counter also is, though, is <b>guessable</b>: the other two
        /// facts <see cref="OnResponse"/> checks are a peer id the sender writes itself and a
        /// container ZDOID every peer nearby already holds, so with an incrementing id the whole
        /// check is guessable, and the thing on the other side of it is an arbitrary Inventory
        /// blob written into a player file that lives outside the ZDO system and can never be
        /// reconciled against anything. Sixty-three bits of entropy inside a window of at most
        /// thirty-five seconds is not a proof, but it is the difference between "spray ids 1 to
        /// 200" and "brute force a long".
        ///
        /// System.Random.Next() yields 31 bits, hence two of them. The loop is for uniqueness
        /// within this client's own table, which is what the id is actually for; a collision is
        /// vanishingly unlikely and re-drawing costs nothing. Zero is skipped so an id is never
        /// confused with a default-initialised one on the failure paths.
        /// </summary>
        private static long NextId()
        {
            long id;

            do
            {
                id = ((long)Ids.Next() << 32) ^ (uint)Ids.Next();
            }
            while (id == 0L || Outstanding.ContainsKey(id));

            return id;
        }

        private static readonly System.Random Ids = new System.Random();

        private static bool _registered;

        /// <summary>
        /// How much of this material the pack has already promised away, summed over every
        /// request it is still possible to be handed.
        ///
        /// <see cref="Room"/> needs it. Sizing every request against the same untouched pack
        /// means two chests asked in one frame are each cleared for the whole of the free space,
        /// and both replies then arrive into room for one of them - at which point the surplus
        /// goes on the floor, out of somebody else's chest.
        ///
        /// <b>Expired entries count here, and that asymmetry is the fix for a real hole.</b> A
        /// single predicate used to answer both this and "is a request still live", so the
        /// moment a request timed out its share of the pack was handed straight back - while the
        /// entry itself was deliberately kept alive because a late reply must still be credited.
        /// Twenty wood could therefore be reserved, released at the timeout, reserved again by a
        /// second request, and then both replies arrive: the first fills the pack and the second
        /// lands on the floor. The rule that removes the hole is simply that a request reserves
        /// room for exactly as long as this client is still willing to credit it, which is until
        /// <see cref="Tick"/> forgets the entry.
        ///
        /// What it costs is stated plainly because it is real: while an unanswered request is
        /// still remembered it holds room a near-full pack may want for a different chest, so a
        /// session where some chest owners are not running Vaettir can see a fetch refused for
        /// up to RequestTimeout plus <see cref="RememberAfterTimeout"/> seconds. That is the
        /// trade this file has already made twice - a craft declining is a better failure than a
        /// chest emptying itself onto the ground.
        /// </summary>
        private static int Reserved(string name, int quality)
        {
            var total = 0;

            foreach (var sent in Outstanding.Values)
            {
                if (sent.Quality != quality || sent.Name != name) continue;

                total += sent.Amount;
            }

            return total;
        }

        /// <summary>
        /// Whether this exact withdrawal is already on its way.
        ///
        /// The re-entrancy protection, and it is not optional. The prefetch fires from
        /// InventoryGui.OnCraftPressed, which a player can press again the very next frame - and
        /// without this the second press would ask for a second copy of material the first press
        /// has already had removed. The owner would clamp the second request to what is left, so
        /// nothing duplicates; what it costs is the chest being drained twice over into a pack
        /// for one craft.
        ///
        /// Keyed on container, name and quality rather than on the container alone, so a recipe
        /// wanting wood and resin out of one chest still gets both in one round trip.
        /// </summary>
        public static bool Pending(ZDOID container, string name, int quality)
        {
            foreach (var sent in Outstanding.Values)
            {
                // An expired entry is kept only so a late reply can still be recognised. It is
                // no longer in flight, and holding the guard shut on it is the exact failure the
                // timeout exists to prevent.
                if (sent.Expired) continue;

                if (sent.Container != container) continue;
                if (sent.Quality != quality) continue;
                if (sent.Name != name) continue;

                return true;
            }

            return false;
        }

        /// <summary>Forget every outstanding request. For a world change.</summary>
        public static void Forget()
        {
            Outstanding.Clear();

            // Not _registered. That flag belongs to the ZRoutedRpc instance, which ZNet.Awake
            // builds fresh per session, and it is cleared by the Game.Start prefix that
            // registers into the new one.
        }

        // ------------------------------------------------------------------ registration

        /// <summary>
        /// Registers the pair, once per session, from a postfix on Game.Start.
        ///
        /// Game.Start is where the game itself registers its routed RPCs, so ZRoutedRpc.instance
        /// is certainly alive there - Game.Start's own first line uses it unguarded. ZNet.Awake
        /// builds a new ZRoutedRpc per session and Game is rebuilt with it, so one Game.Start is
        /// one registration and there is no duplicate to guard against.
        ///
        /// The try/catch is not decoration. m_functions.Add THROWS on a duplicate key, and this
        /// runs inside a Harmony postfix on Game.Start - an exception here would propagate out
        /// of the game's own startup and break the world load, for a convenience feature. A
        /// failed registration costs the request path and nothing else: the fast path still
        /// works, so singleplayer and a listen host are untouched.
        /// </summary>
        public static void Register()
        {
            if (_registered) return;

            var rpc = ZRoutedRpc.instance;
            if (rpc == null) return;

            try
            {
                rpc.Register<ZDOID, ZPackage>(RequestRpc, OnRequest);
                rpc.Register<ZDOID, ZPackage>(ResponseRpc, OnResponse);
                _registered = true;

                if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                    GrovePlugin.Log.LogInfo("Hod withdrawal RPCs registered.");
            }
            catch (Exception e)
            {
                GrovePlugin.LogOnce(
                    "Could not register the hod jib's withdrawal RPCs (" + e.Message + "). "
                    + "Chests this client owns still work; chests owned by another player or by "
                    + "the server will report nothing.");
            }
        }

        /// <summary>Called when the session ends, so the next Game.Start registers again.</summary>
        public static void Unregister()
        {
            _registered = false;
            Outstanding.Clear();
        }

        // ------------------------------------------------------------------ asking

        /// <summary>
        /// Asks the owner of <paramref name="container"/> to remove up to
        /// <paramref name="amount"/> and send it here. Returns whether a request went out.
        ///
        /// It never removes anything and never credits anything. The items arrive later, in
        /// <see cref="Credit"/>, and the caller finds them in the pack when they do - which is
        /// why every consumer of this is a PREFETCH into the pack rather than a spend. There is
        /// no way to fit a round trip inside Player.ConsumeResources, which is synchronous and
        /// expects the material gone when it returns.
        /// </summary>
        public static bool Ask(Container container, Inventory pack, string name, int quality,
                               bool matchWorldLevel, int amount)
        {
            if (!HodConfig.Enabled.Value) return false;
            if (container == null || pack == null || amount <= 0) return false;
            if (string.IsNullOrEmpty(name)) return false;

            // Without the pair registered there is nothing on this machine listening for the
            // reply, so the request would leave and the answer would be dropped on arrival - and
            // the items would be gone from the chest with nowhere to go. Failing to ask is the
            // safe half of that.
            if (!_registered || ZRoutedRpc.instance == null) return false;

            var nview = HodChests.NetViewOf(container);
            if (nview == null || !nview.IsValid()) return false;

            var zdo = nview.GetZDO();
            if (zdo == null) return false;

            // The caller should have taken the fast path. Asking ourselves would work - a routed
            // RPC to your own peer id is delivered locally, without touching the network - but
            // it would route a synchronous removal through the whole packing and unpacking
            // machinery for nothing, and the items would leave the chest and come back through a
            // ZPackage.
            if (zdo.IsOwner()) return false;

            // An ownerless ZDO is not an error and it is not a refusal either: InvokeRPC sends
            // to zdo.GetOwner(), which is 0 for an unowned object, and 0 is ZRoutedRpc's
            // "everybody" - so the request would be broadcast to every peer in the game and
            // answered by none of them, because none of them owns it. It is a chest that nobody
            // has adopted yet; ZDOMan.ReleaseZDOS gives it an owner within two seconds of a
            // player standing near it, so the honest answer is to say nothing and let the next
            // press find it owned.
            if (!zdo.HasOwner())
            {
                if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                    GrovePlugin.Log.LogInfo(
                        "No owner for " + container.m_name + " yet; not asking for " + name + ".");

                return false;
            }

            if (Pending(zdo.m_uid, name, quality)) return false;

            amount = Mathf.Min(amount, Room(container, pack, name, quality, matchWorldLevel));
            if (amount <= 0) return false;

            var game = Game.instance;
            if (game == null) return false;

            var id = NextId();
            var owner = zdo.GetOwner();

            var request = new ZPackage();
            request.Write(Wire);
            request.Write(id);
            request.Write(game.GetPlayerProfile().GetPlayerID());
            request.Write(name);
            request.Write(quality);
            request.Write(matchWorldLevel);
            request.Write(amount);

            Outstanding[id] = new Sent
            {
                Container = zdo.m_uid,
                Peer = owner,
                Name = name,
                Quality = quality,
                Amount = amount,
                At = Time.time
            };

            ZRoutedRpc.instance.InvokeRoutedRPC(owner, RequestRpc, zdo.m_uid, request);

            if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo(
                    "Asked the owner of " + container.m_name + " for " + amount + " " + name
                    + " (request " + id + ").");

            return true;
        }

        /// <summary>
        /// How much of this the pack could actually take.
        ///
        /// Asked before the request goes out rather than after the items arrive, because the
        /// only thing that can be done with an item that will not fit is to drop it on the
        /// ground - see <see cref="Credit"/> - and a chest quietly emptying itself onto the
        /// floor is a worse failure than a craft declining.
        ///
        /// The maximum stack size and the world level are read off a matching stack in the
        /// container's own local replica. That replica can be a second out of date, so this is
        /// an estimate; it is an estimate of how much room there is, not of how much stock there
        /// is, and the two are independent.
        ///
        /// It is an estimate on the generous side for one more reason, and it is left that way
        /// knowingly: Inventory.FindFreeStackSpace matches on name and world level and IGNORES
        /// quality, while the AddItem that will land these items requires a quality match. Part
        /// filled stacks at another quality therefore count as room that does not exist. The
        /// consequence is bounded - a few items on the floor rather than in the pack - and the
        /// alternative is a second copy of vanilla's stacking rule to keep in step.
        ///
        /// What is NOT left generous is the double count that used to sit here. A craft asks
        /// several chests in one frame, and every one of those calls measured the same untouched
        /// pack: two requests were each cleared for the whole of the free space and both replies
        /// then arrived into room for one of them. Whatever is already on its way is subtracted
        /// first.
        /// </summary>
        private static int Room(Container container, Inventory pack, string name, int quality,
                                bool matchWorldLevel)
        {
            var store = container.GetInventory();
            if (store == null) return 0;

            var sample = HodChests.FirstAllowed(store, name, quality, matchWorldLevel);
            if (sample == null || sample.m_shared == null) return 0;

            var perSlot = Mathf.Max(1, sample.m_shared.m_maxStackSize);

            var room = pack.FindFreeStackSpace(name, sample.m_worldLevel)
                       + pack.GetEmptySlots() * perSlot;

            // Matched on name and quality, which is how the requests are keyed. A request at
            // quality -1 ("any level") and one at quality 1 can be for the same physical stacks
            // and are not netted against each other, so a player with both outstanding at once
            // would get a small over-estimate. Nothing asks at -1: crafting asks per level, so
            // every request in flight is keyed the same way and the netting is exact.
            return room - Reserved(name, quality);
        }

        // ------------------------------------------------------------------ answering

        /// <summary>
        /// The owner's side. Read the request, do the removal against live stock, answer with
        /// what really came out.
        ///
        /// Every refusal below answers rather than falling silent, and that is the point of the
        /// whole method: a requester that gets an immediate empty reply stops waiting and
        /// declines the craft in one round trip, where a requester that gets nothing sits on its
        /// timeout. The only path that says nothing is one where the reply itself cannot be
        /// addressed.
        ///
        /// The gate is applied here as well as on the requester, and deliberately. The owner is
        /// the authority on its own container, and on a dedicated server it is the machine
        /// running the host's BossBiomes. Two copies of the feature that disagree about which
        /// biome owns an item therefore produce a SHORT reply, which the requester handles
        /// correctly by declining the craft - not a duplication, and not a silent success.
        ///
        /// No show flight is started from here, and that is not an omission. This runs on the
        /// machine that owns the CHEST, which is frequently not the machine at whose bench the
        /// craft is happening - it has no bench to fly to and no business drawing an effect for
        /// somebody else's craft.
        /// </summary>
        private static void OnRequest(long sender, ZDOID containerId, ZPackage package)
        {
            var id = 0L;

            // Both declared out here so the catch can reach them, and that is not tidiness. They
            // used to live inside the try, which meant a throw ANYWHERE after the removal -
            // inside Inventory.Save walking m_customData, inside PutBack's ItemDrop.DropItem,
            // inside another mod's patch on AddItem - answered "nothing" while the stacks were
            // already out of the chest and referenced by nothing. They were destroyed, on
            // somebody else's machine, with one warning that did not name a count. The method's
            // own contract is that every refusal moves nothing; this is what makes that true.
            List<ItemDrop.ItemData> harvested = null;
            Container container = null;

            try
            {
                if (package == null) return;

                var wire = package.ReadInt();
                id = package.ReadLong();

                // The frozen prefix earns its keep here. The rest of the package cannot be
                // trusted, but the id can, so the requester gets a refusal instead of silence.
                if (wire != Wire)
                {
                    GrovePlugin.LogOnce(
                        "A hod withdrawal request arrived on wire version " + wire + "; this "
                        + "build speaks " + Wire + ". Refusing it. Somebody in this session is "
                        + "running a different build of Vaettir.");

                    Answer(sender, containerId, id, null, null);
                    return;
                }

                var playerID = package.ReadLong();
                var name = package.ReadString();
                var quality = package.ReadInt();
                var matchWorldLevel = package.ReadBool();
                var amount = package.ReadInt();

                container = Find(containerId);

                // Not ours, gone, or refused by the access rules. All three answer "nothing",
                // which moves nothing and tells the requester to stop waiting. The ownership
                // re-check is not paranoia: ZDOMan.ReleaseZDOS moves ownership every two
                // seconds, so a request can genuinely arrive at a machine that owned the chest
                // when it was sent and does not now.
                if (container == null || !HodChests.OwnerMayServe(container, playerID, sender))
                {
                    Answer(sender, containerId, id, null, null);
                    return;
                }

                harvested = new List<ItemDrop.ItemData>();
                HodChests.TakeFromOwned(
                    container, name, quality, matchWorldLevel, amount, harvested);

                // Answer nulls each entry as it deals with it - packed into the parcel, or
                // handed back - so whatever is left non-null afterwards is what it never got to.
                // On the normal path that is nothing at all. This is what lets the catch below
                // repair a throw without any risk of returning a stack twice: a stack that was
                // partly packed and partly returned is already accounted for and its slot is
                // already null.
                Answer(sender, containerId, id, harvested, container);

                // A courtesy, not a repair. The removal bumped the ZDO's DataRevision, so this
                // only moves a send that was going to happen anyway to the front of the queue -
                // which is what stops the requester's own count reporting stock it has just
                // spent for the next second. ForceSendZDO is ShouldSend-filtered, so it can
                // never push a ZDO backwards and it cannot mend a fork; there is nothing in
                // vanilla that can.
                var man = ZDOMan.instance;
                if (man != null) man.ForceSendZDO(sender, containerId);
            }
            catch (Exception e)
            {
                // Never let this throw. It runs off a network message, inside the game's own RPC
                // dispatch, and an exception here lands in Player.log rather than in BepInEx's
                // LogOutput - so it would present as the network quietly breaking.
                GrovePlugin.LogOnce(
                    "A hod withdrawal request could not be served (" + e.Message + "). "
                    + "Answering with nothing.");

                // Anything already out of the chest goes back before the refusal is sent.
                // Without this the requester is told "I had none" while the material is gone
                // from the machine that owns it - a silent destruction, and the only one in this
                // file that is entirely local to the owner.
                //
                // This walks the whole list with no try around it because PutBack no longer
                // throws - it has its own repair and ends in a warning that names the count. It
                // used to be wrapped in one try, which meant a single stack that could not be
                // returned abandoned every stack after it in the list.
                //
                // The stacks Answer had already taken responsibility for are NOT here - it nulls
                // each entry as it packs it - and they are not lost either: Answer drains its own
                // parcel back into the chest before it lets an exception out.
                if (harvested != null)
                    for (var i = 0; i < harvested.Count; i++) PutBack(container, harvested[i]);

                try { Answer(sender, containerId, id, null, null); }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Packs whatever came out and sends it back to the one peer that asked.
        ///
        /// The items go through a temporary Inventory and vanilla's own Inventory.Save rather
        /// than a hand-written item writer. That is not laziness: an ItemData carries
        /// durability, quality, variant, crafter id and name, a custom-data dictionary, the
        /// world level and the picked-up flag, and a mod that rebuilds an item from its prefab
        /// name on the way past is quietly rewriting the player's property. Save and Load are
        /// the pair the game uses for a chest's own contents, so they carry all of it and they
        /// stay right through an update.
        ///
        /// <paramref name="source"/> is where anything that will not fit in the parcel goes
        /// back, and it is not a theoretical case. The parcel is 64 slots; a vanilla chest is at
        /// most 32, but a modded container can be bigger, and a request for everything in one
        /// would harvest more stacks than the parcel can hold. Without the return path those
        /// stacks would already have been removed from the chest and would simply not be in the
        /// reply - destroyed, quietly, and only on somebody else's machine.
        ///
        /// <b>The parcel is repaired here and cannot be repaired anywhere else.</b> The loop
        /// nulls each entry of <paramref name="harvested"/> the instant it takes responsibility
        /// for it, so from that line until the message leaves, those stacks are reachable only
        /// through a local variable of this method. OnRequest's catch walks the harvested list,
        /// finds it all null, returns nothing and answers "I had none" - while the whole parcel
        /// is off both the list and the chest. The throw sources that repair was written for are
        /// precisely the ones inside this window: Inventory.Save walking m_customData is reached
        /// by parcel.Save, and Container.Save is reached by PutBack. So everything from the first
        /// pack to the last byte written runs inside its own try, whose handler drains the parcel
        /// back into the chest and then lets the exception carry on to OnRequest.
        ///
        /// The send itself is deliberately OUTSIDE that try, and that is the one boundary in the
        /// file that is a judgement rather than a proof. Once InvokeRoutedRPC has been entered
        /// the parcel may already be serialised and handed to a peer's ZRpc, so putting the items
        /// back after a throw from inside it would risk delivering them twice - and a duplicate
        /// is the one failure this whole file exists to make impossible. Everything that can
        /// throw is done before that call; a throw from the call itself is treated as a delivery.
        ///
        /// One residue is left knowingly, and it is a single stack. If parcel.AddItem itself
        /// throws part way through - which needs another mod patched into it, since vanilla's own
        /// body cannot - that stack is off the harvested list, partly merged into the parcel and
        /// partly still in its own m_stack, and no reading of it is trustworthy. The merged half
        /// is drained back with the rest; the remainder is not put back, because putting back a
        /// count that may already be in the parcel is how a duplicate is made.
        /// </summary>
        private static void Answer(long peer, ZDOID containerId, long id,
                                   List<ItemDrop.ItemData> harvested, Container source)
        {
            var parcel = new Inventory("hod", null, ParcelWidth, ParcelHeight);
            var rpc = ZRoutedRpc.instance;
            ZPackage response;
            var amount = 0;

            try
            {
                if (harvested != null)
                {
                    for (var i = 0; i < harvested.Count; i++)
                    {
                        var item = harvested[i];
                        if (item == null) continue;

                        // Struck off the list the moment it is this method's problem.
                        // OnRequest's catch walks whatever is left and puts it back, so an entry
                        // that has been half packed and half returned must not still be sitting
                        // there.
                        harvested[i] = null;

                        var stack = item.m_stack;

                        // AddItem reassigns m_gridPos inside the parcel, which is exactly what
                        // is wanted - the position it arrived with belongs to the chest's grid
                        // and may not exist in the parcel's. It can also partially fail on a full
                        // parcel, leaving the remainder in m_stack, so the amount reported is
                        // what really went in and the rest goes home.
                        if (!parcel.AddItem(item))
                        {
                            stack -= item.m_stack;
                            PutBack(source, item);
                        }

                        amount += stack;
                    }
                }

                // The session ended between the request arriving and the answer being packed.
                // The removal has happened and cannot be unwound safely - the requester may or
                // may not hear about it - so the honest thing is to put it back.
                if (rpc == null)
                {
                    Drain(parcel, source);
                    return;
                }

                response = new ZPackage();
                response.Write(Wire);
                response.Write(id);
                response.Write(amount);
                parcel.Save(response);
            }
            catch (Exception)
            {
                Drain(parcel, source);
                throw;
            }

            rpc.InvokeRoutedRPC(peer, ResponseRpc, containerId, response);
        }

        /// <summary>
        /// Empties a packed parcel back into the chest it came out of.
        ///
        /// ToArray first, because PutBack's AddItem mutates the parcel's own list and iterating
        /// it while it changes would skip entries - which on this path means leaving items in a
        /// throwaway Inventory that is about to be garbage.
        /// </summary>
        private static void Drain(Inventory parcel, Container source)
        {
            try
            {
                var stranded = parcel.GetAllItems().ToArray();
                for (var i = 0; i < stranded.Length; i++) PutBack(source, stranded[i]);
            }
            catch (Exception e)
            {
                // Reached only if GetAllItems itself fails, since PutBack does not throw. There
                // is nothing further to try; say so rather than unwinding silently through a
                // handler whose whole job was to stop a silent loss.
                GrovePlugin.LogOnce(
                    "Could not return a packed hod withdrawal to its chest (" + e.Message
                    + "). Those items are lost.");
            }
        }

        /// <summary>
        /// Returns a stack that was removed and then could not be sent.
        ///
        /// Straight back into the container it came from, which is legal because this only ever
        /// runs on the owner - the write goes through Container.OnContainerChanged and is saved.
        /// The chest cannot be full, since the stack came out of it a moment ago and nothing has
        /// been put in since; the floor is there for the case that says otherwise, because
        /// between "items on the ground beside the chest" and "items gone" there is no argument.
        ///
        /// <b>It does not throw.</b> Every one of its callers is already on a repair path -
        /// OnRequest's catch, Answer's catch, the parcel-overflow line, the no-session line - and
        /// a repair that can itself fail halfway is not a repair. Each step is guarded separately
        /// so a failure falls through to the next one - chest, then ground - and the last line
        /// names the count rather than losing it silently.
        /// </summary>
        private static void PutBack(Container source, ItemDrop.ItemData item)
        {
            if (item == null || item.m_stack <= 0) return;

            try
            {
                if (source != null)
                {
                    var store = source.GetInventory();
                    if (store != null && store.AddItem(item)) return;
                }
            }
            catch (Exception e)
            {
                // Container.OnContainerChanged and Inventory.Save run off that AddItem, and
                // either can be patched by another mod. Fall through to the ground.
                GrovePlugin.LogOnce(
                    "Putting " + Describe(item) + " back into a chest threw (" + e.Message
                    + "); trying the ground instead.");
            }

            try
            {
                if (source != null && item.m_dropPrefab != null && item.m_stack > 0)
                {
                    ItemDrop.DropItem(item, item.m_stack,
                        source.transform.position + Vector3.up * 0.5f, Quaternion.identity);

                    return;
                }
            }
            catch (Exception e)
            {
                GrovePlugin.LogOnce(
                    "Dropping " + Describe(item) + " beside a chest threw (" + e.Message + ").");
            }

            GrovePlugin.LogOnce(
                "Could not return " + Describe(item) + " to a chest after a withdrawal that "
                + "would not fit in one reply, and they cannot be dropped. They are lost.");
        }

        /// <summary>
        /// "12 $item_wood", for a warning about material that has gone missing.
        ///
        /// The count is the half that matters and m_shared can be null on an item that has been
        /// through a failed rebuild, so it is read defensively - a log line about a loss is not a
        /// place to risk a second exception.
        /// </summary>
        private static string Describe(ItemDrop.ItemData item)
        {
            if (item == null) return "nothing";

            var name = item.m_shared == null || string.IsNullOrEmpty(item.m_shared.m_name)
                ? "item(s)"
                : item.m_shared.m_name;

            return item.m_stack + " " + name;
        }

        /// <summary>
        /// The container behind a ZDOID, or null.
        ///
        /// GetComponentInChildren rather than GetComponent, because a cart's or a ship's hold
        /// hangs its Container off a child of the object carrying the ZNetView - that is what
        /// Container.m_rootObjectOverride means, read from the other end.
        /// </summary>
        private static Container Find(ZDOID id)
        {
            var man = ZDOMan.instance;
            if (man == null) return null;

            var zdo = man.GetZDO(id);
            if (zdo == null) return null;

            var scene = ZNetScene.instance;
            if (scene == null) return null;

            var nview = scene.FindInstance(zdo);
            if (nview == null) return null;

            return nview.GetComponentInChildren<Container>();
        }

        // ------------------------------------------------------------------ crediting

        /// <summary>
        /// The requester's side. Unpack what the owner really sent and put it in the pack.
        ///
        /// Only the reply is credited. Nothing anywhere assumed the request would succeed, so a
        /// short answer is not an error to recover from - it is simply less material in the pack,
        /// and the craft that wanted it declines on its own.
        ///
        /// <b>And only a reply this client asked for.</b> That check was missing in Hirsla's
        /// first version and its absence was the one place where items could be created out of
        /// nothing. A routed RPC may be invoked by any peer in the session against any other, so
        /// an unvalidated handler here means anyone running a modified client - or any second mod
        /// that happens to register a name with the same stable hash - can hand this character an
        /// arbitrary Inventory blob, and Credit will materialise it out of real prefabs and the
        /// next autosave will write it into the player file, which lives outside the ZDO system
        /// and cannot be reconciled against anything. Three facts have to line up: the
        /// correlation id was issued here, the reply came from the peer that id was sent to, and
        /// it is about the container that id was about.
        ///
        /// <b>What those three facts are worth, exactly.</b> Two of them are not secrets. The
        /// container ZDOID is held by every peer with that chest loaded, and the sender id is
        /// ZRoutedRpc.RoutedRPCData.m_senderPeerID, which Deserialize reads off the wire and
        /// RouteRPC relays without rewriting - so it is chosen by whoever sends the packet, not
        /// attested by the transport. The only unguessable fact of the three is the correlation
        /// id, which is why <see cref="NextId"/> draws it at random over sixty-three bits. Against
        /// a stray or crossed reply this check is complete. Against a purpose-built client it is
        /// a cost, not a wall.
        ///
        /// The late reply is still honoured, which is why the entry outlives the timeout rather
        /// than being deleted by it - see <see cref="Sent.Expired"/>. Refusing a slow answer
        /// would destroy stock that has already left the chest.
        /// </summary>
        private static void OnResponse(long sender, ZDOID containerId, ZPackage package)
        {
            try
            {
                if (package == null) return;

                var wire = package.ReadInt();
                var id = package.ReadLong();

                Sent sent;
                if (!Outstanding.TryGetValue(id, out sent))
                {
                    // Two ways to get here and only one of them is anybody's fault. An answer
                    // that took longer than RequestTimeout plus RememberAfterTimeout - well over
                    // half a minute - has outlived the evidence that the id was ever real, and by
                    // then it cannot be told from an invented one, so it is refused. The other
                    // way is a peer sending a reply to a request that was never made. Both are
                    // named, because an operator reading this line needs to know which one they
                    // are looking at.
                    GrovePlugin.LogOnce(
                        "A hod withdrawal reply arrived for a request this client has no record "
                        + "of. Ignored. Either it is more than "
                        + (Mathf.Max(0.5f, HodConfig.RequestTimeout.Value) + RememberAfterTimeout)
                        + "s late, or somebody in this session is sending replies to requests "
                        + "nobody made.");
                    return;
                }

                if (sent.Peer != sender || sent.Container != containerId)
                {
                    GrovePlugin.LogOnce(
                        "A hod withdrawal reply came from the wrong peer or named the wrong "
                        + "container. Ignored.");
                    return;
                }

                Outstanding.Remove(id);

                if (wire != Wire)
                {
                    GrovePlugin.LogOnce(
                        "A hod withdrawal reply arrived on wire version " + wire + "; this build "
                        + "speaks " + Wire + ". Anything it was carrying is lost.");
                    return;
                }

                var amount = package.ReadInt();
                if (amount <= 0) return;

                var parcel = new Inventory("hod", null, ParcelWidth, ParcelHeight);
                parcel.Load(package);

                // Inventory.Load rebuilds each item through ObjectDB.GetItemPrefab and DISCARDS
                // the ones it cannot resolve - it logs "Failed to find item prefab" and ignores
                // its own AddItem result. Those items are already out of the chest on the owner's
                // machine, so they are simply gone. It needs two builds that disagree about what
                // items exist, which a mixed-mod session provides: the owner picks stacks by
                // m_shared.m_name, a localisation token that a modded prefab can also wear. Say
                // so, rather than letting a short delivery read as a short chest.
                var arrived = parcel.NrOfItemsIncludingStacks();
                if (arrived < amount)
                    GrovePlugin.LogOnce(
                        "A hod withdrawal reply said " + amount + " item(s) and only " + arrived
                        + " could be rebuilt here. The rest name prefabs this game does not have "
                        + "and they are lost. Check that both machines run the same mods.");

                Credit(parcel);
            }
            catch (Exception e)
            {
                GrovePlugin.LogOnce(
                    "A hod withdrawal reply could not be read (" + e.Message + "). Anything it "
                    + "was carrying is lost.");
            }
        }

        /// <summary>
        /// Hands the arrived items to the player.
        ///
        /// The reply is credited even when its request is no longer in the table, and that is
        /// deliberate rather than sloppy. The items are already gone from the chest - the owner
        /// removed them before it answered - so refusing a late reply would destroy somebody's
        /// stock to keep a bookkeeping invariant. A reply that outlives its request is a slow
        /// round trip, not a fraud.
        ///
        /// Anything that will not fit is dropped at the player's feet rather than deleted.
        /// <see cref="Room"/> sizes every request against what the pack can take, less whatever
        /// is already on its way, so this is uncommon rather than impossible - the pack can fill
        /// up during the round trip, and Room's stack-space estimate is quality-blind and
        /// therefore generous. Between "items on the floor" and "items gone" there is no
        /// argument.
        /// </summary>
        private static void Credit(Inventory parcel)
        {
            var player = Player.m_localPlayer;

            // No player: the world was left while the request was in flight. There is nowhere to
            // put them and nothing to be done, so say so loudly - this is the one path in the
            // feature that can lose material.
            if (player == null)
            {
                GrovePlugin.LogOnce(
                    "A hod withdrawal arrived with no local player to give it to. "
                    + parcel.NrOfItemsIncludingStacks() + " item(s) were lost.");
                return;
            }

            var pack = player.GetInventory();
            if (pack == null) return;

            // ToArray, because AddItem and RemoveItem both mutate the parcel's own list.
            var arrived = parcel.GetAllItems().ToArray();

            for (var i = 0; i < arrived.Length; i++)
            {
                var item = arrived[i];
                if (item == null) continue;

                var stack = item.m_stack;

                if (pack.AddItem(item))
                {
                    if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                        GrovePlugin.Log.LogInfo(
                            "Received " + stack + " " + item.m_shared.m_name + " from a chest.");

                    continue;
                }

                // AddItem can partially succeed - it tops up existing stacks and then fails to
                // find a slot - and it leaves the remainder in m_stack when it does.
                if (item.m_stack <= 0) continue;

                if (item.m_dropPrefab == null)
                {
                    GrovePlugin.LogOnce(
                        "Could not fit " + item.m_stack + " " + item.m_shared.m_name
                        + " and it has no prefab to drop, so it was lost.");
                    continue;
                }

                ItemDrop.DropItem(item, item.m_stack,
                    player.transform.position + Vector3.up * 0.5f, Quaternion.identity);

                player.Message(MessageHud.MessageType.Center, "$inventory_full");
            }
        }

        // ------------------------------------------------------------------ timeouts

        /// <summary>
        /// Drops requests nothing ever answered. Called once a frame from HodRuntime.
        ///
        /// A timeout credits nothing, because nothing is credited until a reply says so. All it
        /// does is release the in-flight guard so the next craft may ask again - without it, a
        /// single unanswered request would shut that chest out of that material for the rest of
        /// the session, which reads exactly like the gate having closed.
        ///
        /// Two stages rather than one, and the second is what lets a late reply still be told
        /// from an invented one. At the timeout the entry is marked expired, which releases the
        /// guard; the record itself is kept for a while longer so <see cref="OnResponse"/> can
        /// still recognise the id, the peer and the container if the answer turns up after all.
        /// Only then is it forgotten, and after that a reply carrying that id is refused - by
        /// then the id is no longer evidence of anything.
        ///
        /// Expiry releases the in-flight guard and NOT the pack room the request reserved. The
        /// two used to be one act, which is what let a timed-out request's twenty wood be
        /// promised to a second request and then both replies arrive - see
        /// <see cref="Reserved"/>. Room is held for exactly as long as the reply would still be
        /// credited, which is the line below, not the line above it.
        /// </summary>
        public static void Tick()
        {
            if (Outstanding.Count == 0) return;

            var limit = Mathf.Max(0.5f, HodConfig.RequestTimeout.Value);
            var now = Time.time;

            Expired.Clear();

            foreach (var entry in Outstanding)
            {
                var age = now - entry.Value.At;

                if (age > limit + RememberAfterTimeout)
                {
                    Expired.Add(entry.Key);
                    continue;
                }

                if (age <= limit || entry.Value.Expired) continue;

                entry.Value.Expired = true;

                if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                    GrovePlugin.Log.LogInfo(
                        "No reply about " + entry.Value.Amount + " " + entry.Value.Name
                        + " after " + limit + "s; nothing was credited and the material may be "
                        + "asked for again. The usual cause is that the player or server holding "
                        + "that chest is not running Vaettir, which is silent on both machines.");
            }

            for (var i = 0; i < Expired.Count; i++) Outstanding.Remove(Expired[i]);
        }

        /// <summary>
        /// How long past its timeout a request is still remembered, in seconds.
        ///
        /// Not a config entry, because it is not a taste: it is the width of the window in which
        /// a slow reply is still recognisably a reply. Long enough that a reply held up by a
        /// stalled frame or a saving server is credited rather than refused, short enough that
        /// the table cannot grow without bound in a session.
        /// </summary>
        private const float RememberAfterTimeout = 30f;

        /// <summary>Reused, because Tick runs every frame and the list is nearly always empty.</summary>
        private static readonly List<long> Expired = new List<long>();
    }
}
