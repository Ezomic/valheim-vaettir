using System;
using System.Collections.Generic;
using UnityEngine;
using Grove;
using Stow;

namespace Hod
{
    /// <summary>
    /// The spirit that flies from the chest to the bench when a craft spends something out of
    /// one.
    ///
    /// <b>It carries nothing, and the whole of this file is arranged so that it cannot.</b>
    /// That is the constraint the feature was designed around rather than a caveat attached to
    /// it, so it is worth stating exactly what makes it true instead of asserting that it is.
    ///
    /// The crafting is instant. The material leaves the chest inside
    /// <c>Inventory.RemoveItem</c>, in the same frame and on the same line as every other
    /// craft-from-container mod, and the recipe is paid for before anything here is called.
    /// <see cref="Send"/> runs AFTERWARDS, on a spend that has already happened. There is no
    /// moment in the design at which an item is in the air.
    ///
    /// Four separate things make it structurally impossible for a flight to move an item, and
    /// they are independent of each other:
    ///
    ///   1. <b>It is never handed one.</b> The only types crossing into this file are Vector3,
    ///      string and int. No ItemDrop.ItemData, no Inventory and no Container appears
    ///      anywhere in it - <see cref="Spent"/> is a position and a prefab NAME, captured
    ///      while the stack was still whole and carrying no route back to it. The stack it
    ///      names has already ceased to exist by the time the spirit is built.
    ///   2. <b>The thing it flies has no way to hold one.</b> Stow's Carrier takes cargo
    ///      through <c>Carry(GameObject)</c> and nothing else; its only other verbs are
    ///      Follow, Release and Dismiss. There is no inventory on it, no Container, no
    ///      ZNetView, and what hangs under the sling is a <see cref="CarriedItem"/> template -
    ///      a clone with its ItemDrop, ZNetView, colliders, rigidbody and pickup sparkle
    ///      stripped at build time, precisely so it is a mesh rather than an item.
    ///   3. <b>Nothing reads it.</b> The flight is a local GameObject on the crafting player's
    ///      own machine. It writes no ZDO, registers no prefab and is on no network, so there
    ///      is no other machine that could act on it and nothing to persist. It is destroyed
    ///      when it lands.
    ///   4. <b>It is not CarryRun.</b> Stow's real errand logic reserves stacks, re-finds them
    ///      by identity on arrival and writes them into a chest at the far end. None of it is
    ///      entered here: this file builds a Carrier straight from CarrierModel and drives one
    ///      arc with it. A flight that arrives does not deliver anything, because there is no
    ///      code path from it to a delivery.
    ///
    /// <b>Why it is not published through SpiritView.</b> The obvious way to let everybody see
    /// it is the one Stow already uses - write the trip to the post's ZDO and let every
    /// client's SpiritView draw it. That is wrong here for a mechanical reason rather than a
    /// taste one: CarryRun.Publish rewrites that same ZDO string from the post's OWNER at the
    /// end of every tick, from its own list of couriers, so a second writer would have its
    /// flights erased within a frame - or, worse, would erase a real courier's. The bench
    /// service also runs on the crafting client, which is frequently not the post's owner and
    /// therefore may not write that ZDO at all. So the show flight stays local, and the person
    /// who sees it is the person who pressed craft, which is the person it is explaining
    /// something to.
    /// </summary>
    internal static class HodShow
    {
        /// <summary>
        /// One thing that came out of one chest: where the chest is, and the prefab name of
        /// what left it.
        ///
        /// Both fields are readonly and both are value-like on purpose. This is the entire
        /// interface between the part of the feature that moves material and the part that
        /// draws a spirit, and it is deliberately too narrow to carry an item through.
        /// </summary>
        public struct Spent
        {
            public readonly Vector3 From;
            public readonly string Cargo;

            public Spent(Vector3 from, string cargo)
            {
                From = from;
                Cargo = cargo;
            }
        }

        /// <summary>
        /// A flight in progress: the spirit, and when it should go out.
        ///
        /// The deadline is kept here rather than on the Carrier because Carrier is a view with
        /// no notion of finishing - Stow's couriers are told when to leave by CarryRun, which
        /// this file deliberately does not enter.
        /// </summary>
        private class Run
        {
            public Carrier Spirit;
            public double EndsAt;
        }

