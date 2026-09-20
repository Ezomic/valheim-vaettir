using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Grove;
using Ezomic.Shared;

namespace Stow
{
    /// <summary>
    /// A crate you build in the storage room. Drop things in, close it, and they walk
    /// themselves to the chests that asked for them.
    ///
    /// The post is a real Container rather than a piece with a panel of its own, and that
    /// one decision removes most of the mod's surface. There is no list of your pack to
    /// tick through, no selection to remember, no keybind: the interface is the chest
    /// window you already know, and "which items" is answered by which items you dropped
    /// in. Dragging a stack into a crate is already how you say "put this away" in
    /// Valheim - this only changes where the crate puts it.
    ///
    /// Leftovers stay in the post rather than bouncing back to your pack. A post holding
    /// six things is a post telling you six things have no home yet, which is a more
    /// useful state than a silent failure and reads at a glance from the hover text.
    /// </summary>
    internal class StowPost : MonoBehaviour
    {
        public const string Name = "stow_post";

        private static readonly List<StowPost> All = new List<StowPost>();

        private Piece _piece;
        private Container _container;
        private CarryRun _run;
        private SpiritView _view;

        /// <summary>
        /// When this post came into the world, for PostSize. A shrink has to wait for the
        /// zone around it to finish loading, and there is no event for that.
        /// </summary>
        private float _awoke;

        public Container Container { get { return _container; } }

        private void Awake()
        {
            _piece = GetComponent<Piece>();
            _container = GetComponent<Container>();
            _run = new CarryRun(this);
            _view = new SpiritView(this);
            _awoke = Time.time;

            // Before anything else touches the inventory. Container.Awake has just built it
            // at the prefab's own size and its first load of the saved items is already
            // scheduled, so this is the last moment at which the grid can be widened
            // without an item being thrown away for being in a column that does not exist
            // yet. Whether there is actually a rail outside is not knowable this early and
            // does not need to be - Update settles it down within a few seconds, with the
            // items present and a spill path for anything in the way.
            PostSize.Open(_container);

            All.Add(this);
        }

        private void OnDestroy()
        {
            // The spirits are local objects and nothing owns them but this post, so a
            // post that goes away without saying so leaves lights hanging in the air over
            // a piece that is no longer there.
            if (_view != null) _view.Clear();
            if (_run != null) _run.Abandon();

            All.Remove(this);
        }

        /// <summary>
        /// Decides, then draws - in that order, and on every client.
        ///
        /// CarryRun bails immediately unless this client owns the post, so only one player
        /// is ever ferrying; SpiritView runs for everybody, including the owner. That is
        /// deliberate: a host that drew its own spirits by some other route would be the
        /// one client whose rendering nobody else ever exercised.
        ///
        /// The looking for work is what makes a post resume on its own. A run does not
        /// survive a reload - it is entirely in memory - but the items do, because they
        /// never left the post. Without it, a world quit halfway through a run would come
        /// back to a post full of things with homes, waiting for someone to open and close
        /// it before it carried on, which is indistinguishable from the mod breaking.
        /// </summary>
        private void Update()
        {
            if (_container == null) return;

            // Before the carrier, and outside the CarrierEnabled gate below: the size of
            // the post is a fact about the piece rather than about the ferrying, and a
            // player who has turned the spirits off still built a creel rail and still
            // expects the slots it paid for.
            Neighbours();
            PostSize.Apply(this, _container, _railed, _awoke);

            // Turned off while spirits are out. The owner stops publishing and every
            // client drops what it was drawing, so nothing is left hanging in the air.
            if (!StowConfig.CarrierEnabled.Value)
            {
                if (_run != null) _run.Abandon();
                if (_view != null) _view.Clear();
                return;
            }

            if (_run != null) _run.Tick();
            if (_view != null) _view.Tick();
        }

        /// <summary>Is this container one of ours? Asked on every chest scan.</summary>
        public static bool Is(Container container)
        {
            return container != null && container.GetComponent<StowPost>() != null;
        }

