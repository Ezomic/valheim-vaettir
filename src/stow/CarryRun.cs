using System.Collections.Generic;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// One post's ferrying: which spirits are out, what each is carrying, and what
    /// happens when one lands.
    ///
    /// The single rule everything here is built around: **the item never leaves the
    /// post until the trip lands**. A spirit carries a reservation and a picture, not
    /// cargo. Anything can interrupt a run - the zone unloads, the player logs out, the
    /// game is killed - and in every one of those cases the stack is still sitting in the
    /// post where it was dropped. Holding the ItemData in flight would be simpler by a
    /// dozen lines and would lose a stack of black metal to a crash, which is not a trade
    /// a convenience mod gets to make.
    ///
    /// The cost is that a stack is briefly in two places to look at: in the post's window
    /// and under a spirit. That is only visible if you reopen the post mid-run, and if you
    /// take the stack out while it is "in the air" the trip simply finds it gone on
    /// arrival and delivers nothing.
    ///
    /// Everything below runs on the client that owns the post - Empty claims ownership,
    /// and Tick gives up the moment that is no longer true. Two players closing two posts
    /// cannot both ferry the same chest's contents.
    ///
    /// It also draws nothing. This decides and publishes; SpiritView reads and renders, on
    /// every client including this one. Keeping the two apart is what made multiplayer
    /// cheap: the owner is not a special case that draws its spirits by some other route,
    /// so there is only one rendering path and it is the one everybody exercises.
    /// </summary>
    internal class CarryRun
    {
        /// <summary>
        /// How often a resting post looks for work, in seconds.
        ///
        /// A post with nothing to do costs nothing between these, and a post with
        /// something to do is in a run and not consulting this at all. It exists so a
        /// post resumes on its own after a reload: the items are still in it, they still
        /// have homes, and waiting for someone to open and close it again before it
        /// carries on would be a mod that quietly stopped working.
        /// </summary>
        private const float ScanInterval = 3f;

        /// <summary>How far above each end the spirit hovers.</summary>
        private const float PostHover = 0.45f;
        private const float ChestHover = 0.35f;

        /// <summary>
        /// What a spirit is out doing. Three errands, one flight pattern - out to a
        /// container, do the thing, come home - which is why they share a courier rather
        /// than each getting a loop of their own.
        /// </summary>
        private enum Errand
        {
            /// <summary>Post to chest, carrying. The original job.</summary>
            Stow,

            /// <summary>Post to chest empty, back carrying. Fetch, run backwards.</summary>
            Fetch,

            /// <summary>Chest to chest, correcting a rule someone broke.</summary>
            Tidy,

            /// <summary>Nothing to do, and the post says the spirit lives here.</summary>
            Resting
        }

        private sealed class Courier
        {
            public Errand Errand;
            public ItemDrop.ItemData Item;
            public Container Target;

            /// <summary>Where a fetch or a tidy is taking the stack from.</summary>
            public Container Source;

            public bool Outbound;

            /// <summary>The leg being flown, exactly as published.</summary>
            public SpiritTrips.Flight Flight;

            /// <summary>
            /// When the leg lands *and* has finished hovering. The pause is not published:
            /// a spirit holds at the end of its arc until the next leg replaces it, so
            /// waiting here is what the pause looks like from outside.
            /// </summary>
            public double ReadyAt;

            public bool Idle(double now) { return now >= ReadyAt; }
        }

        private readonly StowPost _post;
        private readonly List<Courier> _couriers = new List<Courier>();
        private readonly List<ItemDrop.ItemData> _reserved = new List<ItemDrop.ItemData>();
        private readonly List<Container> _touched = new List<Container>();

        private Transform _heartwood;
        private float _nextScan;
        private int _moved;

        public CarryRun(StowPost post)
        {
            _post = post;
        }

        public bool Working { get { return _couriers.Count > 0; } }

        /// <summary>Look for work now rather than at the next scan.</summary>
        public void Wake()
        {
            _nextScan = 0f;
        }

        /// <summary>
        /// Sends every spirit home and forgets the run.
        ///
        /// Called when the post is destroyed or ownership is lost. Nothing has to be put
        /// back, which is the reservation rule paying for itself - there is no state to
        /// unwind because nothing was ever taken.
        /// </summary>
        public void Abandon()
        {
            // Cheap when there was nothing to abandon. Tick calls this on every frame for
            // every client that does not own the post, which is most of them.
            if (_couriers.Count == 0 && _reserved.Count == 0 && _moved == 0) return;

            _couriers.Clear();
            _reserved.Clear();
            _touched.Clear();
            _moved = 0;

            Publish();
        }

        // ------------------------------------------------------------------ the loop

        public void Tick()
        {
            var container = _post != null ? _post.Container : null;
            var inventory = container != null ? container.GetInventory() : null;
            if (inventory == null) { Abandon(); return; }

            var nview = _post.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) { Abandon(); return; }

            var now = SpiritTrips.Now;

            for (var i = _couriers.Count - 1; i >= 0; i--)
            {
                var courier = _couriers[i];
                if (!courier.Idle(now)) continue;

                if (courier.Outbound)
                {
                    Land(courier, inventory, nview);
                    continue;
                }

                Arrive(courier, nview);
                if (!Dispatch(courier, inventory, now)) Retire(courier, i);
            }

            Recruit(inventory, now);
            Publish();

            // A resting spirit is still a courier, so "nothing out" is not the same as
            // "nothing happening" any more. The run is over when nothing is on an errand.
            foreach (var courier in _couriers)
                if (courier.Errand != Errand.Resting) return;

            // Anything held back above gets another chance next time - by then the player
            // may have emptied the chest that would not take it, and a permanent skip list
            // would mean a post that quietly refuses to move one stack for the session.
            _reserved.Clear();

            if (_moved > 0) Announce();
        }

        /// <summary>
        /// A spirit has reached its chest. Everything it decided on the way out is
        /// checked again before anything moves.
        ///
        /// All four of these can have changed during a trip that takes a couple of
        /// seconds: the player can take the stack back out of the post, the chest can be
        /// filled by somebody else, torn down, or walked into by a player who opens it.
        /// A delivery that trusted the plan it left with would be writing into a chest
        /// that no longer accepts writes.
        /// </summary>
        private void Land(Courier courier, Inventory inventory, ZNetView nview)
        {
            var item = courier.Item;
            var errand = courier.Errand;
            var source = courier.Source;
            var target = courier.Target;
            var landed = courier.Flight.To;

            courier.Outbound = false;

            // Held past the delivery only when a chest offers room and then takes none.
            // Everything else - a success, a stack somebody moved, a chest torn down or
            // opened - releases it, because those are all states a later trip could
            // reasonably find changed again.
            var keep = false;

            // A fetch is not finished here; it keeps its item and its chest for the way
            // home. The others are done and let go of both.
            var carrying = false;
            string cargo = null;

            // The post's own ZDO is written on two of these three errands, so ownership is
            // re-claimed here rather than trusted from whenever the run started.
            nview.ClaimOwnership();

            switch (errand)
            {
                case Errand.Stow:
                    keep = !Move(inventory, target, item, ref cargo, true);
                    break;

                case Errand.Fetch:
                    // Nothing moves here. The stack stays in the chest it is being
                    // fetched from until the spirit is actually home, so the post's count
                    // goes up when you watch it arrive rather than a trip earlier.
                    //
                    // The safety rule is untouched by that - the stack sits in a real
                    // container for every instant of the journey, just the far one rather
                    // than the near one. An interrupted fetch leaves it in the chest,
                    // which is exactly where it started.
                    carrying = Ready(source, item);
                    if (carrying) cargo = ItemGroups.PrefabNameOf(item);
                    break;

                case Errand.Tidy:
                    keep = !Move(Held(source), target, item, ref cargo, true);
                    break;
            }

            if (!carrying)
            {
                courier.Item = null;
                courier.Source = null;

                if (!keep && item != null) _reserved.Remove(item);
            }

            courier.Target = null;

            // Home again. A fetch shows what it is bringing back; the others fly empty.
            Launch(courier, landed, Home(), cargo, SpiritTrips.Now);
        }

        /// <summary>
        /// Is this fetch still worth flying home with?
        ///
        /// Asked at the chest, and asked again by Move when the spirit lands - both ends
        /// can change during the return leg and only the second answer is allowed to be
        /// trusted. This one exists to stop a spirit setting off carrying a picture of
        /// something that has already gone.
        /// </summary>
        private bool Ready(Container source, ItemDrop.ItemData item)
        {
            var from = Held(source);
            var into = _post.Container != null ? _post.Container.GetInventory() : null;

            return from != null && into != null && item != null
                   && from.ContainsItem(item)
                   && into.CanAddItem(item)
                   && Depositor.Usable(source, _post.transform.position,
                                       StowConfig.Range.Value);
        }

        /// <summary>
        /// A spirit has got home. Only a fetch has anything left to do here - it is the
        /// one errand whose delivery happens at this end.
        /// </summary>
        private void Arrive(Courier courier, ZNetView nview)
        {
            if (courier.Errand != Errand.Fetch || courier.Item == null) return;

            var item = courier.Item;
            var source = courier.Source;

            courier.Item = null;
            courier.Source = null;

            // The post's inventory is written to, so ownership is re-claimed rather than
            // trusted from whenever the errand started.
            nview.ClaimOwnership();

            string cargo = null;
            Move(Held(source), _post.Container, item, ref cargo, true);

            _reserved.Remove(item);
        }

        /// <summary>The inventory behind a container, or null if it has gone.</summary>
        private static Inventory Held(Container container)
        {
            return container == null ? null : container.GetInventory();
        }

        /// <summary>
        /// One stack from one inventory into one container, re-checked on arrival.
        ///
        /// Everything decided on the way out is asked again here. A trip takes seconds,
        /// and in seconds the stack can be taken, the chest filled by somebody else, torn
        /// down, or opened by a player - so a delivery that trusted its own plan would be
        /// writing into a container that no longer accepts writes.
        ///
        /// Returns false only for the one pathological case worth remembering: room was
        /// offered and nothing was taken.
        /// </summary>
        private bool Move(Inventory from, Container into, ItemDrop.ItemData item,
                          ref string cargo, bool celebrate)
        {
            if (from == null || into == null || item == null) return true;
            if (!from.ContainsItem(item)) return true;
            if (!Depositor.Usable(into, _post.transform.position, StowConfig.Range.Value))
                return true;

            cargo = ItemGroups.PrefabNameOf(item);

            bool emptied;
            var went = Depositor.Deposit(from, into, item, out emptied);

            if (went <= 0)
            {
                StowPlugin.Log.LogWarning(
                    "A container offered room for " + item.m_shared.m_name
                    + " and then accepted none - not trying it again this run.");
                return false;
            }

            _moved += went;
            if (!_touched.Contains(into)) _touched.Add(into);

            // The same little effect the game plays when you drop something into a chest,
            // over the chest, at the moment it actually arrives. This is the payoff for
            // the whole trip and it costs one line.
            if (celebrate && InventoryGui.instance != null
                && InventoryGui.instance.m_moveItemEffects != null)
                InventoryGui.instance.m_moveItemEffects.Create(
                    into.transform.position, Quaternion.identity);

            return true;
        }

        /// <summary>
        /// Gives an idle spirit its next errand, or false if there is none and it should
        /// go out.
        ///
        /// The order is the priority, and it is deliberate. Stowing first, because that is
        /// what you just asked for by closing the post. Fetching second, because it is a
        /// standing want rather than a fresh instruction. Tidying last, because it corrects
        /// something nobody asked about today - and a post that spent its spirits tidying
        /// while a full post sat waiting would feel broken even though every trip was
        /// useful.
        /// </summary>
        private bool Dispatch(Courier courier, Inventory inventory, double now)
        {
            var post = _post.Container;
            var origin = _post.transform.position;

            ItemDrop.ItemData item;
            Container target;
            Container source;

            if (Available(inventory)
                && Depositor.NextTrip(inventory, origin, _reserved, out item, out target))
            {
                Begin(courier, Errand.Stow, item, null, target,
                      AimAt(target), ItemGroups.PrefabNameOf(item), now, "carrying");
                return true;
            }

            if (Available(inventory)
                && Depositor.NextFetch(post, origin, _reserved, out item, out source))
            {
                // Out empty, home carrying. The cargo is published on the *return* leg,
                // which is set in Land - so a fetch looks exactly like a stow run played
                // backwards, which is what it is.
                Begin(courier, Errand.Fetch, item, source, null,
                      AimAt(source), null, now, "fetching");
                return true;
            }

            if (PostRules.Tidy(post)
                && Depositor.NextTidy(origin, _reserved, out item, out source, out target))
            {
                Begin(courier, Errand.Tidy, item, source, target,
                      AimAt(source), null, now, "tidying");
                return true;
            }

            // Nothing to do. A post told to keep its spirit sends it home to hover; any
            // other post lets it go out.
            if (PostRules.Where(post) != PostRules.Presence.LivesHere) return false;

            if (courier.Errand != Errand.Resting)
            {
                courier.Errand = Errand.Resting;
                courier.Item = null;
                courier.Source = null;
                courier.Target = null;
                courier.Outbound = false;

                Launch(courier, courier.Flight.To, Home(), null, now);
            }

            // Re-asked on the next scan rather than every frame, so a resting post is not
            // running three planners per frame for the sake of a spirit that is asleep.
            courier.ReadyAt = now + ScanInterval;
            return true;
        }

        /// <summary>
        /// Sets a courier on an errand and puts it in the air.
        ///
        /// It takes off from wherever it already is rather than always from the post - a
        /// spirit that has just landed at a chest and been given a tidy job two chests
        /// over should fly there, not teleport home first.
        /// </summary>
        private void Begin(Courier courier, Errand errand, ItemDrop.ItemData item,
                           Container source, Container target, Vector3 aim, string cargo,
                           double now, string verb)
        {
            courier.Errand = errand;
            courier.Item = item;
            courier.Source = source;
            courier.Target = target;
            courier.Outbound = true;

            _reserved.Add(item);

            var from = courier.Flight.Duration > 0f ? courier.Flight.To : Home();
            Launch(courier, from, aim, cargo, now);

            if (StowConfig.Verbose.Value)
                StowPlugin.Log.LogInfo(verb + " " + item.m_stack + "x "
                                       + item.m_shared.m_name);
        }

        /// <summary>Adds spirits up to the configured count, but only if there is work.</summary>
        private void Recruit(Inventory inventory, double now)
        {
            var wanted = Mathf.Clamp(StowConfig.Couriers.Value, 1, 8);
            if (_couriers.Count >= wanted) return;

            // Cheap checks before the timer, not after. An idle post is the resting state
            // of every post in the world, so that state costs a couple of reads per frame
            // and never touches the clock or the physics scene.
            var post = _post.Container;
            var idle = inventory.NrOfItems() == 0
                       && PostRules.Fetch(post).Count == 0
                       && !PostRules.Tidy(post)
                       && PostRules.Where(post) != PostRules.Presence.LivesHere;

            if (idle) return;

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + ScanInterval;

            // Dispatch is the one that knows what errands exist, so it decides whether
            // there is anything worth a spirit. Asking here as well would be a second copy
            // of that priority order, free to disagree with the first.
            var courier = new Courier { ReadyAt = now };
            _couriers.Add(courier);

            if (!Dispatch(courier, inventory, now)) _couriers.Remove(courier);
        }

        private void Retire(Courier courier, int index)
        {
            if (courier.Item != null) _reserved.Remove(courier.Item);
            _couriers.RemoveAt(index);
        }

        /// <summary>
        /// Whether new stacks may be picked up at all.
        ///
        /// A post someone currently has open is left alone: they are mid-drag, and having
        /// a spirit lift the stack they are reaching for out from under them is the
        /// storage equivalent of someone tidying your desk while you are working at it.
        /// Trips already in the air still land - the item is checked on arrival anyway.
        /// </summary>
        private bool Available(Inventory inventory)
        {
            if (inventory.NrOfItems() == 0) return false;

            var container = _post.Container;
            return container == null || !container.IsInUse();
        }

        // ------------------------------------------------------------------ fallbacks

        /// <summary>The old behaviour: everything at once, no spirit.</summary>
        public void Instant(Inventory inventory)
        {
            int chests;
            var moved = Depositor.Distribute(inventory, _post.transform.position, out chests);

            _moved = moved;
            _touched.Clear();
            Announce(chests);
        }

        // ------------------------------------------------------------------ telling

        private void Announce()
        {
            Announce(_touched.Count);
        }

        private void Announce(int chests)
        {
            var moved = _moved;
            _moved = 0;
            _touched.Clear();

            if (!StowConfig.Messages.Value || Player.m_localPlayer == null) return;

            var container = _post.Container;
            var inventory = container != null ? container.GetInventory() : null;
            var left = inventory != null ? inventory.NrOfItems() : 0;

            string message;
            if (moved == 0)
                message = left > 0 ? "Nothing here has a home yet." : "";
            else
                message = "Stowed " + moved + " item" + (moved == 1 ? "" : "s")
                          + " into " + chests + " chest" + (chests == 1 ? "" : "s") + ".";

            if (left > 0 && moved > 0)
                message += " " + left + " left waiting.";

            if (message.Length == 0) return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize(message), 0, null);
        }

        // ------------------------------------------------------------------ flights

        /// <summary>
        /// Sets a courier on a leg and works out when it will have landed and finished
        /// hovering.
        ///
        /// The arc is measured along itself rather than end to end. Two chests standing
        /// side by side are 40cm apart in a straight line but the control point can be
        /// well above both, so timing the hop off the straight distance fires the spirit
        /// across like a spark.
        /// </summary>
        private void Launch(Courier courier, Vector3 from, Vector3 to, string cargo,
                            double now)
        {
            var lift = Mathf.Max(from.y, to.y) + Mathf.Max(0f, StowConfig.CarrierCruise.Value);
            var control = new Vector3((from.x + to.x) * 0.5f, lift, (from.z + to.z) * 0.5f);

            var speed = Mathf.Max(0.4f, StowConfig.CarrierSpeed.Value);
            var along = Vector3.Distance(from, control) + Vector3.Distance(control, to);
            var duration = Mathf.Max(0.35f, along / speed);

            courier.Flight = new SpiritTrips.Flight
            {
                From = from,
                Control = control,
                To = to,
                Cargo = cargo,
                Start = now,
                Duration = duration
            };

            courier.ReadyAt = now + duration + Mathf.Max(0f, StowConfig.CarrierPause.Value);
        }

        /// <summary>
        /// Puts the current legs on the post's ZDO, for every client to draw.
        ///
        /// Called once at the end of each Tick rather than at each change, so a frame that
        /// lands one spirit and launches another writes one string instead of two.
        /// SpiritTrips.Publish drops a write that would not change anything, which is what
        /// keeps a post with a spirit in mid-flight off the network entirely.
        /// </summary>
        private void Publish()
        {
            var nview = _post != null ? _post.GetComponent<ZNetView>() : null;
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            var flights = new List<SpiritTrips.Flight>();
            foreach (var courier in _couriers) flights.Add(courier.Flight);

            SpiritTrips.Publish(nview, SpiritTrips.Encode(flights));
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// Where a spirit is born and waits between trips: at the heartwood, if the model
        /// has one.
        ///
        /// PostModel puts a marker on the `core` group's centre, so this lands on the lump
        /// itself whatever the model does with it. The collider-top fallback is what this
        /// used to do unconditionally, and on the canopy it is wrong by three quarters of
        /// a metre and on the far side of a roof - the spirit appeared well above its own
        /// source, which undid the whole reason for putting a heartwood on the post.
        /// </summary>
        private Vector3 Home()
        {
            if (_heartwood == null)
                _heartwood = _post.transform.Find("post_visual/" + PostModel.HeartwoodAnchor);

            return _heartwood != null
                ? _heartwood.position
                : Above(_post.transform, PostHover, 1.0f);
        }

        /// <summary>Just above the chest's lid, which is where you would put something.</summary>
        private static Vector3 AimAt(Container container)
        {
            return Above(container.transform, ChestHover, 0.5f);
        }

        /// <summary>
        /// A point hanging over the top of a thing.
        ///
        /// Measured off the collider rather than assumed, because a chest, a post and a
        /// black metal chest are all different heights - flying to a fixed offset above
        /// the transform origin puts the spirit inside the tall ones and well over the
        /// short ones.
        /// </summary>
        private static Vector3 Above(Transform thing, float clearance, float fallback)
        {
            var position = thing.position;
            var collider = thing.GetComponentInChildren<Collider>();

            var top = collider != null ? collider.bounds.max.y : position.y + fallback;
            return new Vector3(position.x, top + clearance, position.z);
        }
    }
}
