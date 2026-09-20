// No `using System` here: this file leans on UnityEngine.Object throughout, and
// importing System makes every bare `Object` ambiguous with System.Object. The two
// places that want a System type name it in full.
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Grove;
using Ezomic.Shared;

namespace Stow
{
    /// <summary>
    /// Which upgrade a placed piece is.
    ///
    /// An enum rather than three component types, because everything about them is the
    /// same except the sentence in the hover text and the effect somebody else applies:
    /// they are all "a thing built on the ground beside a stowing post". Three classes
    /// would be three copies of the link effect, the nearest-post search and the
    /// redundancy check, which is how Kynda's two upgrades nearly ended up.
    /// </summary>
    internal enum UpgradeKind
    {
        /// <summary>Creel rail - the post holds more and its spirit carries more.</summary>
        Rail = 0,

        /// <summary>Spirit perch - a second courier flies from the post.</summary>
        Perch = 1,

        /// <summary>Hod jib - the crafting panel reaches the chests around the post.</summary>
        Jib = 2,
    }

    /// <summary>One upgrade piece: what it is called, what it costs, what it wears.</summary>
    internal sealed class UpgradeDef
    {
        /// <summary>
        /// Permanent from the first one built in any world. ZNetScene keys on
        /// <c>name.GetStableHashCode()</c> and saved ZDOs store that hash, so renaming one
        /// of these destroys every copy already standing - silently, because an
        /// unresolvable ZDO is discarded rather than errored. stow_rail, stow_perch and
        /// hod_jib are therefore settled here and never become config.
        ///
        /// The jib is hod_ rather than stow_ because the thing it turns on is the bench
        /// service, which is its own feature with its own name. The piece still serves a
        /// stowing post like the other two.
        /// </summary>
        public string PrefabName;

        public UpgradeKind Kind;

        /// <summary>
        /// Whether the model carries a `core` group - a heartwood - to be lit.
        ///
        /// Not a decoration flag: it is what makes the perch and the jib cost a heartwood
        /// honestly. You can see the thing you paid for sitting in the piece, lit, from
        /// across the room. The rail has no core group and asks for no heartwood, which is
        /// why it is the cheap one.
        /// </summary>
        public bool Heartwood;

        /// <summary>
        /// The build menu's description: what the piece is, then what it does. Two
        /// sentences, because the menu is where the decision to build it is made and there
        /// is room there.
        /// </summary>
        public string Blurb;

        /// <summary>
        /// The same claim, cut to the half that is worth repeating on a piece already
        /// standing in the world.
        ///
        /// Separate from Blurb rather than reused, because the hover line is read over the
        /// top of whatever you are doing and is competing with a crosshair: by the time it
        /// has said what the piece is made of and which post it feeds, the sentence that
        /// answers "what is this one for" has scrolled off the reader's attention.
        /// </summary>
        public string Effect;

        // Bound in Bind, below. Null until then, which is why nothing here reads them at
        // type-init time: a static initializer that throws takes every method on the type
        // with it, and this type is reached from an Update.
        public ConfigEntry<string> Name;
        public ConfigEntry<string> Cost;
        public ConfigEntry<string> Model;

        public GameObject Prefab;

        /// <summary>
        /// Whether this piece's recipe has been written out of items that all resolved.
        /// False until it has, and rechecked on every world - see Reprice.
        /// </summary>
        public bool Priced;
    }

    /// <summary>
    /// The three pieces you build beside a stowing post to change what it does.
    ///
    /// The shape is vanilla's, not an invention: a chopping block stands beside a
    /// workbench and a Tun beside a smelter, and in both cases the upgrade is an ordinary
    /// buildable piece that finds its station by standing near it. That is worth copying
    /// for one reason above the others - there is nothing to learn. A player who has built
    /// a chopping block already knows how a creel rail works, including that moving the
    /// post breaks the pair and that the motes tell you which one it belongs to.
    ///
    /// What this file owns is the frame: the prefabs, the recipes, the link to a post and
    /// the question "does this post have one of these". The three effects live with the
    /// features they change - the rail's bigger inventory and longer trip with the post,
    /// the perch's second courier with CarryRun, the jib's bench service in its own
    /// runtime - because each of those is a change to somebody else's rules, and a file
    /// that both defines a piece and rewrites three unrelated systems is a file nobody can
    /// read.
    /// </summary>
    internal static class PostUpgrades
    {
        public static readonly UpgradeDef Rail = new UpgradeDef
        {
            PrefabName = "stow_rail",
            Kind = UpgradeKind.Rail,
            Heartwood = false,
            Blurb = "Stowing post improvement. Room to set things down, and a creel for "
                    + "the spirit. The post holds more and its spirit carries more in one "
                    + "trip.",
            Effect = "the post holds more, and carries more in a trip",
        };