        // ------------------------------------------------------------------ upgrades

        /// <summary>
        /// The nearest post to a point, or null.
        ///
        /// Here rather than in PostUpgrades because the list of posts is this class's, and
        /// handing it out would be handing out something that can go stale. An upgrade asks
        /// this once a second; nothing else needs it yet.
        ///
        /// Placement ghosts are skipped. The translucent post following somebody's cursor
        /// is a real instance of the prefab with this component awake on it, and without
        /// the check an upgrade already built would re-point its motes at whatever a player
        /// happened to be holding - and, once the effects land, briefly change its own
        /// behaviour to serve a post that does not exist.
        /// </summary>
        internal static StowPost Nearest(Vector3 point, float range)
        {
            StowPost best = null;
            var bestSq = range * range;

            for (var i = 0; i < All.Count; i++)
            {
                var post = All[i];
                if (post == null || !post.Placed) continue;

                var distance = (post.transform.position - point).sqrMagnitude;
                if (distance > bestSq) continue;

                bestSq = distance;
                best = post;
            }

            return best;
        }

        /// <summary>
        /// A built post rather than a ghost. Asked of the ZNetView each time instead of
        /// being latched in Awake - the answer is a live fact about this object, and a
        /// bool of ours is exactly the kind of thing that outlives what made it true.
        /// </summary>
        private bool Placed
        {
            get
            {
                var nview = GetComponent<ZNetView>();
                return nview != null && nview.GetZDO() != null;
            }
        }

        /// <summary>
        /// Whether this post currently has a given upgrade standing beside it.
        ///
        /// The one question the three effects ask, and it is answered from the world every
        /// time: build a perch and the post gains a courier in the same second, tear it
        /// down and it loses one. Nothing caches "this post is upgraded" anywhere, which is
        /// the only arrangement that cannot outlive the piece that justified it.
        /// </summary>
        public bool Has(UpgradeKind kind)
        {
            return PostUpgrades.Has(this, kind);
        }

        /// <summary>Seconds between asking the world what is standing beside this post.</summary>
        private const float NeighbourInterval = 0.5f;

        private bool _railed;
        private bool _perched;
        private float _nextNeighbours;

        /// <summary>
        /// Re-asks which upgrades are beside this post, twice a second.
        ///
        /// A sample of a live answer, which is a different thing from a remembered one and
        /// the distinction is the whole reason this is safe. Nothing writes it down: it is
        /// not on the ZDO, it does not survive the component, and a post loaded into a new
        /// world starts with both of these false and works them out again from the pieces
        /// that are actually there. The most a torn-down rail can buy anybody is half a
        /// second of a post that has not noticed yet.
        ///
        /// Asked on a timer rather than every frame because three effects want the answer
        /// and two of them - the inventory size and the courier count - are checked from an
        /// Update. Each call walks the placed upgrades in the loaded zones; that is a
        /// handful of pieces and it is cheap, but it is not free, and nothing here changes
        /// at sixty hertz.
        /// </summary>
        private void Neighbours()
        {
            if (Time.time < _nextNeighbours) return;
            _nextNeighbours = Time.time + NeighbourInterval;

            _railed = Has(UpgradeKind.Rail);
            _perched = Has(UpgradeKind.Perch);
        }

        /// <summary>
        /// How many items this post's spirit carries in one trip.
        ///
        /// Per post rather than per world since the creel rail: the number used to be read
        /// straight out of config wherever it was needed, which is exactly the shape that
        /// cannot express "this post, the one with the rail beside it".
        ///
        /// Taken as the larger of the two so a rail can only ever help. Zero is the special
        /// case and it means "the whole stack, however large" - so a post already set to
        /// carry everything cannot be improved on, and a rail configured to zero beats any
        /// finite base.
        /// </summary>
        public int ItemsPerTrip
        {
            get
            {
                var basic = StowConfig.ItemsPerTrip.Value;
                if (!_railed) return basic;

                if (basic <= 0) return basic;

                var railed = PostUpgrades.RailItemsPerTrip.Value;
                return railed <= 0 ? railed : Mathf.Max(basic, railed);
            }
        }

