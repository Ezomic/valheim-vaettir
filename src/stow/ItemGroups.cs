using System.Collections.Generic;

namespace Stow
{
    /// <summary>
    /// The groups a chest can ask for, worked out from what the game does with each item
    /// rather than from a list written here.
    ///
    /// This is the whole reason the mod is worth using. Ticking "ore" is one click; naming
    /// the eleven ores by hand is eleven, and gets it wrong the moment a mod adds a twelfth.
    /// So every group is derived from a real seam in the game's own data:
    ///
    ///   ore      is whatever any smelter accepts        (Smelter.m_conversion.m_from)
    ///   bars     is whatever any smelter produces       (Smelter.m_conversion.m_to)
    ///   fuel     is whatever burns in one               (m_fuelItem, on three components)
    ///   raw      is whatever a cooking station accepts  (CookingStation.m_conversion)
    ///   seeds    is whatever the cultivator can plant   (its own piece table)
    ///   building is whatever the hammer asks for        (its own piece table)
    ///   mead     is whatever a fermenter turns out      (Fermenter.m_conversion)
    ///
    /// A mod that adds black metal ore, or a new crop, lands in the right group without
    /// this file knowing it exists. That is the version resilience the rest of these mods
    /// are built on, applied to a catalogue instead of to AI.
    ///
    /// Membership is by *shared* name ("$item_coal"), not prefab name, because that is
    /// what an inventory compares on and what survives quality and variants.
    /// </summary>
    internal sealed class ItemGroup
    {
        public string Id;
        public string Display;
        public readonly HashSet<string> Members = new HashSet<string>();
    }

    internal static class ItemGroups
    {
        private static readonly List<ItemGroup> All = new List<ItemGroup>();
        private static readonly Dictionary<string, ItemGroup> ById =
            new Dictionary<string, ItemGroup>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Prefab name to shared name, so a filter can be written in prefab names.</summary>
        private static readonly Dictionary<string, string> SharedByPrefab =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> PrefabBySharedName =
            new Dictionary<string, string>();

        private static bool _built;

        public static IList<ItemGroup> Groups { get { Build(); return All; } }

        public static bool Ready { get { Build(); return _built; } }

        /// <summary>Forgets the catalogue, so a world with different mods rebuilds it.</summary>
        public static void Invalidate()
        {
            _built = false;
            All.Clear();
            ById.Clear();
            SharedByPrefab.Clear();
            PrefabBySharedName.Clear();
        }

        public static ItemGroup Find(string id)
        {
            Build();
            ItemGroup group;
            return ById.TryGetValue(id, out group) ? group : null;
        }

        /// <summary>The shared name a filter entry refers to, or null if it names nothing.</summary>
        public static string SharedNameOf(string prefabName)
        {
            Build();
            string shared;
            return SharedByPrefab.TryGetValue(prefabName, out shared) ? shared : null;
        }

        public static string PrefabNameOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;

            // m_dropPrefab is set for anything the game loaded or spawned, but an item
            // handed over by another mod may not have one - hence the reverse map.
            if (item.m_dropPrefab != null) return item.m_dropPrefab.name;

            Build();
            string prefab;
            return PrefabBySharedName.TryGetValue(item.m_shared.m_name, out prefab) ? prefab : null;
        }

        public static string DisplayNameOf(string prefabName)
        {
            Build();

            string shared;
            if (!SharedByPrefab.TryGetValue(prefabName, out shared)) return prefabName;

            return Localization.instance != null ? Localization.instance.Localize(shared) : shared;
        }

        // ------------------------------------------------------------------ building

