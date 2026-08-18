using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Grove;

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
    internal class StowPost : MonoBehaviour, Hoverable
    {
        public const string Name = "stow_post";

        private static readonly List<StowPost> All = new List<StowPost>();

        private static GameObject _prefab;
        private static GameObject _holder;

        private Piece _piece;
        private Container _container;
        private CarryRun _run;
        private SpiritView _view;

        /// <summary>
        /// Registered with the ZNetScene that exists *now*, not "we built a prefab once".
        ///
        /// This distinction destroyed a post. Loading a second world - which includes
        /// logging out to the menu and back in - tears down ZNetScene and builds a new
        /// one, and the new one's m_namedPrefabs is rebuilt in Awake from its own
        /// serialised list. Our prefab was added to the *old* instance. Asking a static
        /// field whether we are ready then answers yes, Register early-returns, the new
        /// scene has never heard of stow_post, and ZNetScene discards every ZDO whose
        /// prefab name it cannot resolve - silently, and permanently.
        ///
        /// Vaettir's SpiritPrefab.Ready asks the live scene for exactly this reason.
        /// Anything cached across a world load has to be re-checked against the world.
        /// </summary>
        public static bool Ready
        {
            get
            {
                return ZNetScene.instance != null
                       && ZNetScene.instance.GetPrefab(Name) != null;
            }
        }

        public Container Container { get { return _container; } }

        private void Awake()
        {
            _piece = GetComponent<Piece>();
            _container = GetComponent<Container>();
            _run = new CarryRun(this);
            _view = new SpiritView(this);
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

        // ------------------------------------------------------------------ hover

        public string GetHoverName()
        {
            return _piece != null ? _piece.m_name : StowConfig.PostName.Value;
        }

        public string GetHoverText()
        {
            var name = GetHoverName();
            var waiting = _container != null && _container.GetInventory() != null
                ? _container.GetInventory().NrOfItems()
                : 0;

            // "Nowhere to go" is only true when nothing is being carried. During a run
            // the same count is mostly things that do have homes and are queued for a
            // trip, and a post reporting them as homeless while a spirit is visibly
            // ferrying them would be the mod contradicting itself on screen.
            string text;
            if (_run != null && _run.Working) text = name + " ( carrying " + waiting + " )";
            else if (waiting == 0) text = name;
            else text = name + " ( " + waiting + " with nowhere to go )";

            return Localization.instance.Localize(
                text + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_container_open");
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
        /// Idempotent, and safe to call every frame until it takes - where "it takes"
        /// means *against the current world*, not once per process.
        ///
        /// The early-out deliberately does not consult any static flag. Both AddToScene
        /// and AddToHammer check the live object they are about to write to, so calling
        /// them again on a world that already has the post costs two dictionary lookups
        /// and does nothing. Guarding them with a bool instead is what lost a post: the
        /// second world of a session was never told about the prefab, and discarded it.
        /// </summary>
        public static bool Register()
        {
            if (!StowConfig.PostEnabled.Value) return true;

            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;
            if (Ready && InHammer()) return true;

            if (_prefab == null)
            {
                _prefab = Build();
                if (_prefab == null) return false;
            }

            AddToScene();
            AddToHammer();
            return Ready;
        }

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
        private static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            if (_holder == null)
            {
                _holder = new GameObject("StowPostHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            var previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;

            GameObject clone;
            try { clone = Object.Instantiate(source, _holder.transform); }
            finally { ZNetView.m_forceDisableInit = previous; }

            clone.name = Name;
            clone.transform.localRotation = Quaternion.identity;

            var container = clone.GetComponent<Container>();
            if (container != null)
            {
                container.m_name = StowConfig.PostName.Value;
                container.m_width = Mathf.Clamp(StowConfig.PostWidth.Value, 1, 8);
                container.m_height = Mathf.Clamp(StowConfig.PostHeight.Value, 1, 4);

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

        private static void AddToScene()
        {
            var scene = ZNetScene.instance;
            if (_prefab == null || scene.GetPrefab(Name) != null) return;

            if (!scene.m_prefabs.Contains(_prefab)) scene.m_prefabs.Add(_prefab);

            try
            {
                var named = (Dictionary<int, GameObject>)
                    AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene);
                named[Name.GetStableHashCode()] = _prefab;
            }
            catch (System.Exception e)
            {
                StowRuntime.Log.LogError("Could not register " + Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// The hammer's piece table, for the ObjectDB that exists now.
        ///
        /// Asked of the table rather than remembered in a bool, for the same reason
        /// Ready asks the scene: ObjectDB is rebuilt per world, and a Hammer from the
        /// last one is a different object with a different list. Remembering meant the
        /// post left the build menu on the second world of a session.
        /// </summary>
        private static bool InHammer()
        {
            var table = HammerPieces();
            return table != null && _prefab != null && table.m_pieces.Contains(_prefab);
        }

        private static PieceTable HammerPieces()
        {
            if (ObjectDB.instance == null) return null;

            var hammer = ObjectDB.instance.GetItemPrefab("Hammer");
            var drop = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                return null;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            return table != null && table.m_pieces != null ? table : null;
        }

        private static void AddToHammer()
        {
            if (_prefab == null) return;

            var table = HammerPieces();
            if (table == null || table.m_pieces.Contains(_prefab)) return;

            table.m_pieces.Add(_prefab);

            // Logged on the add rather than on the call, or a per-frame retry that is
            // already satisfied would write a line every frame.
            StowRuntime.Log.LogInfo("Stowing post added to the hammer.");
        }
    }
}