        public static readonly UpgradeDef Perch = new UpgradeDef
        {
            PrefabName = "stow_perch",
            Kind = UpgradeKind.Perch,
            Heartwood = true,
            Blurb = "Stowing post improvement. A second heartwood, housed and lit. Two "
                    + "spirits fly from the post instead of one.",
            Effect = "two spirits fly from the post",
        };

        public static readonly UpgradeDef Jib = new UpgradeDef
        {
            PrefabName = "hod_jib",
            Kind = UpgradeKind.Jib,
            Heartwood = true,
            // The gate is named in the build menu on purpose, and it is the only one of the
            // three blurbs that has to explain a limit. A player who spends a heartwood on
            // this, stands at a bench and finds nothing served has no way to tell "the jib is
            // broken" from "nothing in that chest comes from a biome you have earned" - and
            // the first thing they would do is take it down again. One clause up front is
            // cheaper than the bug report.
            Blurb = "Stowing post improvement. A heartwood on an arm, over the bench. A "
                    + "crafting station near the post can count the chests around it - but "
                    + "only for materials from a biome whose boss is dead.",
            Effect = "a bench near the post crafts from its chests",
        };

        public static readonly UpgradeDef[] All = { Rail, Perch, Jib };

        public static UpgradeDef For(UpgradeKind kind)
        {
            foreach (var def in All) if (def.Kind == kind) return def;
            return null;
        }

        // ------------------------------------------------------------------ config

        /// <summary>
        /// How far an upgrade may stand from the post it serves.
        ///
        /// Five metres, which is vanilla's own answer: StationExtension.m_maxStationDistance
        /// is 5f on every extension that ships, and that is the distance a player already
        /// has in their hands from building chopping blocks. Deliberately NOT the sorting
        /// Range - that one is about how far a spirit will fly to a chest, and this one is
        /// about what counts as "beside".
        /// </summary>
        public static ConfigEntry<float> Range;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> ShowLink;

        /// <summary>
        /// What the rail and the perch are actually worth, in the units the thing they
        /// change is measured in.
        ///
        /// Here rather than in the Carrier and Post sections next to the numbers they
        /// raise, and that is a deliberate reversal of where you would first reach for
        /// them. The base numbers describe a post; these describe an upgrade, and a player
        /// deciding whether to spend a heartwood on a perch is reading this section rather
        /// than hunting through three others for the one line that says what they get.
        /// </summary>
        public static ConfigEntry<int> RailWidth;
        public static ConfigEntry<int> RailHeight;
        public static ConfigEntry<int> RailItemsPerTrip;
        public static ConfigEntry<int> PerchCouriers;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Upgrades", "UpgradesEnabled", true,
                "Add the three post upgrades to the hammer.\n"
                + "This gates the BUILD MENU only. The prefabs are declared either way, and "
                + "that is not an oversight: a prefab ZNetScene cannot name has its ZDOs "
                + "discarded, silently and permanently, so a host turning this off would "
                + "otherwise demolish every rail, perch and jib standing in the world on "
                + "everyone's next load. Turning it off stops new ones being built and "
                + "leaves what is built alone.");

            Range = config.Bind("Upgrades", "UpgradeRange", 5f,
                "How far an upgrade may stand from the post it serves, in metres. Five is "
                + "vanilla's own figure for a chopping block beside a workbench, so it is "
                + "a distance the game has already taught. An upgrade serves the NEAREST "
                + "post inside this, and exactly one post - look at the piece and the motes "
                + "show you which.");

            Scale = config.Bind("Upgrades", "UpgradeScale", 1f,
                "Scale of all three pieces. One figure rather than three, because they are "
                + "modelled to stand together beside one post and sizing them apart is how "
                + "a set stops reading as a set.");

            ShowLink = config.Bind("Upgrades", "ShowLink", true,
                "Draw the run of motes from an upgrade to its post while you look at it. "
                + "This is the game's own station-extension effect, borrowed rather than "
                + "imitated, and it is how the piece answers \"which post is this feeding\" "
                + "without a word of text.");

            Rail.Name = config.Bind("Upgrades", "RailName", "Creel rail",
                "What the creel rail is called.");