        private static void Build()
        {
            if (_built) return;
            if (ObjectDB.instance == null || ObjectDB.instance.m_items == null) return;

            All.Clear();
            ById.Clear();
            SharedByPrefab.Clear();
            PrefabBySharedName.Clear();

            // Declared in the order they are drawn, so the panel reads roughly the way a
            // storage room is laid out: the bulk you have most of first.
            var ore      = Group("ore",      "Ore");
            var bars     = Group("bars",     "Bars & ingots");
            var fuel     = Group("fuel",     "Fuel");
            var wood     = Group("wood",     "Wood");
            var raw      = Group("raw",      "Raw food");
            var cooked   = Group("cooked",   "Cooked food");
            var mead     = Group("mead",     "Mead & potions");
            var seeds    = Group("seeds",    "Seeds & crops");
            var building = Group("building", "Building materials");
            var material = Group("material", "Crafting materials");
            var gear     = Group("gear",     "Weapons & armour");
            var ammo     = Group("ammo",     "Ammo");
            var trophies = Group("trophies", "Trophies");
            var fish     = Group("fish",     "Fish");
            var valuable = Group("valuable", "Valuables");

            foreach (var prefab in ObjectDB.instance.m_items)
            {
                if (prefab == null) continue;

                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                    continue;

                var shared = drop.m_itemData.m_shared;
                SharedByPrefab[prefab.name] = shared.m_name;
                if (!PrefabBySharedName.ContainsKey(shared.m_name))
                    PrefabBySharedName[shared.m_name] = prefab.name;

                switch (shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Trophy:
                        trophies.Members.Add(shared.m_name); break;

                    case ItemDrop.ItemData.ItemType.Ammo:
                    case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                        ammo.Members.Add(shared.m_name); break;

                    case ItemDrop.ItemData.ItemType.Fish:
                        fish.Members.Add(shared.m_name); break;

                    case ItemDrop.ItemData.ItemType.Material:
                        material.Members.Add(shared.m_name); break;

                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    case ItemDrop.ItemData.ItemType.Bow:
                    case ItemDrop.ItemData.ItemType.Shield:
                    case ItemDrop.ItemData.ItemType.Helmet:
                    case ItemDrop.ItemData.ItemType.Chest:
                    case ItemDrop.ItemData.ItemType.Legs:
                    case ItemDrop.ItemData.ItemType.Hands:
                    case ItemDrop.ItemData.ItemType.Shoulder:
                    case ItemDrop.ItemData.ItemType.Utility:
                    case ItemDrop.ItemData.ItemType.Trinket:
                    case ItemDrop.ItemData.ItemType.Tool:
                    case ItemDrop.ItemData.ItemType.Torch:
                        gear.Members.Add(shared.m_name); break;
                }

                // Food is a property, not a type: cooked meat and a mead are both
                // Consumable, and only one of them fills you up.
                if (shared.m_food > 0f) cooked.Members.Add(shared.m_name);
                else if (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                    mead.Members.Add(shared.m_name);

                if (shared.m_value > 0) valuable.Members.Add(shared.m_name);
            }

            ReadStations(ore, bars, fuel, raw, cooked, mead);
            ReadPieceTable("Hammer", building);
            ReadPieceTable("Cultivator", seeds);

            // Wood is whatever a charcoal kiln eats: the kiln is a Smelter whose output is
            // the fuel every other one burns, which is a sharper definition than any list
            // of log names and picks up modded logs for free.
            SplitFirewood(ore, bars, fuel, wood);

            // A group nothing landed in is a group that would draw an empty row.
            All.RemoveAll(g => g.Members.Count == 0);
            ById.Clear();
            foreach (var group in All) ById[group.Id] = group;

            _built = All.Count > 0;

            if (_built && StowConfig.Verbose != null && StowConfig.Verbose.Value)
                foreach (var group in All)
                    StowPlugin.Log.LogInfo("group " + group.Id + ": " + group.Members.Count + " items");
        }

        private static ItemGroup Group(string id, string display)
        {
            var group = new ItemGroup { Id = id, Display = display };
            All.Add(group);
            ById[id] = group;
            return group;
        }

        private static void ReadStations(ItemGroup ore, ItemGroup bars, ItemGroup fuel,
                                         ItemGroup raw, ItemGroup cooked, ItemGroup mead)
        {
            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return;

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var smelter = prefab.GetComponent<Smelter>();
                if (smelter != null)
                {
                    Add(fuel, smelter.m_fuelItem);
                    if (smelter.m_conversion != null)
                        foreach (var conversion in smelter.m_conversion)
                        {
                            if (conversion == null) continue;
                            Add(ore, conversion.m_from);
                            Add(bars, conversion.m_to);
                        }
                }

                var cooking = prefab.GetComponent<CookingStation>();
                if (cooking != null)
                {
                    Add(fuel, cooking.m_fuelItem);
                    if (cooking.m_conversion != null)
                        foreach (var conversion in cooking.m_conversion)
                        {
                            if (conversion == null) continue;
                            Add(raw, conversion.m_from);
                            Add(cooked, conversion.m_to);
                        }
                }

                var fireplace = prefab.GetComponent<Fireplace>();
                if (fireplace != null) Add(fuel, fireplace.m_fuelItem);

                var fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter != null && fermenter.m_conversion != null)
                    foreach (var conversion in fermenter.m_conversion)
                    {
                        if (conversion == null) continue;
                        Add(mead, conversion.m_from);
                        Add(mead, conversion.m_to);
                    }
            }
        }

        /// <summary>
        /// Everything a tool's own piece table asks for, which is how "building material"
        /// and "seed" are defined without naming a single item.
        /// </summary>
        private static void ReadPieceTable(string toolPrefabName, ItemGroup into)
        {
            if (ObjectDB.instance == null) return;

            var tool = ObjectDB.instance.GetItemPrefab(toolPrefabName);
            if (tool == null) return;

            var drop = tool.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return;

            var table = drop.m_itemData.m_shared.m_buildPieces;
            if (table == null || table.m_pieces == null) return;

            foreach (var pieceGo in table.m_pieces)
            {
                if (pieceGo == null) continue;

                var piece = pieceGo.GetComponent<Piece>();
                if (piece == null || piece.m_resources == null) continue;

                foreach (var requirement in piece.m_resources)
                {
                    if (requirement == null) continue;
                    Add(into, requirement.m_resItem);
                }
            }
        }

        /// <summary>
        /// Pull the logs back out of "ore" and into their own group.
        ///
        /// A charcoal kiln is a Smelter, so its wood-to-coal recipes had already made every
        /// log an "ore" and coal a "bar" - technically true and completely useless to
        /// someone deciding what a chest holds. Anything whose smelted output is a fuel is
        /// firewood, not ore.
        /// </summary>
        private static void SplitFirewood(ItemGroup ore, ItemGroup bars, ItemGroup fuel, ItemGroup wood)
        {
            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return;

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;

                var smelter = prefab.GetComponent<Smelter>();
                if (smelter == null || smelter.m_conversion == null) continue;

                foreach (var conversion in smelter.m_conversion)
                {
                    if (conversion == null || conversion.m_from == null || conversion.m_to == null)
                        continue;

                    var output = SharedNameOfDrop(conversion.m_to);
                    if (output == null || !fuel.Members.Contains(output)) continue;

                    var input = SharedNameOfDrop(conversion.m_from);
                    if (input == null) continue;

                    ore.Members.Remove(input);
                    bars.Members.Remove(output);
                    wood.Members.Add(input);
                }
            }
        }

        private static void Add(ItemGroup group, ItemDrop drop)
        {
            var shared = SharedNameOfDrop(drop);
            if (shared != null) group.Members.Add(shared);
        }

        private static string SharedNameOfDrop(ItemDrop drop)
        {
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                return null;

            return drop.m_itemData.m_shared.m_name;
        }
    }
}