        private static readonly List<Run> Flying = new List<Run>();

        /// <summary>
        /// How many show flights may be in the air at once.
        ///
        /// A multi-craft of twenty arrows spends out of the same chest twenty times in one
        /// frame, and a recipe with four ingredients in four chests does it four ways. Without
        /// a cap one press would fill the room with spirits, which is not the effect - the
        /// effect is "that came from over there". Six is enough to show a recipe drawing on
        /// several chests at once and few enough that they read as individuals.
        ///
        /// A constant rather than a config entry, and that is the honest place for it: it is
        /// not a preference about how much you want to see, it is a bound on a list. The
        /// preference is ShowFlight, which turns the whole thing off.
        /// </summary>
        private const int MaxFlying = 6;

        /// <summary>
        /// Sends a spirit for each chest a craft just spent out of.
        ///
        /// Collapsed per chest and per cargo before anything is built, because twenty arrows
        /// out of one chest is one journey with twenty items in it as far as a player watching
        /// is concerned, and twenty overlapping spirits is a fault rather than emphasis.
        ///
        /// Silent on every failure. Every branch that gives up here costs a visual effect, and
        /// a visual effect is the one thing in this folder whose absence cannot make anything
        /// wrong - the craft already happened, correctly, before this was called.
        /// </summary>
        public static void Send(List<Spent> spent)
        {
            if (spent == null || spent.Count == 0) return;
            if (!HodConfig.Enabled.Value || !HodConfig.ShowFlight.Value) return;
            if (!StowConfig.CarrierEnabled.Value) return;

            // <b>The whole of it inside one try, and this is load-bearing rather than
            // defensive habit.</b> Send is called from inside a Harmony PREFIX on
            // Inventory.RemoveItem, after the chests have already been debited and the amount
            // vanilla is about to remove has already been trimmed. An exception escaping from
            // here would propagate out of that prefix, so vanilla's own removal would never
            // run - the chest would be lighter, the pack would not be, and the craft would
            // fail somewhere up in DoCrafting. A visual effect would have caused a real loss
            // of material.
            //
            // So the file's promise that it is silent on every failure is made true here
            // rather than asserted. Everything this can cost is a spirit nobody sees, and the
            // craft it was a picture of has already happened, correctly, before the first
            // line of this method.
            try
            {
                Launch(spent);
            }
            catch (Exception e)
            {
                GrovePlugin.LogOnce(
                    "A hod jib's show flight could not be started (" + e.Message + "). The "
                    + "craft itself was unaffected - the spirit is only a picture of a spend "
                    + "that had already happened.");
            }
        }

        /// <summary>
        /// One spirit per chest-and-cargo pair in the batch, up to the cap.
        ///
        /// Split out only so <see cref="Send"/> can be one try block with nothing outside it.
        /// </summary>
        private static void Launch(List<Spent> spent)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // The bench, read at the moment of the spend rather than from the scope's cached
            // post. The spirit flies to the thing being crafted at, and that is the station -
            // the post is where the SPHERE is centred, which is a different question and the
            // one HodScope answers.
            var station = player.GetCurrentCraftingStation();
            if (station == null) return;

            var to = Above(station.transform, 0.9f, 1.0f);

            for (var i = 0; i < spent.Count; i++)
            {
                if (Flying.Count >= MaxFlying) return;

                var one = spent[i];
                if (string.IsNullOrEmpty(one.Cargo)) continue;

                // Already sent one from this chest with this cargo in this batch.
                if (SeenBefore(spent, i)) continue;

                Launch(one.From, to, one.Cargo);
            }
        }