        /// <summary>
        /// How many spirits this post flies at once - two with a perch, one without.
        ///
        /// The clamp stays where it was, in CarryRun, because it is a fact about the
        /// ferrying rather than about the piece.
        /// </summary>
        public int Couriers
        {
            get
            {
                var basic = StowConfig.Couriers.Value;
                if (!_perched) return basic;

                return Mathf.Max(basic, PostUpgrades.PerchCouriers.Value);
            }
        }

        // ------------------------------------------------------------------ hover

        /// <summary>
        /// The bracketed status the post adds to its hover line, or null when it has
        /// nothing to say.
        ///
        /// This is NOT reached through Hoverable, and the difference cost a bug report.
        /// The post carries a Container, Container implements Hoverable itself, and
        /// Hud.UpdateCrosshair resolves the text with GetComponentInParent<Hoverable>()
        /// - which returns the FIRST such component on the object. Container is on the
        /// donor prefab and this is added afterwards, so Container always won and the
        /// GetHoverText that used to live here was dead code that never ran once. The
        /// post looked like an ordinary chest, which is exactly when it must not: a post
        /// full of items with no home is then indistinguishable from a mod doing nothing.
        ///
        /// So the words are appended by StowRuntime's postfix on Container.GetHoverText,
        /// which is the seam that actually gets called, and this only supplies them.
        /// </summary>
        public string StatusLine()
        {
            var waiting = _container != null && _container.GetInventory() != null
                ? _container.GetInventory().NrOfItems()
                : 0;

            // "Nowhere to go" is only true when nothing is being carried. During a run
            // the same count is mostly things that do have homes and are queued for a
            // trip, and a post reporting them as homeless while a spirit is visibly
            // ferrying them would be the mod contradicting itself on screen.
            if (_run != null && _run.Working) return "carrying " + waiting;
            if (waiting == 0) return null;

            // Counted, not assumed. This used to report `waiting` - everything in the post -
            // as having nowhere to go, which is a different question from the one the words
            // ask. A post between runs holding one placeable stack said exactly what a post
            // holding one genuinely homeless stack says, and a player reported the mod as
            // broken on the strength of that sentence twice. Both times there was a real bug
            // underneath, and neither of them was the one this line was describing.
            var stuck = Depositor.Homeless(_container.GetInventory(), transform.position);

            if (stuck == 0)
                return waiting + " waiting";

            return stuck + " with nowhere to go"
                 + (stuck < waiting ? ", " + (waiting - stuck) + " waiting" : "");
        }

        // ------------------------------------------------------------------ emptying

        /// <summary>
        /// Runs when the window closes, which is the only moment that means "I am done
        /// putting things in".
        ///
        /// Emptying continuously would be worse in a way that is easy to miss: you would
        /// never be able to drop two half-stacks in and have them merge, because the first
        /// would be gone before you let go of the second.
        ///
        /// What "empty" means changed in 0.3. It used to be the whole transfer, here, in
        /// one frame; now it is the starting gun for a run that takes as long as it takes,
        /// and the work happens in Update. Closing the window is still the trigger - the
        /// trigger was never the part worth changing.
        /// </summary>
        public void Empty()
        {
            if (_container == null) return;

            var inventory = _container.GetInventory();
            if (inventory == null || inventory.NrOfItems() == 0) return;

            // Claimed here as well as at each delivery, because everything downstream
            // gives up the moment this post is somebody else's - and on a shared post the
            // person who closed the window is the one who should be doing the ferrying.
            var nview = GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            nview.ClaimOwnership();

            if (_run == null) return;

            if (StowConfig.CarrierEnabled.Value) _run.Wake();
            else _run.Instant(inventory);
        }

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Builds the post once. Registering it - into the scene, into the hammer's build
        /// menu, and into both again on every world load - is Ezomic.Shared.Prefabs' job.
        ///
        /// That split is not tidiness. This piece is the reason the shared file exists: a
        /// static "already registered" flag survived a world load here, the second world of
        /// a session was never told about stow_post, and ZNetScene discarded every ZDO whose
        /// prefab name it could not resolve. Silently, and permanently, on 2026-08-16.
        /// </summary>
        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            // Configured first, then a fallback, because a name that does not resolve is
            // skipped silently by the game and the piece would just never appear.
            foreach (var name in new[] { StowConfig.PostDonor.Value, "piece_chest_wood" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                StowRuntime.Log.LogWarning("Post donor '" + name + "' does not exist.");
            }

            return null;
        }