            Rail.Cost = config.Bind("Upgrades", "RailCost",
                "FineWood:10,IronNails:10,LeatherScraps:8",
                "What the creel rail costs, as Item:Amount pairs.\n"
                + "No heartwood: this is joinery, not a second spirit, and it is the one "
                + "upgrade a player should be able to build the same evening they build the "
                + "post. Fine wood and nails put it at exactly the post's own tier - the "
                + "post is FineWood:20,IronNails:20 - and the woven part is LeatherScraps "
                + "because that is this game's cordage: it is what the cart and the leather "
                + "armour are strapped with, it is in the chest by the time anyone has "
                + "nails, and it does not drag a storage-room accessory out to the Plains "
                + "the way LinenThread would. An item name that does not resolve is logged "
                + "and skipped, which makes the piece cheaper rather than unbuildable - "
                + "read the log after editing this.");

            Rail.Model = config.Bind("Upgrades", "RailModel", "stow_rail.obj",
                "The hand-built mesh the creel rail wears, beside the dll. Its .col sidecar "
                + "and its _icon.png are picked up from the same stem automatically.");

            RailWidth = config.Bind("Upgrades", "RailWidth", 8,
                "Slots across a post that has a creel rail beside it, instead of PostWidth. "
                + "Eight is the width of your own pack, which is the widest grid the "
                + "container window is built to draw.\n"
                + "LOWERING THIS TAKES SLOTS AWAY from every railed post in the world. "
                + "Nothing in them is lost - whatever is in a slot that stops existing is "
                + "moved to a free one if there is a free one, and dropped at the post's "
                + "feet if there is not, the same way a chest spills when you break it - but "
                + "it does happen the moment the world loads and it does happen quietly, so "
                + "empty your posts before editing this downwards.");

            RailHeight = config.Bind("Upgrades", "RailHeight", 3,
                "Slots down a post that has a creel rail, instead of PostHeight. The same "
                + "warning as RailWidth applies to lowering it.\n"
                + "Three rather than four because the post is meant to stay a table you "
                + "pass things over rather than become the storage it is there to fill - a "
                + "post that holds as much as a chest is a chest.");

            RailItemsPerTrip = config.Bind("Upgrades", "RailItemsPerTrip", 20,
                "How many items the spirit of a railed post carries in one trip, instead of "
                + "ItemsPerTrip. Double, which is the point of the creel: the same stack "
                + "leaves in half the trips and a post that serves a wall of chests stops "
                + "being something you stand and wait for.\n"
                + "Taken as the larger of this and ItemsPerTrip, so an upgrade can never "
                + "make a post slower. 0 means the whole stack in one go, and a base "
                + "ItemsPerTrip of 0 is already that, so a rail adds nothing there.");

            PerchCouriers = config.Bind("Upgrades", "PerchCouriers", 2,
                "How many spirits a post with a spirit perch flies at once, instead of "
                + "Couriers. Two, because that is what the piece is: one more heartwood, "
                + "housed and lit, and one more spirit living in it.\n"
                + "Taken as the larger of this and Couriers. Raising it past two works and "
                + "is a config change rather than a second perch - the piece is once-only, "
                + "so a second one beside the same post does nothing.");

            Perch.Name = config.Bind("Upgrades", "PerchName", "Spirit perch",
                "What the spirit perch is called.");

            Perch.Cost = config.Bind("Upgrades", "PerchCost",
                "GroveHeartwood:1,FineWood:10,IronNails:6",
                "What the spirit perch costs. The heartwood is the whole price and the rest "
                + "is the stand it sits on: a second courier is a second spirit, and a "
                + "spirit needs somewhere to live - which is the same argument that makes "
                + "the post itself cost one. You can see it in the piece, lit, which is why "
                + "it is not simply an expensive plank.");

            Perch.Model = config.Bind("Upgrades", "PerchModel", "stow_perch.obj",
                "The hand-built mesh the spirit perch wears.");

            Jib.Name = config.Bind("Upgrades", "JibName", "Hod jib",
                "What the hod jib is called.");

            Jib.Cost = config.Bind("Upgrades", "JibCost",
                "GroveHeartwood:1,FineWood:15,IronNails:10",
                "What the hod jib costs. A heartwood again, and for the same reason: "
                + "something has to be carrying the material from the chest to the bench, "
                + "and this is the arm it swings from. Dearer than the perch in ordinary "
                + "materials because it is the upgrade that changes how crafting itself "
                + "behaves.");