        /// <summary>
        /// Whether an identical chest-and-cargo pair appears earlier in the same batch.
        ///
        /// A linear scan over a list that is a handful of entries long, which is cheaper than
        /// the HashSet it replaces and allocates nothing - this runs on the frame a craft is
        /// paid for, which is a frame that has already done real work.
        /// </summary>
        private static bool SeenBefore(List<Spent> spent, int index)
        {
            var one = spent[index];

            for (var i = 0; i < index; i++)
            {
                if (spent[i].Cargo != one.Cargo) continue;
                if ((spent[i].From - one.From).sqrMagnitude > 0.01f) continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds one spirit and puts it on one arc.
        ///
        /// The arc is CarryRun's, copied rather than called: the same cruise height, the same
        /// speed, and the same "measure the curve along itself rather than end to end" that
        /// stops two chests standing side by side firing the spirit across like a spark. Copied
        /// because reaching into CarryRun for it would mean reaching into the errand logic this
        /// file exists to stay out of, and because the numbers it reads are config entries both
        /// sides already share - so they cannot drift.
        /// </summary>
        private static void Launch(Vector3 from, Vector3 to, string cargo)
        {
            var spirit = CarrierModel.Build(from);

            // Null when the spirit meshes are not beside the dll. CarrierModel has already
            // said so once; there is nothing to add and nothing is broken by its absence.
            if (spirit == null) return;

            var lift = Mathf.Max(from.y, to.y) + Mathf.Max(0f, StowConfig.CarrierCruise.Value);
            var control = new Vector3((from.x + to.x) * 0.5f, lift, (from.z + to.z) * 0.5f);

            var speed = Mathf.Max(0.4f, StowConfig.CarrierSpeed.Value);
            var along = Vector3.Distance(from, control) + Vector3.Distance(control, to);
            var duration = Mathf.Max(0.35f, along / speed);

            var now = SpiritTrips.Now;

            spirit.Follow(new SpiritTrips.Flight
            {
                From = from,
                Control = control,
                To = to,
                Cargo = cargo,
                Start = now,
                Duration = duration
            });

            // A stripped display copy of the item's own model. Null for anything the game has
            // no model for, which Carry handles by carrying nothing - a spirit crossing the
            // room empty-handed is a worse picture than one with a crate, and a far better one
            // than an exception.
            spirit.Carry(CarriedItem.For(cargo));

            Flying.Add(new Run
            {
                Spirit = spirit,

                // The pause at the far end is Stow's own, so the spirit hovers over the bench
                // for the same beat a courier hovers over a chest before it goes. Without it
                // the arrival is invisible: the spirit reaches the bench and vanishes in the
                // same frame.
                EndsAt = now + duration + Mathf.Max(0f, StowConfig.CarrierPause.Value)
            });
        }

        /// <summary>
        /// Retires the flights that have landed. Called once a frame from HodRuntime.
        ///
        /// Walked backwards so a removal never skips the entry behind it. Dismiss shrinks the
        /// spirit out over FadeTime and destroys its own GameObject at the end, so nothing here
        /// has to own the destruction - which is what keeps this list a list of deadlines
        /// rather than a second lifetime manager.
        ///
        /// Returns immediately on the empty list, which is nearly every frame.
        /// </summary>
        public static void Tick()
        {
            if (Flying.Count == 0) return;

            var now = SpiritTrips.Now;

            for (var i = Flying.Count - 1; i >= 0; i--)
            {
                var run = Flying[i];

                // A spirit whose GameObject has gone - a world unloaded under it, most likely.
                // Plain null against a UnityEngine.Object, never ?., because Unity overloads ==
                // so a destroyed object compares equal to null and the null-propagating
                // operators walk straight past that overload.
                if (run == null || run.Spirit == null)
                {
                    Flying.RemoveAt(i);
                    continue;
                }

                if (now < run.EndsAt) continue;

                run.Spirit.Dismiss();
                Flying.RemoveAt(i);
            }
        }

        /// <summary>
        /// Sends every flight out at once. For a world change.
        ///
        /// A spirit is a plain local GameObject with no ZNetView, so a world load does not
        /// clear one - it would hang in the air over ground that no longer exists. Dismiss
        /// rather than Destroy so it goes out the way it would have anyway.
        /// </summary>
        public static void Clear()
        {
            for (var i = 0; i < Flying.Count; i++)
            {
                var run = Flying[i];
                if (run == null || run.Spirit == null) continue;

                run.Spirit.Dismiss();
            }

            Flying.Clear();
        }

        /// <summary>
        /// A point hanging over the top of a thing.
        ///
        /// Measured off the collider rather than assumed, for the same reason CarryRun measures
        /// it: a workbench, a forge and a black forge are all different heights, so flying to a
        /// fixed offset above the transform origin puts the spirit inside the tall ones and
        /// well over the short ones.
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