        /// <summary>
        /// A chest, kept as a chest.
        ///
        /// Stoker's hopper clones the same donor and then tears the Container out, because
        /// a bin that turns out to have an inventory is exactly the confusion it wanted to
        /// avoid. Here the inventory *is* the feature, so the donor is left almost intact
        /// and only its appearance and its label change.
        /// </summary>
        internal static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            var clone = Prefabs.Clone(source, Name);
            if (clone == null) return null;

            clone.transform.localRotation = Quaternion.identity;

            var container = clone.GetComponent<Container>();
            if (container != null)
            {
                container.m_name = StowConfig.PostName.Value;

                // The prefab's baseline only. Container.Awake reads these two fields once,
                // to build the inventory, and never again - so since the creel rail made
                // the size a property of one post rather than of the mod, what is written
                // here is just where a placed copy starts before PostSize sizes it from
                // what is standing beside it. Left in step with PostSize.Plain all the same:
                // a prefab claiming a different size from every instance of it is a lie
                // waiting to be read by the next person.
                container.m_width = PostSize.Plain.x;
                container.m_height = PostSize.Plain.y;

                // An empty post is the normal resting state - it has just done its job.
                // Inheriting a donor that tidies itself away would delete the piece every
                // time it succeeded.
                container.m_autoDestroyEmpty = false;
                container.m_privacy = Container.PrivacySetting.Public;
            }

            var piece = clone.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = StowConfig.PostName.Value;
                piece.m_description = "Drop things in and close it. They go to the chests "
                                      + "that asked for them.";
                piece.m_resources = Requirements(StowConfig.PostCost.Value);
                piece.m_category = Piece.PieceCategory.Furniture;

                // The clone arrives wearing piece_chest_wood's icon, so the Furniture tab
                // advertised the post as a wooden chest - the one thing that would make
                // you scroll past it. Left null on failure rather than blanked: a wrong
                // picture is bad, and an empty slot in the build menu is worse.
                var icon = Icons.Load(Icons.For(StowConfig.PostModelFile.Value), Name);
                if (icon != null) piece.m_icon = icon;
            }

            // The post's shape is its own. Nothing vanilla is grafted on - only the
            // *materials* are borrowed, group by group, so the mesh is ours and the
            // surfaces are the game's.
            if (!PostModel.Apply(clone))
                StowRuntime.Log.LogWarning(
                    "Stowing post is wearing the donor chest's own body - the model file "
                    + "was not found beside the dll.");

            var scale = StowConfig.PostScale.Value;
            clone.transform.localScale = new Vector3(scale, scale, scale);

            if (clone.GetComponent<StowPost>() == null) clone.AddComponent<StowPost>();

            StowRuntime.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        private static Piece.Requirement[] Requirements(string spec)
        {
            var list = new List<Piece.Requirement>();

            foreach (var entry in (spec ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                int amount;
                if (!int.TryParse(parts[1].Trim(), out amount) || amount <= 0) continue;

                var prefab = ObjectDB.instance.GetItemPrefab(itemName);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    StowRuntime.Log.LogWarning("Post cost mentions unknown item '" + itemName + "'.");
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,
                    m_recover = true
                });
            }

            return list.ToArray();
        }

    }
}
