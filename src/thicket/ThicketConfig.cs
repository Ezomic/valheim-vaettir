using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace Thicket
{
    /// <summary>
    /// Settings for the wild plants, one line each.
    ///
    /// One entry per plant rather than four entries per plant, because eight plants at
    /// four settings is thirty-two lines of config and nobody edits a file that long -
    /// they give up on it. The cost of packing them is a format to explain, and the
    /// explanation is in the comment on every single row where it is read.
    ///
    /// What is deliberately *not* configurable is which vanilla prefab each row grows
    /// into. That is the plant's identity rather than a taste: pointing the raspberry row
    /// at a blueberry bush does not tune the raspberry, it makes a second blueberry with
    /// the wrong name, wrong icon and wrong cost. The tunables are what it costs, where
    /// it will grow, how long it takes and what you must have earned first.
    /// </summary>
    internal static class ThicketConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<string> Donor;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> SpacingScale;
        public static ConfigEntry<bool> SayTheLevel;
        public static ConfigEntry<float> DigReach;
        public static ConfigEntry<float> DigAssist;
        public static ConfigEntry<bool> Verbose;

        /// <summary>Each plant's row, keyed by its id.</summary>
        private static readonly Dictionary<string, ConfigEntry<string>> Rows =
            new Dictionary<string, ConfigEntry<string>>();

        /// <summary>
        /// The format, spelled once and quoted into every row's comment.
        ///
        /// Four fields because they are the four questions anyone asks of a plant: when may
        /// I, what does it cost, where does it live, how long does it take.
        /// </summary>
        private const string Format =
            "Farming level | cost | biomes | seconds to take root (min-max).\n"
            + "Level 0 means no gate. Biomes are Valheim's own names, comma separated. "
            + "The grow time is a range and each seedling picks its own point in it from "
            + "its ZDO seed, so a row planted together does not come up all at once.";

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Thicket", "Enabled", true,
                "Move the things the game does not let you move: dig wild berry bushes, "
                + "thistle, dandelion and mushrooms up with the cultivator and replant "
                + "them where you want them. Off and none of the pieces or items are "
                + "registered at all - which is safe to do only before any have been "
                + "replanted or dug up, because a prefab that no longer resolves takes "
                + "every standing seedling and every uprooted item in a chest with it.");

            Donor = config.Bind("Thicket", "Donor", "sapling_carrot",
                "The vanilla plant each seedling is cloned from, for its Piece, its "
                + "ZNetView, its WearNTear and its Plant. Unlike the ancient sapling the "
                + "donor's Plant is kept and reconfigured rather than torn out: an ordinary "
                + "plant growing on an ordinary timer is exactly what is wanted here, so "
                + "riding the game's own component is both less code and one less thing to "
                + "break on an update.");

            Scale = config.Bind("Thicket", "Scale", 1f,
                "Scale of the seedling in the ground. The grown bush is the game's own and "
                + "is not affected.");

            SpacingScale = config.Bind("Thicket", "SpacingScale", 1f,
                "Multiplier on how much clear ground each seedling demands around it. The "
                + "base figure comes from the plant's own size - a bush needs metres where "
                + "a mushroom needs centimetres - and this scales all of them at once. "
                + "Below 1 packs them tighter than the grown bushes will fit.");

            DigReach = config.Bind("Thicket", "DigReach", 6f,
                "How far away a wild plant can be dug up, in metres, measured from you "
                + "rather than from the camera. Roughly the reach of any other click on "
                + "the world.");

            DigAssist = config.Bind("Thicket", "DigAssist", 12f,
                "How many degrees off the crosshair a plant may sit and still be dug, "
                + "when the crosshair itself hits nothing. Wild plants are ragged and "
                + "several have a collider narrower than they look, so an exact ray is a "
                + "harder shot than the plant appears to be. The plant nearest the aim "
                + "line wins, not the nearest plant. 0 turns the help off.");

            SayTheLevel = config.Bind("Thicket", "SayTheLevel", true,
                "Write the Farming level a plant needs into its build-menu description, and "
                + "say it on screen when you try to plant one you have not earned. Without "
                + "this the piece is simply greyed out, which reads as missing materials and "
                + "sends you looking through your chests for berries you are already "
                + "carrying.");

            Verbose = config.Bind("Thicket", "Verbose", false,
                "Log every row as it is parsed, and every prefab that could not be found.");
        }

        /// <summary>
        /// Binds one plant's row. Called from the roster so the defaults live beside the
        /// plant they describe rather than in a second list here that can drift from it.
        /// </summary>
        public static ConfigEntry<string> Row(ConfigFile config, WildPlant plant)
        {
            var entry = config.Bind("Plants", plant.Id, plant.DefaultRow,
                plant.Note + "\n" + Format);

            Rows[plant.Id] = entry;
            return entry;
        }

        public static string RowFor(string id)
        {
            ConfigEntry<string> entry;
            return Rows.TryGetValue(id, out entry) ? entry.Value : null;
        }

        public static void Say(ManualLogSource log, string message)
        {
            if (Verbose.Value) log.LogInfo("Thicket: " + message);
        }
    }
}
