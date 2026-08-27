using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thicket
{
    /// <summary>
    /// One plantable wild thing: what it is, what it grows into, and what it costs you.
    ///
    /// The fields split into two halves on purpose. Everything set here in code is the
    /// plant's identity - its permanent prefab name, the vanilla bush it becomes, the model
    /// it wears - and everything read off the config row is a tunable. Identity in code
    /// because a renamed piece destroys every one already planted, and because pointing a
    /// row at a different bush makes a different plant rather than a tuned one.
    /// </summary>
    internal sealed class WildPlant
    {
        /// <summary>How big a shape the seedling is, which is the whole of what the family
        /// decides: the model it wears and how much clear ground it demands.</summary>
        public enum Shape
        {
            Bush,
            Mushroom,
            Thistle,
            Dandelion
        }

        public string Id;

        /// <summary>
        /// The network identity, and permanent.
        ///
        /// ZNetScene keys prefabs by name.GetStableHashCode() and every saved ZDO stores
        /// that hash. A prefab whose name no longer resolves is discarded on load rather
        /// than errored, so renaming one of these silently deletes every seedling anybody
        /// has planted, in every save. Treat these strings as written in stone.
        /// </summary>
        public string PieceName;

        /// <summary>What the hover text and the build menu call it.</summary>
        public string Title;

        /// <summary>
        /// The uprooted item's network identity - as permanent as PieceName, and for the
        /// same reason: a dropped item is a ZDO keyed on this name, and renaming it
        /// silently deletes every uprooted plant lying in a chest or on the ground.
        /// </summary>
        public string ItemName;

        /// <summary>The vanilla prefab it grows into. Already in ZNetScene - nothing new is
        /// registered for the grown plant, which is why this feature adds no risk at all to
        /// the bushes themselves.</summary>
        public string Grown;

        public Shape Form;
        public string Model;
        public string Icon;

        /// <summary>
        /// Whether the cold is survivable where it grows.
        ///
        /// Plant.UpdateHealth refuses Mountain and Deep North outright unless this is set,
        /// so the blue mushroom - which lives nowhere else - could never reach Healthy and
        /// therefore never grow, with the hover text reading "too cold" and no other clue.
        /// Deriving it from the biome list would be neater and is wrong: this is a property
        /// of the plant, and the row is allowed to be edited to somewhere warm.
        /// </summary>
        public bool TolerateCold;

        public string DefaultRow;
        public string Note;

        // -------------------------------------------------------------- parsed from config

        public int Level;
        public string CostItem;
        public int CostAmount;
        public Heightmap.Biome Biomes;
        public float GrowMin;
        public float GrowMax;

        /// <summary>
        /// How much clear ground the seedling demands, before SpacingScale.
        ///
        /// From the shape rather than from config, because the number that matters is the
        /// size of the *grown* plant and that is not the player's business to tune. A
        /// raspberry bush is metres across and a mushroom is a handspan; one figure for
        /// both would either pack bushes so they choke each other or scatter mushrooms
        /// across a whole clearing.
        /// </summary>
        public float Radius
        {
            get
            {
                switch (Form)
                {
                    case Shape.Bush: return 1.6f;
                    case Shape.Thistle: return 0.5f;
                    case Shape.Dandelion: return 0.4f;
                    default: return 0.5f;
                }
            }
        }

        /// <summary>
        /// Reads the config row over the defaults, and says so rather than throwing.
        ///
        /// A row someone has half-edited must not take the plant out of the game - it must
        /// take the edit out. So every field falls back to the default row's own value and
        /// the log names what it could not read. The alternative is a mod that silently
        /// loses a piece to a typo in a config file, which is a bug report nobody can write.
        /// </summary>
        public bool Read(string row)
        {
            var fields = (row ?? "").Split('|');
            if (fields.Length < 4)
            {
                if (!string.IsNullOrEmpty(row))
                    WildPlants.Warn(Id + ": a row needs four fields separated by | - using the "
                                 + "default instead.");
                fields = DefaultRow.Split('|');
            }

            Level = Mathf.Max(0, Int(fields[0], 0));

            var cost = fields[1].Split(':');
            CostItem = cost[0].Trim();
            CostAmount = cost.Length > 1 ? Mathf.Max(1, Int(cost[1], 1)) : 1;

            Biomes = ParseBiomes(fields[2]);
            if (Biomes == 0)
            {
                WildPlants.Warn(Id + ": no biome in " + fields[2].Trim() + " was recognised. "
                             + "It would never grow anywhere, so the default is used.");
                Biomes = ParseBiomes(DefaultRow.Split('|')[2]);
            }

            ParseRange(fields[3]);
            return CostItem.Length > 0;
        }

        private static int Int(string text, int fallback)
        {
            int value;
            return int.TryParse((text ?? "").Trim(), out value) ? value : fallback;
        }

        /// <summary>
        /// Names to a bitmask, one name at a time.
        ///
        /// Enum.Parse would take the whole comma-separated string in one call and is not
        /// used, because one bad name in a list of three throws away the other two. Parsed
        /// singly, a typo costs its own biome and says which one it was.
        /// </summary>
        private static Heightmap.Biome ParseBiomes(string text)
        {
            var mask = (Heightmap.Biome)0;

            foreach (var name in (text ?? "").Split(','))
            {
                var trimmed = name.Trim();
                if (trimmed.Length == 0) continue;

                try
                {
                    mask |= (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), trimmed, true);
                }
                catch (Exception)
                {
                    WildPlants.Warn(trimmed + " is not one of Valheim's biome names.");
                }
            }

            return mask;
        }

        /// <summary>
        /// "1800-3000" into a pair, and a single number into a pair of itself.
        ///
        /// Plant lerps between the two off its own ZDO seed, so a range is what stops a rank
        /// of seeds sown in one press from all coming up in the same second - which looks
        /// like a scripted event rather than a garden.
        /// </summary>
        private void ParseRange(string text)
        {
            var parts = (text ?? "").Trim().Split('-');

            GrowMin = Mathf.Max(1f, Int(parts[0], 1800));
            GrowMax = parts.Length > 1 ? Mathf.Max(GrowMin, Int(parts[1], (int)GrowMin)) : GrowMin;
        }

        // -------------------------------------------------------------- the roster

        /// <summary>
        /// The eight, in the order they unlock.
        ///
        /// The ladder is the feature. Farming levels come slowly and mean very little on
        /// their own once your carrots are in the ground, so hanging the wild plants off it
        /// gives the skill somewhere to go: dandelions and raspberries early, cloudberries
        /// once you are living in the Plains, and the blue mushroom last because it is the
        /// only one whose home wants a cape.
        ///
        /// The cost is the uprooted plant itself, and only digging a wild one up with the
        /// cultivator produces one. That is the conservation law the first design lacked:
        /// berries-buy-a-bush let the world's bush count grow without bound, where moving
        /// one only ever changes where it stands. The grow times are short because this is
        /// a transplant taking root again, not a seed becoming a plant.
        /// </summary>
        public static List<WildPlant> Roster()
        {
            return new List<WildPlant>
            {
                new WildPlant
                {
                    Id = "Dandelion",
                    PieceName = "thicket_dandelion",
                    ItemName = "thicket_uprooted_dandelion",
                    Title = "Dandelion",
                    Grown = "Pickable_Dandelion",
                    Form = Shape.Dandelion,
                    Model = "thicket_dandelion.obj",
                    Icon = "thicket_dandelion.png",
                    DefaultRow = "5 | thicket_uprooted_dandelion:1 | Meadows | 60-120",
                    Note = "The first one, and cheap. A meadow flower at Farming 5 is the "
                           + "rung that tells you the ladder is there at all."
                },
                new WildPlant
                {
                    Id = "Raspberry",
                    PieceName = "thicket_raspberry",
                    ItemName = "thicket_uprooted_raspberry",
                    Title = "Raspberry bush",
                    Grown = "RaspberryBush",
                    Form = Shape.Bush,
                    Model = "thicket_bush.obj",
                    Icon = "thicket_raspberry.png",
                    DefaultRow = "10 | thicket_uprooted_raspberry:1 | Meadows,BlackForest | 240-420",
                    Note = "The one everybody wants first, so it is early and it is cheap."
                },
                new WildPlant
                {
                    Id = "Mushroom",
                    PieceName = "thicket_mushroom",
                    ItemName = "thicket_uprooted_mushroom",
                    Title = "Mushrooms",
                    Grown = "Pickable_Mushroom",
                    Form = Shape.Mushroom,
                    Model = "thicket_mushroom.obj",
                    Icon = "thicket_mushroom.png",
                    DefaultRow = "15 | thicket_uprooted_mushroom:1 | Meadows,BlackForest | 90-180",
                    Note = "Fast, because mushrooms are eaten about as fast as they are found."
                },
                new WildPlant
                {
                    Id = "Blueberry",
                    PieceName = "thicket_blueberry",
                    ItemName = "thicket_uprooted_blueberry",
                    Title = "Blueberry bush",
                    Grown = "BlueberryBush",
                    Form = Shape.Bush,
                    Model = "thicket_bush.obj",
                    Icon = "thicket_blueberry.png",
                    DefaultRow = "25 | thicket_uprooted_blueberry:1 | BlackForest | 240-420",
                    Note = "Black Forest only. A blueberry hedge in the meadows would be the "
                           + "point at which the biomes stop meaning anything."
                },
                new WildPlant
                {
                    Id = "Thistle",
                    PieceName = "thicket_thistle",
                    ItemName = "thicket_uprooted_thistle",
                    Title = "Thistle",
                    Grown = "Pickable_Thistle",
                    Form = Shape.Thistle,
                    Model = "thicket_thistle.obj",
                    Icon = "thicket_thistle.png",
                    DefaultRow = "35 | thicket_uprooted_thistle:1 | BlackForest,Swamp | 120-240",
                    Note = "Mid-ladder, because thistle is the first of these that is a chore "
                           + "to gather rather than a pleasure."
                },
                new WildPlant
                {
                    Id = "MushroomYellow",
                    PieceName = "thicket_mushroom_yellow",
                    ItemName = "thicket_uprooted_mushroom_yellow",
                    Title = "Yellow mushrooms",
                    Grown = "Pickable_Mushroom_yellow",
                    Form = Shape.Mushroom,
                    Model = "thicket_mushroom.obj",
                    Icon = "thicket_mushroom_yellow.png",
                    DefaultRow = "45 | thicket_uprooted_mushroom_yellow:1 | BlackForest | 90-180",
                    Note = "Grown outdoors in the Black Forest, which is not where you find "
                           + "them - they are a burial chamber crop and a chamber has a roof. "
                           + "Plant refuses anything under one, so the only place these can "
                           + "be farmed is the forest above the crypt."
                },
                new WildPlant
                {
                    Id = "Cloudberry",
                    PieceName = "thicket_cloudberry",
                    ItemName = "thicket_uprooted_cloudberry",
                    Title = "Cloudberry bush",
                    Grown = "CloudberryBush",
                    Form = Shape.Bush,
                    Model = "thicket_bush.obj",
                    Icon = "thicket_cloudberry.png",
                    DefaultRow = "60 | thicket_uprooted_cloudberry:1 | Plains | 240-420",
                    Note = "Late, and Plains only. Cloudberries are most of what makes the "
                           + "Plains worth farming in, and a Meadows cloudberry patch would "
                           + "quietly delete that reason."
                },
                new WildPlant
                {
                    Id = "MushroomBlue",
                    PieceName = "thicket_mushroom_blue",
                    ItemName = "thicket_uprooted_mushroom_blue",
                    Title = "Blue mushrooms",
                    Grown = "Pickable_Mushroom_blue",
                    Form = Shape.Mushroom,
                    Model = "thicket_mushroom.obj",
                    Icon = "thicket_mushroom_blue.png",
                    TolerateCold = true,
                    DefaultRow = "75 | thicket_uprooted_mushroom_blue:1 | Mountain | 90-180",
                    Note = "Last, and the only one that needs the cold tolerance: the "
                           + "mountain is refused by Plant outright otherwise, and a plant "
                           + "that can never be healthy can never grow."
                }
            };
        }
    }
}
