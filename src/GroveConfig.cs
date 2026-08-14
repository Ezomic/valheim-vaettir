using BepInEx.Configuration;

namespace Grove
{
    internal static class GroveConfig
    {
        public static ConfigEntry<string> SpiritName;
        public static ConfigEntry<float> SpiritScale;
        public static ConfigEntry<int> MoteCount;

        public static ConfigEntry<float> SpiritRise;

        public static ConfigEntry<string> SaplingName;
        public static ConfigEntry<string> SaplingCost;
        public static ConfigEntry<string> SaplingDonor;
        public static ConfigEntry<float> SaplingScale;
        public static ConfigEntry<bool> NeedsCultivated;

        public static ConfigEntry<float> BloodNeeded;
        public static ConfigEntry<float> FeedRange;
        public static ConfigEntry<string> FeedWeights;
        public static ConfigEntry<bool> Messages;
        public static ConfigEntry<bool> Verbose;

        public static ConfigEntry<string> GlowDonors;
        public static ConfigEntry<bool> DumpMaterials;
        public static ConfigEntry<string> LookForPrefabs;

        public static void Bind(ConfigFile config)
        {
            SpiritName = config.Bind("Spirit", "SpiritName", "Forest spirit",
                "What it is called when you look at it.");

            SpiritScale = config.Bind("Spirit", "SpiritScale", 1f,
                "Scale of the whole thing. The mesh is built knee-to-chest height.");

            MoteCount = config.Bind("Spirit", "MoteCount", 7,
                "How many beads ride the hoop. Seven is what the mesh was designed "
                + "around; the hoop does not care, and neither does the drift.");

            SpiritRise = config.Bind("Spirit", "SpiritRise", 0.4f,
                "How far above the sapling the spirit appears.");

            // ---------------------------------------------------------- the sapling

            SaplingName = config.Bind("Sapling", "SaplingName", "Ancient sapling",
                "What the planted seed is called.");

            SaplingCost = config.Bind("Sapling", "SaplingCost", "AncientSeed:1",
                "What planting one costs. Not refunded when you take it back up - a "
                + "seed put into the ground is spent.");

            SaplingDonor = config.Bind("Sapling", "SaplingDonor", "sapling_carrot",
                "The vanilla piece it is cloned from, for its placement rules and its "
                + "Piece and WearNTear. The donor's own Plant component is torn out: it "
                + "grows on a timer, which is exactly what must not happen here.");

            SaplingScale = config.Bind("Sapling", "SaplingScale", 1f,
                "Scale of the planted piece.");

            NeedsCultivated = config.Bind("Sapling", "NeedsCultivated", false,
                "Whether it must go in tilled soil. Off by default: an ancient seed "
                + "answering to a forest is a stranger idea than a crop, and making it "
                + "want a vegetable patch shrinks it.");

            // ---------------------------------------------------------- feeding

            BloodNeeded = config.Bind("Sapling", "BloodNeeded", 60f,
                "How much death it takes. With the default weights that is about forty "
                + "greydwarfs, or rather fewer if you go and find a nest.");

            FeedRange = config.Bind("Sapling", "FeedRange", 24f,
                "How close a kill must be to count. Only the nearest sapling is fed - "
                + "otherwise a heap of saplings planted together would all grow at once "
                + "off the same work.");

            FeedWeights = config.Bind("Sapling", "FeedWeights",
                "Greydwarf:1,Greydwarf_Elite:4,Greydwarf_Shaman:3,Greyling:0",
                "What each death is worth, as Prefab:Amount. Anything not listed is "
                + "worth nothing. A list rather than a faction check, because "
                + "ForestMonsters would also catch trolls, boars and the Elder, and "
                + "'kill anything in the forest' is a duller quest than 'clear out the "
                + "greydwarfs'. Greylings are listed at zero so it is visible that they "
                + "were considered and refused.");

            Messages = config.Bind("Sapling", "Messages", true,
                "Corner counter each time a kill feeds a sapling.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log every feed.");

            // ---------------------------------------------------------- surfaces

            // Deliberately a list and deliberately unconfirmed. Valheim's own shaders
            // are custom, so which prefab yields a material that actually reads as lit
            // is a question for the game rather than for a guess made here - hence
            // DumpMaterials below, and hence a fallback rather than a hard failure.
            GlowDonors = config.Bind("Spirit", "GlowDonors",
                "fire_pit,piece_walltorch,bonfire,Ember,piece_groundtorch_green,guard_stone",
                "Prefabs to try to lift the glowing material from, best first. The first "
                + "one that resolves and has an albedo wins.");

            DumpMaterials = config.Bind("Diagnostics", "DumpMaterials", false,
                "On startup, log every material on each donor with its shader and "
                + "whether it exposes an emission property. This is how to find a donor "
                + "worth naming rather than guessing at one.");

            LookForPrefabs = config.Bind("Diagnostics", "LookForPrefabs", "",
                "Comma-separated words. Logs every loaded prefab whose name contains "
                + "one. The asset manifest lists what exists on disk, most of which is "
                + "never loaded; this lists what is genuinely there.");
        }
    }
}
