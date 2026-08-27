using System.Collections.Generic;
using BepInEx.Configuration;
using Grove;
using HarmonyLib;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// The eight wild plants, registered into whatever world is currently loaded.
    ///
    /// Built once per process and re-registered per world. That split is the trap this
    /// whole file is shaped around: ZNetScene and ObjectDB are torn down and rebuilt when
    /// you load a second world - including logging out to the menu and back in - and the
    /// new ZNetScene rebuilds its private m_namedPrefabs in Awake from its own serialised
    /// list, which has never heard of ours. A "done" flag answered from a static field
    /// therefore says yes while the live scene knows nothing, Register early-returns, and
    /// every ZDO of every one of these prefabs is discarded silently and permanently. Vaettir
    /// lost a built piece to exactly that on 2026-08-16. So Ready asks the world.
    /// </summary>
    internal static class WildPlants
    {
        private static readonly List<WildPlant> Plants = new List<WildPlant>();

        /// <summary>Built prefabs by plant, kept across worlds because building is the
        /// expensive half and only the registration goes stale.</summary>
        private static readonly Dictionary<WildPlant, GameObject> Prefabs =
            new Dictionary<WildPlant, GameObject>();

        /// <summary>Piece name to plant, for the skill gate - which is asked about every
        /// piece in the build menu on every frame it is open, so it must not be a scan.</summary>
        private static readonly Dictionary<string, WildPlant> ByPiece =
            new Dictionary<string, WildPlant>();

        /// <summary>Grown prefab name to plant, for the dig-up hover and interact -
        /// asked on every hover, so a dictionary and never a scan.</summary>
        private static readonly Dictionary<string, WildPlant> ByGrown =
            new Dictionary<string, WildPlant>();

        /// <summary>Built uprooted items by plant, kept across worlds like Prefabs.</summary>
        private static readonly Dictionary<WildPlant, GameObject> Items =
            new Dictionary<WildPlant, GameObject>();

        private static readonly HashSet<string> Said = new HashSet<string>();

        /// <summary>Plants whose prefab could not be built at all - a missing model, a bush
        /// this version of the game does not have - so the retry stops asking.</summary>
        private static readonly HashSet<WildPlant> Refused = new HashSet<WildPlant>();

        /// <summary>
        /// Binds the roster's config rows. Called from the plugin's Awake, before any world
        /// exists, because BepInEx writes the file on first run and the rows have to be in
        /// it for anyone to edit them.
        /// </summary>
        public static void Bind(ConfigFile config)
        {
            Plants.Clear();
            ByPiece.Clear();

            foreach (var plant in WildPlant.Roster())
            {
                ThicketConfig.Row(config, plant);
                Plants.Add(plant);
                ByPiece[plant.PieceName] = plant;
                ByGrown[plant.Grown] = plant;
            }
        }

        /// <summary>
        /// The one thing anything outside this file asks: is this piece one of ours, and if
        /// so which plant is it.
        /// </summary>
        public static WildPlant Of(Piece piece)
        {
            if (piece == null) return null;

            WildPlant plant;

            // The placement ghost is a clone and Unity has appended "(Clone)" to its name,
            // so an exact lookup finds the real piece and silently misses the ghost - which
            // is the object the gate is asked about while you are holding the cultivator.
            var name = piece.gameObject.name;
            if (ByPiece.TryGetValue(name, out plant)) return plant;

            var clone = name.IndexOf("(Clone)", System.StringComparison.Ordinal);
            if (clone <= 0) return null;

            return ByPiece.TryGetValue(name.Substring(0, clone), out plant) ? plant : null;
        }

        /// <summary>The plant whose grown prefab this is, or null. For the dig.</summary>
        public static WildPlant OfGrown(string prefabName)
        {
            WildPlant plant;
            return ByGrown.TryGetValue(prefabName, out plant) ? plant : null;
        }

        /// <summary>The plant whose piece this is, by bare prefab name.</summary>
        public static WildPlant OfPieceName(string prefabName)
        {
            WildPlant plant;
            return ByPiece.TryGetValue(prefabName, out plant) ? plant : null;
        }

        /// <summary>
        /// Everything in this world, or as much of it as can be. Idempotent, and safe to
        /// call every frame until it takes.
        /// </summary>
        public static bool Register()
        {
            if (!ThicketConfig.Enabled.Value) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;

            // The first ObjectDB.Awake of a session fires against a stub - two status
            // effects and no items at all - where every cost item lookup fails. Refusing
            // here rather than warning eight times about berries that do exist.
            if (ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0)
                return false;

            var table = CultivatorPieces();
            if (table == null) return false;

            var done = true;

            foreach (var plant in Plants)
            {
                if (Refused.Contains(plant)) continue;
                if (!Register(plant, table)) done = false;
            }

            return done;
        }

        private static bool Register(WildPlant plant, PieceTable table)
        {
            var scene = ZNetScene.instance;
            var live = scene.GetPrefab(plant.PieceName);

            // The uprooted item first, because the piece cost row resolves it through
            // ObjectDB and a piece registered before its currency reads as free.
            GameObject item;
            if (!Items.TryGetValue(plant, out item) || item == null)
            {
                item = WildPrefab.BuildItem(plant);
                if (item == null)
                {
                    Refused.Add(plant);
                    return true;
                }
                Items[plant] = item;
                ThicketConfig.Say(GrovePlugin.Log, plant.ItemName + " built.");
            }
            if (scene.GetPrefab(plant.ItemName) == null) AddToScene(plant.ItemName, item);
            var db = ObjectDB.instance;
            if (db.GetItemPrefab(plant.ItemName) == null)
            {
                if (!db.m_items.Contains(item)) db.m_items.Add(item);
                try
                {
                    // m_itemByHash is built once in UpdateRegisters and never again -
                    // the list alone leaves the item unfindable by name.
                    AccessTools.Method(typeof(ObjectDB), "UpdateRegisters").Invoke(db, null);
                }
                catch (System.Exception e)
                {
                    GrovePlugin.Log.LogError("Could not refresh ObjectDB for "
                                             + plant.ItemName + ": " + e.Message);
                }
            }

            GameObject prefab;
            if (!Prefabs.TryGetValue(plant, out prefab) || prefab == null)
            {
                // Read here rather than at bind time. A row edited between two worlds in one
                // session then takes effect on the second, and more usefully, the cost item
                // cannot be resolved until ObjectDB exists.
                if (!plant.Read(ThicketConfig.RowFor(plant.Id)))
                {
                    Refused.Add(plant);
                    return true;
                }

                prefab = WildPrefab.Build(plant);
                if (prefab == null)
                {
                    // Refused rather than retried. A missing model file or a bush this
                    // version of the game does not carry will not appear on the next frame,
                    // and retrying it sixty times a second buries the log line that says so.
                    Refused.Add(plant);
                    return true;
                }

                Prefabs[plant] = prefab;
                ThicketConfig.Say(GrovePlugin.Log, plant.Id + " built.");
            }

            if (live == null) AddToScene(plant.PieceName, prefab);
            if (!table.m_pieces.Contains(prefab)) AddToCultivator(plant, prefab, table);

            return scene.GetPrefab(plant.PieceName) != null
                && scene.GetPrefab(plant.ItemName) != null
                && table.m_pieces.Contains(prefab);
        }

        private static void AddToScene(string name, GameObject prefab)
        {
            var scene = ZNetScene.instance;

            if (!scene.m_prefabs.Contains(prefab)) scene.m_prefabs.Add(prefab);

            try
            {
                // Both halves. The list alone does nothing: ZNetScene looks prefabs up
                // through the private dictionary, which is built once in Awake from that
                // list and never rebuilt.
                var named = (Dictionary<int, GameObject>)
                    AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene);
                named[name.GetStableHashCode()] = prefab;
            }
            catch (System.Exception e)
            {
                GrovePlugin.Log.LogError("Could not register " + name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Onto the cultivator, because it is the tool already in your hand when you think
        /// "I want to plant this". The hammer would work and would be wrong.
        /// </summary>
        private static void AddToCultivator(WildPlant plant, GameObject prefab, PieceTable table)
        {
            table.m_pieces.Add(prefab);

            // Logged on the add rather than in the caller, which is retried every frame.
            GrovePlugin.Log.LogInfo(plant.Title + " added to the cultivator.");
        }

        /// <summary>
        /// The cultivator's piece table, for the ObjectDB that exists now.
        ///
        /// Asked of the game rather than remembered, for the same reason Ready is: ObjectDB
        /// is rebuilt per world, so the Cultivator from the last one is a different object
        /// with a different list. A flag saying "already added" keeps all eight out of the
        /// menu for the whole of the second world.
        /// </summary>
        private static PieceTable CultivatorPieces()
        {
            if (ObjectDB.instance == null) return null;

            var tool = ObjectDB.instance.GetItemPrefab("Cultivator");
            var drop = tool != null ? tool.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                return null;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            return table != null && table.m_pieces != null ? table : null;
        }

        /// <summary>
        /// Warns once rather than every frame.
        ///
        /// Registration is retried until it takes, so a complaint inside it is a complaint
        /// sixty times a second - which buries the log it was written to help with.
        /// </summary>
        public static void Warn(string message)
        {
            if (GrovePlugin.Log == null || !Said.Add(message)) return;
            GrovePlugin.Log.LogWarning("Thicket: " + message);
        }
    }
}
