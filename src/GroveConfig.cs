using BepInEx.Configuration;
using UnityEngine;

namespace Grove
{
    internal static class GroveConfig
    {
        public static ConfigEntry<bool> TestMode;

        public static ConfigEntry<string> SpiritName;
        public static ConfigEntry<float> SpiritScale;
        public static ConfigEntry<int> MoteCount;

        public static ConfigEntry<float> SpiritRise;

        public static ConfigEntry<string> FadeEffect;

        public static ConfigEntry<string> HeartwoodName;
        public static ConfigEntry<string> HeartwoodDonor;
        public static ConfigEntry<int> HeartwoodStack;
        public static ConfigEntry<int> HeartwoodGiven;

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

        public static ConfigEntry<bool> CoupleToStow;
        public static ConfigEntry<string> StowPostCost;

        public static ConfigEntry<string> GlowDonors;
        public static ConfigEntry<bool> DumpMaterials;
        public static ConfigEntry<string> LookForPrefabs;

        /// <summary>
        /// Three greydwarfs, not forty.
        ///
        /// Sixty is the right price for the feature and completely wrong for checking
        /// that it works at all: every pass over the sapling's stages, the spirit, the
        /// commune and the heartwood costs an hour of killing first. A switch rather
        /// than a number to edit and remember to put back - the number is the thing
        /// most likely to be left at 3 and then played on.
        /// </summary>
        private const float TestBlood = 3f;

        /// <summary>
        /// What one sapling actually needs, honouring TestMode.
        ///
        /// Read live rather than cached: lowering this under a sapling that is already
        /// part-fed leaves it over the line, and the next kill opens it. That is the
        /// wanted behaviour for a test and harmless otherwise, since Feed is the only
        /// caller that can open one.
        /// </summary>
        public static float BloodNeededNow()
        {
            return TestMode.Value ? TestBlood : Mathf.Max(1f, BloodNeeded.Value);
        }

        public static void Bind(ConfigFile config)
        {
            TestMode = config.Bind("Diagnostics", "TestMode", false,
                "Drops what a sapling needs to three greydwarfs, so the whole chain - "
                + "stages, spirit, commune, heartwood - can be walked in a minute "
                + "instead of an hour. Announced in the log on startup so it is hard "
                + "to leave on.");

            SpiritName = config.Bind("Spirit", "SpiritName", "Forest spirit",
                "What it is called when you look at it.");

            SpiritScale = config.Bind("Spirit", "SpiritScale", 1f,
                "Scale of the whole thing. The mesh is built knee-to-chest height.");

            MoteCount = config.Bind("Spirit", "MoteCount", 7,
                "How many beads ride the hoop. Seven is what the mesh was designed "
                + "around; the hoop does not care, and neither does the drift.");

            SpiritRise = config.Bind("Spirit", "SpiritRise", 0.4f,
                "How far above the sapling the spirit appears.");

            FadeEffect = config.Bind("Spirit", "FadeEffect", "vfx_prayer",
                "A vanilla effect played where the spirit stood when it goes. Blank for "
                + "none. Named rather than built because a particle system authored here "
                + "would look like a mod and the game's own does not - but which effect "
                + "is right is a question for the game, so a name that does not resolve "
                + "costs the moment its flourish rather than breaking it.");

            // ---------------------------------------------------------- heartwood

            HeartwoodName = config.Bind("Heartwood", "HeartwoodName", "Heartwood",
                "What the material is called.");

            HeartwoodDonor = config.Bind("Heartwood", "HeartwoodDonor", "SurtlingCore",
                "The vanilla item it is cloned from, for its ItemDrop, Rigidbody, "
                + "colliders and float-in-water behaviour. Only the mesh, the name and "
                + "the icon change.");

            HeartwoodStack = config.Bind("Heartwood", "HeartwoodStack", 10,
                "How many fit in a slot.");

            HeartwoodGiven = config.Bind("Heartwood", "HeartwoodGiven", 1,
                "How much one spirit hands over. One by default: the whole chain exists "
                + "to make a single stowing post cost something real, and handing over "
                + "five would make the second through fifth free.");

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

            // ---------------------------------------------------------- stow

            CoupleToStow = config.Bind("Stow", "CoupleToStow", true,
                "If the Stow mod is installed, make its stowing post cost heartwood. "
                + "Off leaves Stow's own recipe alone. Nothing here touches Stow when it "
                + "is absent, and Stow is never told about this mod - it stays a mod that "
                + "works on its own and still builds its post out of wood and fine wood.");

            StowPostCost = config.Bind("Stow", "StowPostCost", "GroveHeartwood:1",
                "What is *added* to the stowing post's cost, as Item:Amount. Added rather "
                + "than replacing, because the rest of that recipe is Stow's setting and "
                + "someone may have changed it deliberately. An ingredient already there "
                + "is raised to whichever amount is higher rather than counted twice.");

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