            Jib.Model = config.Bind("Upgrades", "JibModel", "hod_jib.obj",
                "The hand-built mesh the hod jib wears.");
        }

        // ------------------------------------------------------------------ the link

        /// <summary>
        /// Does this post have one of these right now?
        ///
        /// The whole reason the rest of the work can stay out of this file. An effect asks
        /// the question at the moment it needs the answer and gets one computed from what
        /// is standing in the world, so building a rail changes the post in the same frame
        /// and tearing it down changes it back - with nothing anywhere holding a "this post
        /// is upgraded" flag that could outlive the piece that justified it.
        ///
        /// Cheap enough to call from an Update or a UI repaint: it walks the placed
        /// upgrades, which is a handful per loaded zone, and every one of them answers
        /// from a link it resolved at most a second ago rather than searching for a post.
        /// </summary>
        public static bool Has(StowPost post, UpgradeKind kind)
        {
            return PostUpgrade.Has(post, kind);
        }

        /// <summary>
        /// The nearest post within <paramref name="range"/> of a point that carries this
        /// upgrade, or null.
        ///
        /// This is the question the bench service asks - "is the station I am standing at
        /// within reach of a post with a jib" - and it is asked of the POST rather than of
        /// the player on purpose. The post is the fixed thing: a range measured from the
        /// player moves as they shuffle and turns a bench that worked a moment ago into one
        /// that does not.
        /// </summary>
        public static StowPost ServingPost(Vector3 point, UpgradeKind kind, float range)
        {
            return PostUpgrade.ServingPost(point, kind, range);
        }

        // ------------------------------------------------------------------ building

        /// <summary>
        /// The donor. Same chest the post is cloned from, and for the same reason: it
        /// carries the machinery a buildable piece needs - ZNetView, Piece, WearNTear,
        /// placement rules - and building that by hand is exactly the work cloning avoids.
        ///
        /// Its Container is torn out below. The post keeps one because the inventory IS the
        /// post; an upgrade that turned out to be a chest would be the single most
        /// confusing thing in the mod, since it stands beside a piece whose entire job is
        /// moving things between chests.
        /// </summary>
        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;
            if (scene == null) return null;

            foreach (var name in new[] { StowConfig.PostDonor.Value, "piece_chest_wood" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Upgrade donor '" + name + "' does not exist.");
            }

            return null;
        }

        /// <summary>
        /// Builders, one per piece, handed to Prefabs.Keep in GrovePlugin.Awake.
        ///
        /// Cached delegates rather than lambdas at the call site - Keep is called once, so
        /// this is not about allocation, it is so that the same delegate identity is passed
        /// on a re-declaration and Keep's "declared twice is a mod reloading" branch keeps
        /// the prefab it already built.
        /// </summary>
        public static readonly System.Func<GameObject> BuildRail = () => Build(Rail);
        public static readonly System.Func<GameObject> BuildPerch = () => Build(Perch);
        public static readonly System.Func<GameObject> BuildJib = () => Build(Jib);

        private static GameObject Build(UpgradeDef def)
        {
            var source = Donor();
            if (source == null) return null;

            var clone = Prefabs.Clone(source, def.PrefabName);
            if (clone == null) return null;

            // Dressing props are often modelled with a lean baked into the transform so
            // they look casually dropped. Inheriting that tips the whole piece over.
            clone.transform.localRotation = Quaternion.identity;

            // DestroyImmediate, never Destroy: ordinary Destroy is deferred to the end of
            // the frame, and this prefab is registered and buildable within that frame -
            // which would hand out an upgrade that really is a chest.
            foreach (var container in clone.GetComponentsInChildren<Container>(true))
            {
                if (container == null) continue;
                Object.DestroyImmediate(container);
            }

            // The model swap destroys MeshRenderers, and a particle system's renderer is a
            // ParticleSystemRenderer rather than one of those - so the donor's pick-me-up
            // sparkle would survive onto a piece that looks nothing like it.
            foreach (var particles in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles == null) continue;
                Object.DestroyImmediate(particles);
            }

            var piece = clone.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = def.Name.Value;
                piece.m_description = def.Blurb;

                // The same tab as the post itself. An upgrade in a different category from
                // the thing it upgrades is an upgrade nobody finds - and Furniture is where
                // a player who has just built a stowing post already is.
                piece.m_category = Piece.PieceCategory.Furniture;

                // The star in the corner of the build-menu slot. It is a flag rather than
                // art: Hud builds each slot from a prefab carrying an "upgrade" child and
                // does m_upgrade.SetActive(piece.m_isUpgrade).
                piece.m_isUpgrade = true;

                ApplyCost(def, piece);

                // The clone arrives wearing piece_chest_wood's icon, which would advertise
                // all three as wooden chests - the one picture guaranteed to make a player
                // scroll past. Left as the donor's on failure rather than blanked: a wrong
                // picture is bad and an empty slot is worse.
                var icon = Icons.Load(Icons.For(def.Model.Value), def.PrefabName);
                if (icon != null) piece.m_icon = icon;
            }

            // The shape is ours and only the surfaces are the game's. PostModel does the
            // whole of it - strip the donor's renderers, hang the mesh, borrow a material
            // per OBJ group, remap the UVs into each donor's atlas rect, swap the colliders
            // for the .col sidecar - and, for the two that carry a `core` group, light the
            // heartwood and hang the halo on it. That last argument is why the perch and
            // the jib visibly house the heartwood they cost.
            if (!PostModel.Apply(clone, def.Model.Value, def.PrefabName + "_visual",
                                 def.Heartwood))
            {
                StowRuntime.Log.LogWarning(
                    def.PrefabName + " is wearing the donor chest's own body - "
                    + def.Model.Value + " was not found beside the dll.");
            }

            var scale = Mathf.Max(0.05f, Scale.Value);
            clone.transform.localScale = new Vector3(scale, scale, scale);

            var upgrade = clone.GetComponent<PostUpgrade>();
            if (upgrade == null) upgrade = clone.AddComponent<PostUpgrade>();

            // A plain public field, and an int rather than the enum, on purpose. Unity
            // copies serialised fields from the prefab into every instance, so setting it
            // once here is what makes a placed copy remember which upgrade it is - and an
            // int is a type that copy is guaranteed to carry whatever Unity thinks of a
            // runtime-declared enum.
            upgrade.m_kind = (int)def.Kind;

            StowRuntime.Log.LogInfo("Built " + def.PrefabName + " from " + source.name + ".");
            return clone;
        }

        // ------------------------------------------------------------------ the recipe

        /// <summary>
        /// Rewrites every upgrade's recipe from config, and says so once it has stuck.
        ///
        /// This is not tidiness, it closes a hole that would have shipped a free heartwood.
        /// A recipe is an array of ItemDrop references resolved out of ObjectDB, the
        /// heartwood is itself a runtime-registered item, and the piece is built by
        /// Prefabs.Tick as soon as a scene exists - which can be a frame or two before the
        /// real ObjectDB has our item in it. Resolving once at build time therefore had a
        /// window where GroveHeartwood did not resolve, was logged and SKIPPED, and the
        /// perch and the jib stood in the hammer at fine wood and nails for the rest of the
        /// session.
        ///
        /// So the cost is applied again, from the same config string, until every named
        /// item resolves. Cheap once satisfied - one bool - and it is re-armed on every
        /// ObjectDB, because a new world means a new item database and a server handing
        /// over its own list means the same.
        ///
        /// The window itself is harmless: it is at world load, before a player exists, and
        /// the partially-resolved recipe it briefly writes is the safe direction to be
        /// wrong in for exactly as long as nobody can open a build menu.
        /// </summary>
        public static void Reprice()
        {
            var db = ObjectDB.instance;

            // The first ObjectDB.Awake of a session fires against a stub with no items in
            // it. Anything looked up there fails, so this is not the database to price
            // against, and an empty item list is the tell.
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;

            foreach (var def in All)
            {
                if (def.Priced || def.Prefab == null) continue;

                var piece = def.Prefab.GetComponent<Piece>();
                if (piece == null) { def.Priced = true; continue; }

                ApplyCost(def, piece);
            }
        }

        /// <summary>
        /// Called on both ObjectDB entry points - Awake for a local world, CopyOtherDB when
        /// a server hands its item list over. The ItemDrops in a recipe belong to whichever
        /// database was loaded when they were resolved, so a second world has to be priced
        /// against its own.
        /// </summary>
        public static void Invalidate()
        {
            foreach (var def in All) def.Priced = false;
        }

        private static void ApplyCost(UpgradeDef def, Piece piece)
        {
            var missing = false;
            var list = new List<Piece.Requirement>();

            foreach (var entry in (def.Cost.Value ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                int amount;
                if (!int.TryParse(parts[1].Trim(), out amount) || amount <= 0) continue;

                var prefab = ObjectDB.instance == null
                    ? null
                    : ObjectDB.instance.GetItemPrefab(itemName);

                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    // Once, not once a frame: this is retried from an Update until it
                    // takes, and the case that never clears - a typo in the cfg - would
                    // otherwise write this line sixty times a second forever.
                    GrovePlugin.LogOnce(def.PrefabName + "'s cost mentions '" + itemName
                        + "', which nothing can find. If this is the heartwood it will "
                        + "resolve in a moment; if it is a typo the piece stays cheaper "
                        + "than it should be.");
                    missing = true;
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,

                    // Recoverable, and for a reason rather than a convention. Taking one of
                    // these down destroys nothing: the spirit's home is the heartwood, not
                    // the piece around it, so dismantling hands the heartwood back and
                    // whatever was living in it comes with you.
                    m_recover = true
                });
            }

            piece.m_resources = list.ToArray();
            def.Priced = !missing;
        }
    }

    /// <summary>
    /// The component on a placed upgrade. It carries which kind it is, so a rail standing
    /// near a post does not quietly count as a perch, and it owns the link: which post it
    /// serves, the run of motes that says so, and the hover line.
    /// </summary>
    internal class PostUpgrade : MonoBehaviour, Hoverable
    {
        private static readonly List<PostUpgrade> All = new List<PostUpgrade>();

        /// <summary>
        /// Public and a plain field: Unity copies serialised fields from the prefab into
        /// every instance, so this is what makes a placed copy remember what it is. An int
        /// holding an <see cref="UpgradeKind"/> - see the comment where it is set.
        /// </summary>
        public int m_kind;

        private Piece _piece;

        /// <summary>
        /// False on a placement ghost, which is a copy of the prefab with every component
        /// awake on it and no ZDO. Vanilla's StationExtension.Awake gates its whole
        /// registration on exactly this check, and the reason is worth keeping: without it
        /// the translucent copy following your cursor registers as a real upgrade, draws
        /// its own motes from wherever the cursor happens to be, and counts towards a post
        /// before you have built anything.
        /// </summary>
        private bool _placed;

        /// <summary>
        /// The post this piece serves, resolved from the world and re-resolved on a timer.
        ///
        /// A cached REFERENCE, which is a different animal from the cached flag that cost a
        /// built post on 2026-08-16. That one was a static bool answering "have I
        /// registered?" for a scene that had been torn down and rebuilt underneath it. This
        /// is a live object compared with Unity's == null, so a post that is destroyed
        /// reads as gone immediately whatever this field holds, and the worst a stale entry
        /// can do is point at a dead post for up to a second while a live one stands in
        /// range too.
        /// </summary>
        private StowPost _post;
        private float _nextResolve;

        /// <summary>Where on the post the motes land - its heartwood, if it has one.</summary>
        private Transform _target;

        private GameObject _connection;

        /// <summary>
        /// How far up this piece the link starts, measured off its own colliders rather
        /// than configured. A number in the cfg would be right for one model and silently
        /// wrong for every other, and the model is a setting.
        /// </summary>
        private float _linkHeight;
        private bool _linkHeightKnown;

        /// <summary>Seconds between asking the world which post is nearest.</summary>
        private const float ResolveInterval = 1f;

        private void Awake()
        {
            _piece = GetComponent<Piece>();

            var nview = GetComponent<ZNetView>();
            _placed = nview != null && nview.GetZDO() != null;
            if (!_placed) return;

            All.Add(this);
        }

        private void OnDestroy()
        {
            StopConnectionEffect();
            if (_placed) All.Remove(this);
        }

        public UpgradeKind Kind { get { return (UpgradeKind)m_kind; } }

        // ------------------------------------------------------------------ the post

        /// <summary>
        /// The post this upgrade feeds, or null when it is standing on its own.
        ///
        /// Nearest wins, and nearest is chosen because it is stable: distance does not
        /// change when something is destroyed and rebuilt, where "whichever registered
        /// first" would hand the credit around behind the player's back.
        /// </summary>
        private StowPost Post
        {
            get
            {
                if (Time.time < _nextResolve) return _post;
                _nextResolve = Time.time + ResolveInterval;

                var found = StowPost.Nearest(transform.position, PostUpgrades.Range.Value);
                if (found != _post)
                {
                    _post = found;
                    _target = null;
                }

                if (_post != null && _target == null) _target = LinkTarget(_post);

                return _post;
            }
        }

        /// <summary>
        /// Where the motes should land. The post's heartwood if it has one - that is where
        /// the spirit lives and where the post's own light comes from, so a run of motes
        /// arriving there reads as feeding the thing rather than hitting a post.
        /// </summary>
        private static Transform LinkTarget(StowPost post)
        {
            foreach (var child in post.GetComponentsInChildren<Transform>(true))
            {
                if (child == null) continue;
                if (child.name == PostModel.HeartwoodAnchor) return child;
            }

            return post.transform;
        }

        /// <summary>
        /// Whether this post already has an upgrade of this kind, so an effect can ask
        /// without knowing anything about pieces.
        /// </summary>
        public static bool Has(StowPost post, UpgradeKind kind)
        {
            if (post == null) return false;

            for (var i = 0; i < All.Count; i++)
            {
                var upgrade = All[i];
                if (upgrade == null || upgrade.m_kind != (int)kind) continue;
                if (upgrade.Post == post) return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest post within range of a point that carries this upgrade.
        ///
        /// Walked from the upgrades rather than from the posts, which is the cheaper
        /// direction: an upgrade already knows its post, so this is one pass over a handful
        /// of pieces with no searching inside it.
        /// </summary>
        public static StowPost ServingPost(Vector3 point, UpgradeKind kind, float range)
        {
            StowPost best = null;
            var bestSq = range * range;

            for (var i = 0; i < All.Count; i++)
            {
                var upgrade = All[i];
                if (upgrade == null || upgrade.m_kind != (int)kind) continue;

                var post = upgrade.Post;
                if (post == null) continue;

                var distance = (post.transform.position - point).sqrMagnitude;
                if (distance > bestSq) continue;

                bestSq = distance;
                best = post;
            }

            return best;
        }

        /// <summary>
        /// How many upgrades of this kind stand closer to the same post than this one.
        ///
        /// Which is how a piece knows whether it is doing anything. Every one of these is
        /// once-only - a post either has a rail or it does not - so the second rail built
        /// beside a post changes nothing at all, and without being told that is
        /// indistinguishable from a broken mod.
        /// </summary>
        private int CloserToPost(StowPost post)
        {
            var mine = (transform.position - post.transform.position).sqrMagnitude;
            var ahead = 0;

            for (var i = 0; i < All.Count; i++)
            {
                var upgrade = All[i];
                if (upgrade == null || upgrade == this) continue;
                if (upgrade.m_kind != m_kind) continue;
                if (upgrade.Post != post) continue;

                var theirs = (upgrade.transform.position - post.transform.position)
                    .sqrMagnitude;

                // Ties broken on the instance id, so two pieces the same distance out do
                // not both call themselves redundant and leave the post apparently
                // unserved.
                if (theirs < mine
                    || (theirs == mine && upgrade.GetInstanceID() < GetInstanceID()))
                {
                    ahead++;
                }
            }

            return ahead;
        }

        // ------------------------------------------------------------------ the motes

        /// <summary>
        /// The run of motes from this piece to the post it feeds - the same thing a
        /// chopping block draws to its workbench.
        ///
        /// Lifted from StationExtension.StartConnectionEffect rather than invented: the
        /// game already has one answer to "show me which station this is attached to", and
        /// a second one that looked slightly different would simply read as wrong. Two
        /// details matter and neither is obvious - the effect is rotated so its local +Z
        /// faces the target, and then SCALED along Z by the distance, which is what turns a
        /// stationary puff into something that spans the gap.
        ///
        /// Poked from GetHoverText, exactly as vanilla does for a non-continuous extension.
        /// Looking at the piece is the moment you are asking the question, and a storage
        /// room with three posts in it would be a light show if every upgrade emitted all
        /// the time.
        /// </summary>
        private void PokeEffect(StowPost post, float timeout = 1f)
        {
            if (!_placed || post == null || !PostUpgrades.ShowLink.Value) return;

            var from = transform.position + Vector3.up * LinkHeight();
            var to = _target != null ? _target.position : post.transform.position;

            if (_connection == null)
            {
                var prefab = ConnectionPrefab();
                if (prefab == null) return;

                _connection = Instantiate(prefab, from, Quaternion.identity);
            }

            var span = to - from;
            if (span.sqrMagnitude < 0.0001f) return;

            // Costs nothing and rules one thing out: a prefab held inactive instantiates
            // inactive, and an inactive effect is indistinguishable from a broken one.
            if (!_connection.activeSelf) _connection.SetActive(true);

            _connection.transform.position = from;
            _connection.transform.rotation = Quaternion.LookRotation(span.normalized);
            _connection.transform.localScale = new Vector3(1f, 1f, span.magnitude);

            CancelInvoke("StopConnectionEffect");
            Invoke("StopConnectionEffect", timeout);
        }

        /// <summary>
        /// The top of this piece, in metres above its own origin.
        ///
        /// Measured once, from the colliders the .col sidecar put on it, because the three
        /// models are different heights and each of them is a config entry that somebody
        /// may swap. A constant would have the jib's motes leaving its foot.
        /// </summary>
        private float LinkHeight()
        {
            if (_linkHeightKnown) return _linkHeight;
            _linkHeightKnown = true;

            var top = 0f;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider.isTrigger) continue;

                var height = collider.bounds.max.y - transform.position.y;
                if (height > top) top = height;
            }

            // Half a metre off the ground when a piece has no collision to measure, which
            // is better than the ground itself: a line starting at y=0 is half buried in
            // the terrain and reads as no line at all.
            _linkHeight = top > 0.05f ? top : 0.5f;
            return _linkHeight;
        }

        private void StopConnectionEffect()
        {
            if (_connection == null) return;

            Destroy(_connection);
            _connection = null;
        }

        /// <summary>
        /// The vanilla connection effect, borrowed off whichever station extension the game
        /// has loaded.
        ///
        /// Found by component rather than by name - anything carrying a StationExtension
        /// with a connection prefab will do - so this does not depend on the workbench and
        /// forge extensions keeping their current prefab names. Cached because it is a
        /// scan, and dropped with the rest of the borrowed art when the item list changes.
        /// </summary>
        private static GameObject _connectionPrefab;
        private static bool _connectionSearched;

        private static GameObject ConnectionPrefab()
        {
            if (_connectionSearched) return _connectionPrefab;
            _connectionSearched = true;

            var scene = ZNetScene.instance;
            if (scene == null)
            {
                // Not searched after all: there is no scene to search. Left unset so the
                // next poke tries again rather than remembering a null found in the menu.
                _connectionSearched = false;
                return null;
            }

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var extension = prefab.GetComponent<StationExtension>();
                if (extension == null || extension.m_connectionPrefab == null) continue;

                _connectionPrefab = extension.m_connectionPrefab;
                StowRuntime.Log.LogInfo("Link effect borrowed from " + prefab.name
                    + " (" + _connectionPrefab.name + ").");
                return _connectionPrefab;
            }

            StowRuntime.Log.LogWarning(
                "No StationExtension with a connection effect is loaded - the post "
                + "upgrades will not draw a link to the post they feed.");
            return null;
        }

        public static void ForgetConnectionPrefab()
        {
            _connectionPrefab = null;
            _connectionSearched = false;
        }

        // ------------------------------------------------------------------ hover

        public string GetHoverName()
        {
            return _piece != null ? _piece.m_name : "";
        }

        /// <summary>
        /// How far up the hover text floats. Zero, which is the vanilla default: these sit
        /// on the ground beside the post, so the post's own text position is the one to
        /// match, and matching it means not moving.
        /// </summary>
        public float GetHoverOffset()
        {
            return 0f;
        }

        /// <summary>
        /// What it does, and which post it is feeding.
        ///
        /// Kynda's upgrades deliberately say neither - a chopping block reads "Chopping
        /// block" and you learn the rest from the motes. These say both, because they are
        /// not one effect in two flavours: three pieces stand beside one post, each
        /// changing something different about it, and "which of the three is this one"
        /// cannot be read off a silhouette from across a storage room.
        ///
        /// The post is named by its distance rather than by a label, since every post in
        /// the game is called the same thing. The motes are the real answer and this is the
        /// caption under them.
        /// </summary>
        public string GetHoverText()
        {
            var post = Post;
            PokeEffect(post);

            var name = GetHoverName();
            var def = PostUpgrades.For(Kind);
            var what = def != null ? def.Effect : "";

            if (post == null)
            {
                return Localization.instance.Localize(
                    name + "\n<color=grey>" + what + "</color>"
                    + "\n<color=grey>no stowing post within "
                    + PostUpgrades.Range.Value.ToString("0.#")
                    + "m - it is doing nothing</color>");
            }

            var distance = Vector3.Distance(transform.position, post.transform.position);

            // The one case that must be said out loud. These are once-only, so a second
            // piece of the same kind beside the same post changes nothing whatsoever - and
            // silence there is indistinguishable from the mod being broken, which is the
            // first reading anybody reaches for.
            if (CloserToPost(post) > 0)
            {
                return Localization.instance.Localize(
                    name + "\n<color=grey>" + what + "</color>"
                    + "\n<color=grey>that post already has one - this adds nothing</color>");
            }

            return Localization.instance.Localize(
                name + "\n<color=grey>" + what + "</color>"
                + "\n<color=#D9A441>feeding the stowing post " + distance.ToString("0.#")
                + "m away</color>");
        }
    }
}
